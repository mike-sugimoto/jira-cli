using System.Net;

namespace Atlassian.Common.Transport;

/// <summary>A successful (2xx) response whose body is already fully buffered in memory.</summary>
public sealed class TransportResponse : IDisposable
{
    public TransportResponse(HttpStatusCode statusCode, Stream body)
    {
        ArgumentNullException.ThrowIfNull(body);
        StatusCode = statusCode;
        Body = body;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>The buffered body; reading it never touches the network.</summary>
    public Stream Body { get; }

    public void Dispose() => Body.Dispose();
}
