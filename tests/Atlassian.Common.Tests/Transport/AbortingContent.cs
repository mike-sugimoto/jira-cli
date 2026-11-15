using System.Net;

namespace Atlassian.Common.Tests.Transport;

/// <summary>
/// Content that stalls until its read is cancelled, then fails like an aborted socket
/// (<see cref="IOException"/>, which <see cref="HttpContent"/> wraps in a status-less
/// <see cref="HttpRequestException"/>) instead of reporting cancellation.
/// </summary>
internal sealed class AbortingContent : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
    {
        await stream.WriteAsync("{"u8.ToArray(), CancellationToken.None);
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Fall through and fail as a broken connection would.
        }

        throw new IOException("The connection was aborted.");
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
