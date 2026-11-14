using Atlassian.Common.Config;
using Atlassian.Common.Paths;

namespace JiraCli.Cli.Config;

/// <summary>Loads jira-cli's config from its one fixed location (AD-11). Never throws for a bad config.</summary>
internal static class JiraCliConfig
{
    public const string AppName = "jira-cli";

    public static ConfigLoadResult Load() => Load(ConfigPath());

    /// <summary>The one fixed config location that <see cref="Load()"/> reads.</summary>
    internal static UserPathLookup ConfigPath() => UserPaths.ConfigFile(AppName);

    internal static ConfigLoadResult Load(UserPathLookup path)
    {
        var file = ConfigFileLoader.Load(path, ConfigJsonContext.Default.ConfigFileSchema);
        return file is { IsSuccess: true, Value: { } schema, Path: { } filePath }
            ? ConfigMapper.Map(schema, filePath)
            : ConfigMapper.FromFileFailure(file);
    }
}
