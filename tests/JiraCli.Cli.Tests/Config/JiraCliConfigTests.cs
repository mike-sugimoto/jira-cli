using System.Text;
using Atlassian.Common.Config;
using Atlassian.Common.Paths;
using JiraCli.Cli.Config;
using JiraCli.Domain;

namespace JiraCli.Cli.Tests.Config;

/// <summary>The I/O and edge-case matrix of Story 1.2, run end to end through the real schema and mapper.</summary>
public sealed class JiraCliConfigTests : IDisposable
{
    private const string Token = "SECRET-api+token/value==";
    private const string CloudId = "0f3b2c1a-4d5e-6f70-8192-a3b4c5d6e7f8";

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("jira-cli-config-");

    public void Dispose() => folder.Delete(recursive: true);

    private string FilePath => Path.Combine(folder.FullName, "config.json");

    private ConfigLoadResult LoadText(string json, bool bom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        File.WriteAllBytes(FilePath, bom ? [0xEF, 0xBB, 0xBF, .. bytes] : bytes);
        return JiraCliConfig.Load(UserPathLookup.FromPath(FilePath));
    }

    private static string Config(
        string site = "\"My-Site\"",
        string email = "\"me@example.com\"",
        string apiToken = $"\"{Token}\"",
        string? cloudId = null,
        string? section = """{ "allowedProjects": ["abc"] }""",
        string extraTopLevel = "")
    {
        var members = new List<string>();
        if (site.Length > 0) members.Add($"\"site\": {site}");
        if (email.Length > 0) members.Add($"\"email\": {email}");
        if (apiToken.Length > 0) members.Add($"\"apiToken\": {apiToken}");
        if (cloudId is not null) members.Add($"\"cloudId\": {cloudId}");
        if (section is not null) members.Add($"\"jira-cli\": {section}");
        if (extraTopLevel.Length > 0) members.Add(extraTopLevel);
        return "{\n  " + string.Join(",\n  ", members) + "\n}\n";
    }

    [Fact]
    public void Real_load_reads_the_fixed_user_path_for_jira_cli()
    {
        Assert.Equal("jira-cli", JiraCliConfig.AppName);

        var expected = UserPaths.ConfigFile("jira-cli");
        var actual = JiraCliConfig.ConfigPath();

        Assert.True(expected.IsFound, expected.Problem);
        Assert.Equal(expected.Path, actual.Path);
        Assert.True(Path.IsPathFullyQualified(actual.Path!));

        // Whatever the developer's config holds, a file-level failure must name that same path.
        var result = JiraCliConfig.Load();
        if (result.Failure is { Path: { } failurePath })
        {
            Assert.Equal(expected.Path, failurePath);
        }
    }

    [Fact]
    public void Valid_config_maps_to_connection_and_access_settings_with_defaults()
    {
        var result = LoadText(Config());

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("my-site", result.Connection!.Site);
        Assert.Equal("me@example.com", result.Connection.Email);
        Assert.Equal(Token, result.Connection.ApiToken);
        Assert.Null(result.Connection.CloudId);
        Assert.Equal([ProjectKey.Parse("ABC")], result.Access!.AllowedProjects);
        Assert.Equal("ABC", result.Access.AllowedProjects[0].Value);
        Assert.Equal(20, result.Access.DefaultPageSize);
        Assert.Equal(100, result.Access.MaxPageSize);
        Assert.Empty(result.Access.CustomFieldPassthrough);
    }

    [Fact]
    public void Scoped_token_config_sets_the_cloud_id()
    {
        var result = LoadText(Config(cloudId: $"\"{CloudId}\""));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(Guid.Parse(CloudId), result.Connection!.CloudId);
    }

