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
