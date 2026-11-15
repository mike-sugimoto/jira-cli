using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Atlassian.Common.Paths;

namespace Atlassian.Common.Config;

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
