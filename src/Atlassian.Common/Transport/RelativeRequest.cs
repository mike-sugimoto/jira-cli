namespace Atlassian.Common.Transport;

/// <summary>
/// A request relative to the configured base URL: raw (unescaped) path segments, query
/// name/value pairs, and the response kind. It deliberately has no URL and no HTTP method;
/// the transport escapes every part and always sends GET.
/// </summary>
public sealed class RelativeRequest
{
    public RelativeRequest(
        IReadOnlyList<string> segments,
        IReadOnlyList<KeyValuePair<string, string>> query,
        ResponseKind kind)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(query);

        if (segments.Count == 0)
        {
            throw new ArgumentException("At least one path segment is required.", nameof(segments));
        }

        foreach (var segment in segments)
        {
            if (segment is null or "" or "." or "..")
            {
                throw new ArgumentException("Path segments must not be null, empty, '.', or '..'.", nameof(segments));
            }
        }

        foreach (var pair in query)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null)
            {
                throw new ArgumentException("Query names must not be empty and values must not be null.", nameof(query));
            }
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Segments = [.. segments];
        Query = [.. query];
        Kind = kind;
    }

    /// <summary>Unescaped path segments, appended in order to the base URL.</summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>Unescaped query name/value pairs, in order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Query { get; }

    public ResponseKind Kind { get; }
}
