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

/// <summary>
/// Reads one JSON config file through a caller-supplied source-generated <see cref="JsonTypeInfo{T}"/>.
/// The caller owns the schema and the serializer options (comments, trailing commas, duplicates).
/// The loader only reads; it never creates or writes the file.
/// </summary>
public static class ConfigFileLoader
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static ConfigFileLoadResult<T> Load<T>(UserPathLookup path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (path.Path is not { } filePath)
        {
            return ConfigFileLoadResult<T>.Failed(ConfigFileFailureKind.PathUnavailable, null, problem: path.Problem);
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return ConfigFileLoadResult<T>.Failed(ConfigFileFailureKind.Missing, filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConfigFileLoadResult<T>.Failed(ConfigFileFailureKind.Unreadable, filePath);
        }

        ReadOnlySpan<byte> json = bytes;
        if (json.StartsWith(Utf8Bom))
        {
            json = json[Utf8Bom.Length..];
        }

        try
        {
            var value = JsonSerializer.Deserialize(json, typeInfo);
            return value is null
                ? ConfigFileLoadResult<T>.Failed(ConfigFileFailureKind.Invalid, filePath)
                : ConfigFileLoadResult<T>.Success(value, filePath);
        }
        catch (JsonException exception)
        {
            // Never use exception.Message: it can quote file content, such as a token.
            return ConfigFileLoadResult<T>.Failed(
                ConfigFileFailureKind.Invalid,
                filePath,
                JsonKeyPath.Readable(exception.Path),
                exception.LineNumber is { } line ? checked((int)line + 1) : null);
        }
    }
}
