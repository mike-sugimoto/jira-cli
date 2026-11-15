using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraCli.Cli.Config;

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
