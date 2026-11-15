namespace Atlassian.Common.Transport;

/// <summary>
/// The only way out to an Atlassian site (AD-5). It can only read: the sole operation is a GET
/// of a path relative to the configured base URL, so a write request cannot be expressed.
/// </summary>
public interface IAtlassianTransport
{
    /// <summary>
    /// Sends one GET and returns the buffered 2xx response. A non-2xx status, a timeout, or a
    /// connection failure raises <see cref="TransportException"/>; caller cancellation raises
    /// <see cref="OperationCanceledException"/>; a null request or one that does not resolve under
    /// the base URL raises an <see cref="ArgumentException"/> (or subclass) before anything is sent.
    /// </summary>
    Task<TransportResponse> GetAsync(RelativeRequest request, CancellationToken cancellationToken);
}
