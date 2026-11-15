using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraCli.Cli.Config;

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