    [Fact]
    public void Full_section_with_dedupe_comments_trailing_commas_and_bom_is_accepted()
    {
        var json = """
            // jira-cli config
            {
              "site": "site",
              "email": "me@example.com",
              "apiToken": "t", /* token */
              "confluence-cli": { "anything": [1, 2, 3] },
              "jira-cli": {
                "allowedProjects": ["abc", "ABC", "x_1",],
                "customFieldPassthrough": ["customfield_10010", "customfield_10010"],
                "defaultPageSize": 50,
                "maxPageSize": 500,
              },
            }
            """;

        var result = LoadText(json, bom: true);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["ABC", "X_1"], result.Access!.AllowedProjects.Select(key => key.Value));
        Assert.Equal(["customfield_10010"], result.Access.CustomFieldPassthrough);
        Assert.Equal(50, result.Access.DefaultPageSize);
        Assert.Equal(500, result.Access.MaxPageSize);
    }

    [Fact]
    public void Unknown_top_level_keys_are_ignored()
    {
        var result = LoadText(Config(extraTopLevel: "\"futureSetting\": true"));

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Fact]
    public void Path_lookup_failure_is_path_unavailable_without_fallback()
    {
        var result = JiraCliConfig.Load(UserPathLookup.Unavailable("getpwuid_r failed with error 2"));

        var failure = AssertFailure(result, ConfigFileFailureKind.PathUnavailable, key: null);
        Assert.Null(failure.Path);
        Assert.Contains("getpwuid_r failed with error 2", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_file_is_missing_with_the_path()
    {
        var result = JiraCliConfig.Load(UserPathLookup.FromPath(FilePath));

        var failure = AssertFailure(result, ConfigFileFailureKind.Missing, key: null);
        Assert.Equal(FilePath, failure.Path);
        Assert.Contains(FilePath, failure.Message, StringComparison.Ordinal);
        Assert.Contains(FilePath, failure.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Unreadable_file_is_unreadable_with_the_path()
    {
        File.WriteAllText(FilePath, Config());
        using var exclusive = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = JiraCliConfig.Load(UserPathLookup.FromPath(FilePath));

        var failure = AssertFailure(result, ConfigFileFailureKind.Unreadable, key: null);
        Assert.Contains(FilePath, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrong_type_is_invalid_naming_the_key_path_and_line()
    {
        var result = LoadText(Config(section: """{ "allowedProjects": ["ABC"], "defaultPageSize": "x" }"""));

        var failure = AssertFailure(result, ConfigFileFailureKind.Invalid, key: "jira-cli.defaultPageSize");
        Assert.Contains("\"jira-cli.defaultPageSize\" (line 5)", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Syntax_error_is_invalid_with_a_line()
    {
        var result = LoadText("{\n  \"site\": \"a\"\n  \"email\": \"b\"\n}");

        // A syntax error between members has no key path, only a line.
        var failure = AssertFailure(result, ConfigFileFailureKind.Invalid, key: null);
        Assert.Contains("line 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_key_is_invalid()
    {
        var result = LoadText(Config(extraTopLevel: "\"site\": \"other\""));

        AssertFailure(result, ConfigFileFailureKind.Invalid, key: "site");
    }

    [Fact]
    public void Duplicate_key_inside_the_section_is_invalid()
    {
        var result = LoadText(Config(section: """{ "allowedProjects": ["ABC"], "allowedProjects": ["XYZ"] }"""));

        AssertFailure(result, ConfigFileFailureKind.Invalid, key: "jira-cli.allowedProjects");
    }

    [Fact]
    public void Duplicate_unknown_key_inside_the_section_is_invalid()
    {
        var result = LoadText(Config(section: """{ "allowedProjects": ["ABC"], "extra": 1, "extra": 2 }"""));

        AssertFailure(result, ConfigFileFailureKind.Invalid, key: "jira-cli.<unknown key>");
    }

    [Fact]
    public void Key_names_from_the_file_are_masked_unless_jira_cli_knows_them()
    {
        // A key name is text from the file: a pasted token can end up as one.
        var result = LoadText(Config(section: $$"""{ "allowedProjects": ["ABC"], "a.{{Token}}[0]": 1, "a.{{Token}}[0]": 2 }"""));

        var failure = AssertFailure(result, ConfigFileFailureKind.Invalid, key: "jira-cli.<unknown key>");
        Assert.DoesNotContain("SECRET", failure.Message + failure.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_used_as_an_ignored_top_level_key_never_reaches_a_message()
    {
        var result = LoadText(Config(extraTopLevel: $"\"{Token}\": 1, \"{Token}\": 2"));

        Assert.DoesNotContain("SECRET", result.ToString(), StringComparison.Ordinal);
        if (result.Failure is { } failure)
        {
            Assert.DoesNotContain("SECRET", failure.Message + failure.Hint + failure.Key, StringComparison.Ordinal);
        }
    }

    public static TheoryData<string, string> BadValues => new()
    {
        { Config(site: "\"a.b\""), "site" },
        { Config(site: "\"https://a\""), "site" },
        { Config(site: ""), "site" },
        { Config(site: "\"  \""), "site" },
        { Config(email: ""), "email" },
        { Config(apiToken: ""), "apiToken" },
        { Config(apiToken: "\"\""), "apiToken" },
        { Config(apiToken: "\"token \""), "apiToken" },
        { Config(apiToken: "\" token\""), "apiToken" },
        { Config(apiToken: "\"token\\n\""), "apiToken" },
        { Config(apiToken: "\"token\\t\""), "apiToken" },
        { Config(email: "\"me@example.com \""), "email" },
        { Config(email: "\"\\r\\nme@example.com\""), "email" },
        { Config(cloudId: "\"not-a-guid\""), "cloudId" },
        { Config(cloudId: "\"00000000-0000-0000-0000-000000000000\""), "cloudId" },
        { Config(cloudId: "\"{0f3b2c1a-4d5e-6f70-8192-a3b4c5d6e7f8}\""), "cloudId" },
        { Config(section: null), "jira-cli" },
        { Config(section: "null"), "jira-cli" },
        { Config(section: "{}"), "jira-cli.allowedProjects" },
        { Config(section: """{ "allowedProjects": [] }"""), "jira-cli.allowedProjects" },
        { Config(section: """{ "allowedProjects": ["1X"] }"""), "jira-cli.allowedProjects[0]" },
        { Config(section: """{ "allowedProjects": ["ABC", null] }"""), "jira-cli.allowedProjects[1]" },
        { Config(section: """{ "allowedProjects": ["ABC"], "customFieldPassthrough": ["cf_1"] }"""), "jira-cli.customFieldPassthrough[0]" },
        { Config(section: """{ "allowedProjects": ["ABC"], "customFieldPassthrough": ["customfield_1", null] }"""), "jira-cli.customFieldPassthrough[1]" },
        { Config(section: """{ "allowedProjects": ["ABC"], "defaultPageSize": 0 }"""), "jira-cli.defaultPageSize" },
        { Config(section: """{ "allowedProjects": ["ABC"], "maxPageSize": 0 }"""), "jira-cli.maxPageSize" },
        { Config(section: """{ "allowedProjects": ["ABC"], "defaultPageSize": 101 }"""), "jira-cli.defaultPageSize" },
        { Config(section: """{ "allowedProjects": ["ABC"], "defaultPageSize": 30, "maxPageSize": 10 }"""), "jira-cli.defaultPageSize" },
        { Config(section: """{ "allowedProjects": ["ABC"], "AllowedProjects": ["XYZ"] }"""), "jira-cli.<unknown key>" },
        { Config(section: """{ "allowedProjects": ["ABC"], "zeta": 1, "alpha": 2 }"""), "jira-cli.<unknown key>" },
    };

    [Theory]
    [MemberData(nameof(BadValues))]
    public void Bad_values_are_invalid_naming_the_key(string json, string key)
    {
        var result = LoadText(json);

        var failure = AssertFailure(result, ConfigFileFailureKind.Invalid, key);
        Assert.Contains($"\"{key}\"", failure.Message, StringComparison.Ordinal);
        Assert.Contains(FilePath, failure.Hint, StringComparison.Ordinal);
    }

    /// <summary>A secret-shaped value in every field that can be invalid; it must never reach the message or hint.</summary>
    public static TheoryData<string> SecretShapedInvalidFiles => new()
    {
        Config(site: $"\"{Token}\""),
        Config(site: "\"SECRET.example\""),
        Config(site: "[\"SECRET\"]"),
        Config(email: "{ \"SECRET\": 1 }"),
        Config(apiToken: "[\"SECRET-api+token/value==\"]"),
        Config(apiToken: $"\"{Token} \""),
        Config(apiToken: $"\"{Token}\\n\""),
        Config(email: "\" SECRET@example.com\""),
        Config(cloudId: $"\"{Token}\""),
        Config(section: $$"""{ "allowedProjects": ["{{Token}}"] }"""),
        Config(section: """{ "allowedProjects": "SECRET" }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "customFieldPassthrough": ["{{Token}}"] }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "defaultPageSize": "{{Token}}" }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "maxPageSize": "{{Token}}" }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "unknown": "{{Token}}" }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "{{Token}}": 1 }"""),
        Config(section: $$"""{ "allowedProjects": ["ABC"], "{{Token}}": 1, "{{Token}}": 2 }"""),
        Config(section: """{ "allowedProjects": ["ABC"], "SECRET": 1, "SECRET": 2 }"""),
        Config(section: $$"""{{Token}}"""),
        Config(extraTopLevel: $"\"apiToken\": \"{Token}\""),
        $$"""{ "site": "a", "apiToken": "{{Token}} """,
        $$"""{ "site": "a", "apiToken": {{Token}} }""",
    };

    [Theory]
    [MemberData(nameof(SecretShapedInvalidFiles))]
    public void Values_from_the_file_never_appear_in_message_or_hint(string json)
    {
        var result = LoadText(json);

        Assert.False(result.IsSuccess);
        var failure = result.Failure!;
        Assert.Equal(ConfigFileFailureKind.Invalid, failure.Kind);
        foreach (var text in new[] { failure.Message, failure.Hint, failure.Key ?? string.Empty, result.ToString() })
        {
            Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        }
    }

    private static ConfigLoadFailure AssertFailure(ConfigLoadResult result, ConfigFileFailureKind kind, string? key)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Connection);
        Assert.Null(result.Access);
        var failure = result.Failure!;
        Assert.Equal(kind, failure.Kind);
        Assert.Equal(key, failure.Key);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.False(string.IsNullOrWhiteSpace(failure.Hint));
        return failure;
    }
}
