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
