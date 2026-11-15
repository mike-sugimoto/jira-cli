using System.Net;

namespace Atlassian.Common.Tests.Transport;

/// <summary>
/// Content whose body never finishes arriving until the read is cancelled. The optional callback
/// runs after the first bytes are written, so a test can act mid-body.
/// </summary>
internal sealed class StallingContent(Func<Task>? afterFirstBytes = null) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
    {
        await stream.WriteAsync("{\"partial\":"u8.ToArray(), cancellationToken);
        if (afterFirstBytes is not null)
        {
            await afterFirstBytes();
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
