using System.Net;
using System.Text;

namespace Atlassian.Common.Transport;

/// <summary>
/// A failed transport call. The message names only the kind and status; the response body
/// is kept separately (capped) and never placed in <see cref="Exception.Message"/>.
/// Category and exit-code mapping happen elsewhere (Story 1.4).
/// </summary>
public sealed class TransportException : Exception
{
    /// <summary>The most response body kept, in UTF-8 bytes.</summary>
    public const int MaxResponseBodyBytes = 8 * 1024;

    public TransportException(
        TransportFailureKind kind,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(BuildMessage(kind, statusCode), innerException)
    {
        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter));
        }

        Kind = kind;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        ResponseBody = responseBody is null ? null : Cap(responseBody);
    }

    public TransportFailureKind Kind { get; }

    /// <summary>Set only for <see cref="TransportFailureKind.Status"/>.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>From the <c>Retry-After</c> header, when present; never negative.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The response body text, at most <see cref="MaxResponseBodyBytes"/> UTF-8 bytes.</summary>
    public string? ResponseBody { get; }

    private static string BuildMessage(TransportFailureKind kind, HttpStatusCode? statusCode) => kind switch
    {
        TransportFailureKind.Status when statusCode is { } status =>
            $"The server answered with HTTP status {(int)status}.",
        TransportFailureKind.Status =>
            throw new ArgumentException("A status failure needs a status code.", nameof(statusCode)),
        TransportFailureKind.Timeout or TransportFailureKind.Connection when statusCode is not null =>
            throw new ArgumentException("Only a status failure carries a status code.", nameof(statusCode)),
        TransportFailureKind.Timeout => "No complete response arrived within the timeout.",
        TransportFailureKind.Connection => "The connection to the server failed.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Keeps whole characters up to <see cref="MaxResponseBodyBytes"/> UTF-8 bytes.</summary>
    private static string Cap(string body)
    {
        if (Encoding.UTF8.GetByteCount(body) <= MaxResponseBodyBytes)
        {
            return body;
        }

        var bytes = 0;
        var length = 0;
        foreach (var rune in body.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxResponseBodyBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            length += rune.Utf16SequenceLength;
        }

        return body[..length];
    }
}
