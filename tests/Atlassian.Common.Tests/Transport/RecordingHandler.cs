using System.Net;

namespace Atlassian.Common.Tests.Transport;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> that records every request and answers from a
/// callback. It never opens a socket, so no test reaches a real host.
/// </summary>
internal sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public RecordingHandler()
        : this(HttpStatusCode.OK, "{}")
    {
    }

    public RecordingHandler(HttpStatusCode status, string body)
        : this((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }))
    {
    }

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!.AbsoluteUri,
            request.Headers.Authorization?.ToString(),
            request.Headers.Accept.ToString(),
            request.Content is not null));

        return await respond(request, cancellationToken);
    }
}
