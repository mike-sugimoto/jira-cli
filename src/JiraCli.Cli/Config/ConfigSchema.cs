using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraCli.Cli.Config;

/// <summary>
/// The config file as written. Every member is nullable so <see cref="ConfigMapper"/> can name a
/// missing key instead of the serializer failing without one. Unknown top-level keys are ignored
/// (room for other tools' sections); unknown keys in the <c>jira-cli</c> section are collected and rejected.
/// </summary>
internal sealed class ConfigFileSchema
{
    [JsonPropertyName("site")]
    public string? Site { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("apiToken")]
    public string? ApiToken { get; set; }

    [JsonPropertyName("cloudId")]
    public string? CloudId { get; set; }

    [JsonPropertyName(ConfigKeys.Section)]
    public JiraCliSectionSchema? JiraCli { get; set; }
}

/// <summary>The <c>jira-cli</c> section.</summary>
internal sealed class JiraCliSectionSchema
{
    [JsonPropertyName("allowedProjects")]
    public List<string?>? AllowedProjects { get; set; }

    [JsonPropertyName("customFieldPassthrough")]
    public List<string?>? CustomFieldPassthrough { get; set; }

    [JsonPropertyName("defaultPageSize")]
    public int? DefaultPageSize { get; set; }

    [JsonPropertyName("maxPageSize")]
    public int? MaxPageSize { get; set; }

    /// <summary>Keys this version does not know. Their values are never read or reported.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownKeys { get; set; }
}

/// <summary>
/// Source-generated metadata for the config file: comments and trailing commas are allowed,
/// duplicate keys are rejected, and property names match exactly.
/// </summary>
[JsonSourceGenerationOptions(
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ConfigFileSchema))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
