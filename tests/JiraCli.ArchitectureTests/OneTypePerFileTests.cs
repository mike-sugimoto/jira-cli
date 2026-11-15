using System.Text.RegularExpressions;

namespace JiraCli.ArchitectureTests;

/// <summary>
/// House rule: every C# file under <c>src</c>, <c>tests</c>, and <c>tools</c> declares at most one
/// top-level type, and the file is named after it. Nested types stay inside their outer type.
/// The repository uses file-scoped namespaces, so a top-level declaration starts at column 0 and a
/// nested one is indented.
/// </summary>
public sealed partial class OneTypePerFileTests
{
    private static readonly string[] Roots = ["src", "tests", "tools"];

    [Fact]
    public void Every_source_file_declares_at_most_one_top_level_type_named_like_the_file()
    {
        var root = SolutionLayout.RepoRoot().FullName;
        var violations = Roots
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file))
            .SelectMany(file => FindViolations(Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Rule_flags_a_second_top_level_type_and_a_mismatched_file_name()
    {
        const string twoTypes =
            "namespace X;\n" +
            "/// <summary>Doc.</summary>\n" +
            "[Obsolete]\n" +
            "internal sealed class Widget\n{\n    private sealed class Nested { }\n}\n" +
            "public enum WidgetKind { A }\n" +
            "internal sealed partial record Holder<T>(T Value);\n";

        Assert.Equal(
            [
                "Widget.cs declares 3 top-level types (Widget, WidgetKind, Holder); put each in its own file.",
            ],
            FindViolations("Widget.cs", twoTypes));

        Assert.Equal(
            ["Wrong.cs declares 'Holder' but is not named Holder.cs."],
            FindViolations("Wrong.cs", "namespace X;\ninternal sealed record Holder<T>(T Value);\n"));
    }

    [Fact]
    public void Rule_accepts_one_type_a_file_without_types_and_nested_types()
    {
        Assert.Empty(FindViolations("Widget.cs", "namespace X;\npublic static class Widget\n{\n    internal interface INested;\n}\n"));
        Assert.Empty(FindViolations("Program.cs", "using X;\n\nreturn 0;\n"));
        Assert.Empty(FindViolations("IThing.cs", "namespace X;\ninternal interface IThing\n{\n}\n"));
    }

    internal static IReadOnlyList<string> FindViolations(string relativePath, string source)
    {
        var names = TopLevelDeclaration()
            .Matches(source)
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        var fileName = relativePath.Split('/')[^1];

        if (names.Length > 1)
        {
            return [$"{relativePath} declares {names.Length} top-level types ({string.Join(", ", names)}); put each in its own file."];
        }

        if (names.Length == 1 && !string.Equals(fileName, $"{names[0]}.cs", StringComparison.Ordinal))
        {
            return [$"{relativePath} declares '{names[0]}' but is not named {names[0]}.cs."];
        }

        return [];
    }

    private static bool IsBuildOutput(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin", StringComparer.Ordinal) || parts.Contains("obj", StringComparer.Ordinal);
    }

    [GeneratedRegex(
        @"^(?:(?:public|internal|file|static|sealed|abstract|partial|readonly|ref|unsafe)\s+)*(?:class|interface|enum|struct|record(?:\s+(?:class|struct))?|delegate\s+\S+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TopLevelDeclaration();
}
