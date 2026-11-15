using System.Net;
using System.Text;
using Atlassian.Common.Transport;

namespace JiraCli.LiveCheck.Tests;

/// <summary>
/// A fake transport that records every request and answers from a script keyed by the joined
/// path. Unscripted paths answer 200 with <c>{}</c>. It never touches the network.
/// </summary>
internal sealed class RecordingTransport : IAtlassianTransport
{
    private readonly Dictionary<string, Func<TransportResponse>> script = new(StringComparer.Ordinal);

    public List<RelativeRequest> Requests { get; } = [];

    public RecordingTransport Answer(string path, HttpStatusCode status, string body)
    {
        script[path] = () => new TransportResponse(status, new MemoryStream(Encoding.UTF8.GetBytes(body)));
        return this;
    }

    public RecordingTransport Fail(string path, TransportException exception)
    {
        script[path] = () => throw exception;
        return this;
    }

    public Task<TransportResponse> GetAsync(RelativeRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var path = string.Join('/', request.Segments);
        var response = script.TryGetValue(path, out var answer)
            ? answer()
            : new TransportResponse(HttpStatusCode.OK, new MemoryStream(Encoding.UTF8.GetBytes("{}")));
        return Task.FromResult(response);
    }
}
