using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Atlassian.Common.Paths;

namespace Atlassian.Common.Config;

/// <summary>Why a config file could not be loaded.</summary>
public enum ConfigFileFailureKind
{
    /// <summary>The OS could not tell where the file lives.</summary>
    PathUnavailable,

    /// <summary>No file at the path.</summary>
    Missing,

    /// <summary>The file exists but could not be read.</summary>
    Unreadable,

    /// <summary>The file is not valid JSON for the schema.</summary>
    Invalid,
}
