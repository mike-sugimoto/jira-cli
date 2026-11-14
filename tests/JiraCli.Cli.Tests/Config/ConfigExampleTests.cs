using Atlassian.Common.Paths;
using JiraCli.Cli.Config;

namespace JiraCli.Cli.Tests.Config;

/// <summary>
/// <c>docs/config.example.json</c> is the template people copy to the fixed config location.
/// It must load through the real schema and mapper, so it cannot drift from what jira-cli accepts.
/// </summary>
public sealed class ConfigExampleTests : IDisposable
{
    private const string CommentedCloudId = "// \"cloudId\":";

    private static readonly string ExamplePath = Path.Combine(AppContext.BaseDirectory, "config.example.json");

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("jira-cli-example-");

    public void Dispose() => folder.Delete(recursive: true);

    [Fact]
    public void Example_loads_as_a_classic_token_config_with_the_defaults()
    {
        var result = JiraCliConfig.Load(UserPathLookup.FromPath(ExamplePath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Null(result.Connection!.CloudId);
        Assert.NotEmpty(result.Access!.AllowedProjects);
        Assert.Equal(20, result.Access.DefaultPageSize);
        Assert.Equal(100, result.Access.MaxPageSize);
    }

    [Fact]
    public void Example_loads_as_a_scoped_token_config_when_the_cloud_id_line_is_uncommented()
    {
        var text = File.ReadAllText(ExamplePath);
        Assert.Contains(CommentedCloudId, text, StringComparison.Ordinal);

        var scoped = Path.Combine(folder.FullName, "config.json");
        File.WriteAllText(scoped, text.Replace(CommentedCloudId, "\"cloudId\":", StringComparison.Ordinal));

        var result = JiraCliConfig.Load(UserPathLookup.FromPath(scoped));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.NotNull(result.Connection!.CloudId);
    }
}
