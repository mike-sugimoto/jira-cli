using Atlassian.Common.Paths;

namespace Atlassian.Common.Tests.Paths;

[CollectionDefinition(nameof(EnvironmentMutation), DisableParallelization = true)]
public sealed class EnvironmentMutation;

/// <summary>
/// AD-11: a decoy config under every variable that usually points at a user folder must not
/// redirect the config path. These tests change the process environment, so they run alone.
/// </summary>
[Collection(nameof(EnvironmentMutation))]
public sealed class HostileEnvironmentTests : IDisposable
{
    private static readonly string[] HostileVariables =
    [
        "HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "XDG_STATE_HOME", "XDG_CACHE_HOME", "XDG_RUNTIME_DIR",
        "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA",
    ];

    private readonly Dictionary<string, string?> saved = HostileVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
    private readonly DirectoryInfo decoy = Directory.CreateTempSubdirectory("jira-cli-decoy-");

    public void Dispose()
    {
        foreach (var (name, value) in saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        decoy.Delete(recursive: true);
    }

    [Fact]
    public void Windows_path_ignores_hostile_environment()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows only.");
        }

        AssertDecoyIgnored();
    }

    [Fact]
    public void Linux_path_ignores_hostile_environment()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux only.");
        }

        AssertDecoyIgnored();
    }

    private void AssertDecoyIgnored()
    {
        var before = UserPaths.ConfigFile("jira-cli");
        Assert.True(before.IsFound, before.Problem);

        PlantDecoys();
        var root = decoy.FullName;
        foreach (var name in HostileVariables)
        {
            Environment.SetEnvironmentVariable(name, name == "HOMEDRIVE" ? Path.GetPathRoot(root)!.TrimEnd('\\', '/') : root);
        }

        Environment.SetEnvironmentVariable("HOMEPATH", root[(Path.GetPathRoot(root)!.TrimEnd('\\', '/').Length)..]);

        var after = UserPaths.ConfigFile("jira-cli");

        Assert.True(after.IsFound, after.Problem);
        Assert.Equal(before.Path, after.Path);
        Assert.False(
            after.Path!.StartsWith(root, StringComparison.OrdinalIgnoreCase),
            "The config path was redirected into the decoy folder.");
    }

    private void PlantDecoys()
    {
        string[] relativeConfigs =
        [
            Path.Combine("jira-cli", "config.json"),
            Path.Combine(".config", "jira-cli", "config.json"),
            Path.Combine("AppData", "Roaming", "jira-cli", "config.json"),
        ];

        foreach (var relative in relativeConfigs)
        {
            var file = new FileInfo(Path.Combine(decoy.FullName, relative));
            file.Directory!.Create();
            File.WriteAllText(file.FullName, """{ "site": "decoy", "jira-cli": { "allowedProjects": ["DECOY"] } }""");
        }
    }
}
