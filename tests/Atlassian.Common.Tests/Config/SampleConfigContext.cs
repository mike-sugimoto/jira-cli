using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlassian.Common.Config;
using Atlassian.Common.Paths;

namespace Atlassian.Common.Tests.Config;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SampleConfig))]
internal sealed partial class SampleConfigContext : JsonSerializerContext;
