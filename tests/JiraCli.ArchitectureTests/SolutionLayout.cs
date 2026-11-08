using System.Reflection;

namespace JiraCli.ArchitectureTests;

/// <summary>The src projects, their assemblies, and the AD-1 reference table.</summary>
internal static class SolutionLayout
{
    public const string Cli = "JiraCli.Cli";
    public const string Application = "JiraCli.Application";
    public const string Domain = "JiraCli.Domain";
    public const string Infrastructure = "JiraCli.Infrastructure";
    public const string Common = "Atlassian.Common";

    /// <summary>AD-1: the only project references each src project may have.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Cli] = new HashSet<string>(StringComparer.Ordinal) { Application, Domain, Infrastructure, Common },
            [Application] = new HashSet<string>(StringComparer.Ordinal) { Domain },
            [Domain] = new HashSet<string>(StringComparer.Ordinal),
            [Infrastructure] = new HashSet<string>(StringComparer.Ordinal) { Domain, Common },
            [Common] = new HashSet<string>(StringComparer.Ordinal),
        };

    /// <summary>Each src project's assembly, anchored on its registration class.</summary>
    public static readonly IReadOnlyDictionary<string, Assembly> Assemblies =
        new Dictionary<string, Assembly>(StringComparer.Ordinal)
        {
            [Cli] = typeof(JiraCli.Cli.ServiceCollectionExtensions).Assembly,
            [Application] = typeof(JiraCli.Application.ServiceCollectionExtensions).Assembly,
            [Domain] = typeof(JiraCli.Domain.ServiceCollectionExtensions).Assembly,
            [Infrastructure] = typeof(JiraCli.Infrastructure.ServiceCollectionExtensions).Assembly,
            [Common] = typeof(Atlassian.Common.ServiceCollectionExtensions).Assembly,
        };

    /// <summary>The expected registration method of each src project (AD-1).</summary>
    public static readonly IReadOnlyDictionary<string, string> RegistrationMethods =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Cli] = "AddCli",
            [Application] = "AddApplication",
            [Domain] = "AddDomain",
            [Infrastructure] = "AddInfrastructure",
            [Common] = "AddAtlassianCommon",
        };

    /// <summary>Maps an assembly name to its project name (the Cli assembly is named <c>jira-cli</c>).</summary>
    public static string? ProjectOfAssembly(string? assemblyName) =>
        Assemblies.FirstOrDefault(pair => pair.Value.GetName().Name == assemblyName).Key;

    /// <summary>Walks up from the test output folder until <c>jira-cli.slnx</c> is found.</summary>
    public static DirectoryInfo RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "jira-cli.slnx")))
            {
                return directory;
            }
        }

        throw new InvalidOperationException($"jira-cli.slnx not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>Returns the outermost declaring type, so compiler-generated nested types map to their source type.</summary>
    public static Type Outermost(Type type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }
}
