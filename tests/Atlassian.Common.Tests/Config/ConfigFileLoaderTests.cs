using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlassian.Common.Config;
using Atlassian.Common.Paths;

namespace Atlassian.Common.Tests.Config;

public sealed class SampleConfig
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("my-app")]
    public SampleSection? Section { get; set; }
}

public sealed class SampleSection
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("items")]
    public List<int>? Items { get; set; }
}

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SampleConfig))]
internal sealed partial class SampleConfigContext : JsonSerializerContext;

public sealed class ConfigFileLoaderTests : IDisposable
{
    private const string Secret = "SECRET-tok+en/value==";

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("config-loader-");

    public void Dispose() => folder.Delete(recursive: true);

    private string FilePath => Path.Combine(folder.FullName, "config.json");

    private ConfigFileLoadResult<SampleConfig> LoadText(string json, bool bom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        File.WriteAllBytes(FilePath, bom ? [0xEF, 0xBB, 0xBF, .. bytes] : bytes);
        return ConfigFileLoader.Load(UserPathLookup.FromPath(FilePath), SampleConfigContext.Default.SampleConfig);
    }

    [Fact]
    public void Loads_a_valid_file_through_the_callers_type_info()
    {
        var result = LoadText("""{ "name": "x", "my-app": { "count": 3 } }""");

        Assert.True(result.IsSuccess);
        Assert.Equal("x", result.Value!.Name);
        Assert.Equal(3, result.Value.Section!.Count);
        Assert.Equal(FilePath, result.Path);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Tolerates_a_utf8_bom()
    {
        var result = LoadText("""{ "name": "x" }""", bom: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("x", result.Value!.Name);
    }

    [Fact]
    public void Honors_comments_and_trailing_commas_from_the_callers_options()
    {
        var result = LoadText("""
            {
              // comment
              "name": "x", /* block */
              "my-app": { "count": 1, },
            }
            """);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Unavailable_path_is_a_failure_value_with_the_problem()
    {
        var result = ConfigFileLoader.Load(UserPathLookup.Unavailable("lookup failed"), SampleConfigContext.Default.SampleConfig);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfigFileFailureKind.PathUnavailable, result.Failure);
        Assert.Null(result.Path);
        Assert.Equal("lookup failed", result.Problem);
    }

    [Fact]
    public void Missing_file_is_a_failure_value_with_the_path()
    {
        var result = ConfigFileLoader.Load(UserPathLookup.FromPath(FilePath), SampleConfigContext.Default.SampleConfig);

        Assert.Equal(ConfigFileFailureKind.Missing, result.Failure);
        Assert.Equal(FilePath, result.Path);
    }

    [Fact]
    public void Missing_folder_is_a_missing_file()
    {
        var path = Path.Combine(folder.FullName, "absent", "config.json");

        var result = ConfigFileLoader.Load(UserPathLookup.FromPath(path), SampleConfigContext.Default.SampleConfig);

        Assert.Equal(ConfigFileFailureKind.Missing, result.Failure);
        Assert.Equal(path, result.Path);
    }

    [Fact]
    public void A_directory_at_the_path_is_unreadable()
    {
        Directory.CreateDirectory(FilePath);

        var result = ConfigFileLoader.Load(UserPathLookup.FromPath(FilePath), SampleConfigContext.Default.SampleConfig);

        Assert.Equal(ConfigFileFailureKind.Unreadable, result.Failure);
        Assert.Equal(FilePath, result.Path);
    }

    [Fact]
    public void A_file_locked_by_another_handle_is_unreadable()
    {
        File.WriteAllText(FilePath, "{}");
        using var exclusive = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = ConfigFileLoader.Load(UserPathLookup.FromPath(FilePath), SampleConfigContext.Default.SampleConfig);

        Assert.Equal(ConfigFileFailureKind.Unreadable, result.Failure);
    }

    [Fact]
    public void A_file_the_user_may_not_read_is_unreadable()
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(FilePath, "{}");
            AssertUnreadableWithDenyReadAce();
        }
        else
        {
            Assert.Skip("Windows only (deny-Read ACE).");
        }
    }

    [SupportedOSPlatform("windows")]
    private void AssertUnreadableWithDenyReadAce()
    {
        var file = new FileInfo(FilePath);
        var user = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(user, FileSystemRights.Read, AccessControlType.Deny);
        var security = file.GetAccessControl();
        security.AddAccessRule(deny);
        file.SetAccessControl(security);
        try
        {
            var result = ConfigFileLoader.Load(UserPathLookup.FromPath(FilePath), SampleConfigContext.Default.SampleConfig);

            Assert.Equal(ConfigFileFailureKind.Unreadable, result.Failure);
            Assert.Equal(FilePath, result.Path);
        }
        finally
        {
            security = file.GetAccessControl();
            security.RemoveAccessRule(deny);
            file.SetAccessControl(security);
        }
    }

    [Fact]
    public void Wrong_value_type_names_the_key_path_and_line_but_not_the_value()
    {
        var result = LoadText($$"""
            {
              "name": "x",
              "my-app": {
                "count": "{{Secret}}"
              }
            }
            """);

        Assert.Equal(ConfigFileFailureKind.Invalid, result.Failure);
        Assert.Equal("my-app.count", result.KeyPath);
        Assert.Equal(4, result.Line);
        Assert.Equal(FilePath, result.Path);
        AssertNoSecret(result);
    }

    [Fact]
    public void Array_item_of_wrong_type_names_the_index()
    {
        var result = LoadText("""{ "my-app": { "items": [1, "two"] } }""");

        Assert.Equal(ConfigFileFailureKind.Invalid, result.Failure);
        Assert.Equal("my-app.items[1]", result.KeyPath);
        Assert.Equal(1, result.Line);
    }

    [Fact]
    public void Syntax_error_is_invalid_with_a_line_and_never_the_text()
    {
        var result = LoadText($$"""
            {
              "name": "{{Secret}}
            }
            """);

        Assert.Equal(ConfigFileFailureKind.Invalid, result.Failure);
        Assert.NotNull(result.Line);
        AssertNoSecret(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    [InlineData("{ \"name\": \"x\" } trailing")]
    public void Non_object_or_empty_files_are_invalid(string json)
    {
        var result = LoadText(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConfigFileFailureKind.Invalid, result.Failure);
    }

    [Fact]
    public void Duplicate_keys_are_invalid_when_the_callers_options_reject_them()
    {
        var result = LoadText($$"""{ "name": "a", "name": "{{Secret}}" }""");

        Assert.Equal(ConfigFileFailureKind.Invalid, result.Failure);
        Assert.Equal("name", result.KeyPath);
        AssertNoSecret(result);
    }

    [Fact]
    public void Loader_never_creates_the_file_or_its_folder()
    {
        var path = Path.Combine(folder.FullName, "absent", "config.json");

        ConfigFileLoader.Load(UserPathLookup.FromPath(path), SampleConfigContext.Default.SampleConfig);

        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    private static void AssertNoSecret(ConfigFileLoadResult<SampleConfig> result)
    {
        string?[] texts = [result.KeyPath, result.Path, result.Problem];
        Assert.All(texts, text => Assert.DoesNotContain("SECRET", text ?? string.Empty, StringComparison.Ordinal));
    }
}
