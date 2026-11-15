using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Atlassian.Common.Config;

namespace Atlassian.Common.Transport;

/// <summary>
/// The only <c>HttpClient</c> user (AD-5, AD-12, AD-19). Always GET; escapes every segment and
/// query part; adds the Basic credentials only after checking the final URI sits under the base
/// URL; never follows redirects; no retries; one total timeout covering send and body read.
/// </summary>
internal sealed class HttpAtlassianTransport : IAtlassianTransport
{
    /// <summary>Total time for a JSON request, from send to the last body byte.</summary>
    internal static readonly TimeSpan JsonTimeout = TimeSpan.FromSeconds(30);

    private readonly string _basicParameter;
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly IRequestUriBuilder _uriBuilder;

    internal HttpAtlassianTransport(
        ConnectionSettings settings,
        string product,
        HttpMessageHandler handler,
        TimeSpan timeout,
        TimeProvider timeProvider,
        IRequestUriBuilder? uriBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var baseUri = AtlassianEndpoint.For(settings, product);
        _uriBuilder = uriBuilder ?? new RequestUriBuilder(baseUri);
        _basicParameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Email}:{settings.ApiToken}"));
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        Handler = handler;
        _timeout = timeout;
        _timeProvider = timeProvider;
    }

    /// <summary>The handler this transport sends through; exposed for wiring tests only.</summary>
    internal HttpMessageHandler Handler { get; }

    /// <summary>The total request timeout; exposed for wiring tests only.</summary>
    internal TimeSpan RequestTimeout => _timeout;

    /// <summary>Redirects off; TLS validation and proxy left at .NET defaults.</summary>
    internal static SocketsHttpHandler CreateProductionHandler() => new() { AllowAutoRedirect = false };

    public async Task<TransportResponse> GetAsync(RelativeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The builder refuses a URI outside the base, so credentials below go only to the base URL.
        var uri = _uriBuilder.Build(request);
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", _basicParameter);

        using var timeoutSource = new CancellationTokenSource(Timeout.InfiniteTimeSpan, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        timeoutSource.CancelAfter(_timeout);

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (Translate(exception, cancellationToken, timeoutSource) is { } translated)
        {
            throw translated;
        }

        using (response)
        {
            try
            {
                await response.Content.LoadIntoBufferAsync(linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (Translate(exception, cancellationToken, timeoutSource) is { } translated)
            {
                throw translated;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new TransportResponse(response.StatusCode, new MemoryStream(bytes, writable: false));
            }

            var body = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, TransportException.MaxResponseBodyBytes + 4));
            throw new TransportException(TransportFailureKind.Status, response.StatusCode, RetryAfter(response), body);
        }
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - _timeProvider.GetUtcNow();
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    /// <summary>
    /// Maps a send or body-read failure. Caller cancellation always surfaces as
    /// <see cref="OperationCanceledException"/>, never as a timeout. Returns null to let the
    /// original exception propagate unchanged.
    /// </summary>
    private static Exception? Translate(Exception exception, CancellationToken caller, CancellationTokenSource timeout)
    {
        if (caller.IsCancellationRequested)
        {
            return new OperationCanceledException("The caller cancelled the request.", exception, caller);
        }

        if (timeout.IsCancellationRequested && exception is OperationCanceledException or HttpRequestException { StatusCode: null })
        {
            return new TransportException(TransportFailureKind.Timeout, innerException: exception);
        }

        return exception is HttpRequestException { StatusCode: null }
            ? new TransportException(TransportFailureKind.Connection, innerException: exception)
            : null;
    }
}
