using System.Xml.Linq;

namespace JiraCli.ArchitectureTests;

/// <summary>AD-1: project references follow the dependency diagram and nothing else.</summary>
public sealed class ProjectReferenceTests
{
    [Fact]
    public void Every_src_project_is_in_the_AD1_table()
    {
        var projects = SrcProjectFiles()
            .Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Order(StringComparer.Ordinal);

        Assert.Equal(SolutionLayout.AllowedReferences.Keys.Order(StringComparer.Ordinal), projects);
    }

    [Fact]
    public void Csproj_project_references_follow_AD1()
    {
        var actual = SrcProjectFiles().ToDictionary(
            file => Path.GetFileNameWithoutExtension(file.Name),
            file => (IReadOnlyCollection<string>)ReadProjectReferences(file),
            StringComparer.Ordinal);

        var violations = FindViolations(actual);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Compiled_assembly_references_follow_AD1()
    {
        var actual = SolutionLayout.Assemblies.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value.GetReferencedAssemblies()
                .Select(name => SolutionLayout.ProjectOfAssembly(name.Name))
                .OfType<string>()
                .ToArray(),
            StringComparer.Ordinal);

        var violations = FindViolations(actual);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void A_forbidden_reference_is_reported_with_the_project_name()
    {
        var actual = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [SolutionLayout.Domain] = [SolutionLayout.Common],
            [SolutionLayout.Common] = [SolutionLayout.Domain],
            [SolutionLayout.Application] = [SolutionLayout.Domain],
        };

        var violations = FindViolations(actual);

        Assert.Equal(
            [
                $"'{SolutionLayout.Domain}' references '{SolutionLayout.Common}', which AD-1 does not allow.",
                $"'{SolutionLayout.Common}' references '{SolutionLayout.Domain}', which AD-1 does not allow.",
            ],
            violations);
    }

    internal static List<string> FindViolations(IReadOnlyDictionary<string, IReadOnlyCollection<string>> actualReferences)
    {
        var violations = new List<string>();
        foreach (var (project, references) in actualReferences)
        {
            if (!SolutionLayout.AllowedReferences.TryGetValue(project, out var allowed))
            {
                violations.Add($"'{project}' is not in the AD-1 table.");
                continue;
            }

            foreach (var reference in references.Where(reference => !allowed.Contains(reference)))
            {
                violations.Add($"'{project}' references '{reference}', which AD-1 does not allow.");
            }
        }

        return violations;
    }

    private static IEnumerable<FileInfo> SrcProjectFiles() =>
        new DirectoryInfo(Path.Combine(SolutionLayout.RepoRoot().FullName, "src"))
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories);

    private static string[] ReadProjectReferences(FileInfo csproj) =>
        XDocument.Load(csproj.FullName)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/').Split('/')[^1]))
            .ToArray();
}
