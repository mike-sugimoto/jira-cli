using JiraCli.Cli.Config;

namespace JiraCli.Cli.Tests.Config;

public sealed class ConfigKeysTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("site", "site")]
    [InlineData("jira-cli.allowedProjects[0]", "jira-cli.allowedProjects[0]")]
    [InlineData("jira-cli.defaultPageSize", "jira-cli.defaultPageSize")]
    [InlineData("jira-cli.extra", "jira-cli.<unknown key>")]
    [InlineData("jira-cli.AllowedProjects", "jira-cli.<unknown key>")]
    [InlineData("SECRET", "<unknown key>")]
    [InlineData("jira-cli.SECRET.allowedProjects", "jira-cli.<unknown key>")]
    [InlineData("jira-cli.allowedProjects[SECRET]", "jira-cli.<unknown key>")]
    [InlineData("jira-cli.allowedProjects[0]SECRET", "jira-cli.<unknown key>")]
    [InlineData("[3]", "<unknown key>")]
    public void Only_names_jira_cli_knows_are_kept(string? keyPath, string? expected)
    {
        Assert.Equal(expected, ConfigKeys.MaskUnknownNames(keyPath));
    }
}
