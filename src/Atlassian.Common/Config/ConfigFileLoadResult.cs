using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Atlassian.Common.Paths;

namespace Atlassian.Common.Config;

/// <summary>
/// The outcome of <see cref="ConfigFileLoader.Load{T}"/>. A failure carries only the cause, the
/// file path, and for invalid JSON the key path and line; never text from the file.
/// </summary>
public sealed class ConfigFileLoadResult<T>
    where T : class
{
    private ConfigFileLoadResult(T? value, ConfigFileFailureKind? failure, string? path, string? keyPath, int? line, string? problem)
    {
        Value = value;
        Failure = failure;
        Path = path;
        KeyPath = keyPath;
        Line = line;
        Problem = problem;
    }

    public bool IsSuccess => Value is not null;

    /// <summary>The deserialized schema object on success.</summary>
    public T? Value { get; }

    public ConfigFileFailureKind? Failure { get; }

    /// <summary>The file path, or <see langword="null"/> when the path itself was unavailable.</summary>
    public string? Path { get; }

    /// <summary>For <see cref="ConfigFileFailureKind.Invalid"/>: the offending key path, such as <c>section.items[0]</c>, if known.</summary>
    public string? KeyPath { get; }

    /// <summary>For <see cref="ConfigFileFailureKind.Invalid"/>: the 1-based line, if known.</summary>
    public int? Line { get; }

    /// <summary>For <see cref="ConfigFileFailureKind.PathUnavailable"/>: why the path lookup failed.</summary>
    public string? Problem { get; }

    internal static ConfigFileLoadResult<T> Success(T value, string path) => new(value, null, path, null, null, null);

    internal static ConfigFileLoadResult<T> Failed(
        ConfigFileFailureKind failure, string? path, string? keyPath = null, int? line = null, string? problem = null) =>
        new(null, failure, path, keyPath, line, problem);
}
