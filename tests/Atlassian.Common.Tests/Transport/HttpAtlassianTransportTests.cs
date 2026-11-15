using System.Net;
using System.Text;
using Atlassian.Common.Config;
using Atlassian.Common.Transport;

namespace Atlassian.Common.Tests.Transport;

public sealed class HttpAtlassianTransportTests
{
    private const string Email = "me@example.com";
    private const string Token = "SECRET-api+token/==";
    private static readonly Guid CloudId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly string ExpectedAuthorization =
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"));

    private static readonly string HeaderValue = ExpectedAuthorization["Basic ".Length..];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConnectionSettings Classic => new("acme", Email, Token);

    private static ConnectionSettings Scoped => new("acme", Email, Token, CloudId);

    private static HttpAtlassianTransport Create(
        HttpMessageHandler handler,
        ConnectionSettings? settings = null,
        TimeSpan? timeout = null,
        TimeProvider? timeProvider = null) =>
        new(settings ?? Classic, "jira", handler, timeout ?? HttpAtlassianTransport.JsonTimeout, timeProvider ?? TimeProvider.System);

    private static RelativeRequest Myself() => new(["rest", "api", "3", "myself"], [], ResponseKind.Json);

    [Fact]
    public async Task Classic_request_is_a_GET_to_the_site_with_auth_and_accept()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");

        using var response = await Create(handler).GetAsync(Myself(), Ct);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://acme.atlassian.net/rest/api/3/myself", sent.AbsoluteUri);
        Assert.Equal(ExpectedAuthorization, sent.Authorization);
        Assert.Equal("application/json", sent.Accept);
        Assert.False(sent.HasContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"ok\":true}", await new StreamReader(response.Body).ReadToEndAsync(Ct));
    }

    [Fact]
    public async Task Scoped_request_goes_through_the_gateway()
    {
        var handler = new RecordingHandler();

        using var response = await Create(handler, Scoped).GetAsync(Myself(), Ct);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555/rest/api/3/myself", sent.AbsoluteUri);
        Assert.Equal(ExpectedAuthorization, sent.Authorization);
    }

    [Fact]
    public async Task Segments_and_query_are_percent_encoded_and_sent_exactly_so()
    {
        var handler = new RecordingHandler();
        var request = new RelativeRequest(["rest", "a/b c"], [new("jql", "a=b&c"), new("x y", "")], ResponseKind.Json);

        using var response = await Create(handler).GetAsync(request, Ct);

        Assert.Equal("https://acme.atlassian.net/rest/a%2Fb%20c?jql=a%3Db%26c&x%20y=", Assert.Single(handler.Requests).AbsoluteUri);
    }

    [Theory]
    [InlineData("..%2F..")]
    [InlineData("%2e%2e")]
    [InlineData("@evil.example")]
    [InlineData("//evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("?x=1")]
    [InlineData("#frag")]
    [InlineData("...")]
    public async Task Hostile_segments_stay_under_the_base_url(string segment)
    {
        var handler = new RecordingHandler();

        using var response = await Create(handler, Scoped).GetAsync(new RelativeRequest([segment, "x"], [], ResponseKind.Json), Ct);

        var sent = new Uri(Assert.Single(handler.Requests).AbsoluteUri);
        Assert.Equal("api.atlassian.com", sent.Host);
        Assert.StartsWith("/ex/jira/11111111-2222-3333-4444-555555555555/", sent.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("", sent.Query);
        Assert.Equal("", sent.Fragment);
        Assert.EndsWith("/x", sent.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_is_set_on_every_request()
    {
        var handler = new RecordingHandler();
        var transport = Create(handler);

        for (var i = 0; i < 3; i++)
        {
            using var response = await transport.GetAsync(Myself(), Ct);
        }

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, sent => Assert.Equal(ExpectedAuthorization, sent.Authorization));
    }

    [Fact]
    public async Task Redirect_is_not_followed()
    {
        var handler = new RecordingHandler((_, _) =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found) { Content = new StringContent("") };
            redirect.Headers.Location = new Uri("https://evil.example/steal");
            return Task.FromResult(redirect);
        });

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        Assert.Single(handler.Requests);
        Assert.Equal(TransportFailureKind.Status, exception.Kind);
        Assert.Equal(HttpStatusCode.Found, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Non_success_status_raises_with_status_and_body(HttpStatusCode status)
    {
        var handler = new RecordingHandler(status, "{\"errorMessages\":[\"nope\"]}");

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        Assert.Equal(TransportFailureKind.Status, exception.Kind);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("{\"errorMessages\":[\"nope\"]}", exception.ResponseBody);
        Assert.Null(exception.RetryAfter);
        Assert.DoesNotContain("nope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Error_body_is_capped_at_8_KiB()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, new string('x', 20_000));

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        Assert.Equal(TransportException.MaxResponseBodyBytes, exception.ResponseBody!.Length);
        Assert.Equal(8 * 1024, TransportException.MaxResponseBodyBytes);
    }

    [Theory]
    [InlineData("x", "あ")]
    [InlineData("xy", "\U0001F600")]
    [InlineData("", "\U0001F600")]
    public async Task Multi_byte_error_body_is_capped_on_a_character_boundary(string prefix, string character)
    {
        var body = prefix + string.Concat(Enumerable.Repeat(character, 3_000));
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, body);

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        AssertCappedOnBoundary(body, exception.ResponseBody!);
    }

    /// <summary>The cap keeps a prefix of whole characters, at most 8 KiB of UTF-8, with nothing replaced or split.</summary>
    internal static void AssertCappedOnBoundary(string original, string capped)
    {
        Assert.InRange(Encoding.UTF8.GetByteCount(capped), TransportException.MaxResponseBodyBytes - 3, TransportException.MaxResponseBodyBytes);
        Assert.StartsWith(capped, original, StringComparison.Ordinal);
        Assert.DoesNotContain('�', capped);
        Assert.False(char.IsHighSurrogate(capped[^1]));
    }

    [Fact]
    public async Task Rate_limit_with_delta_seconds_carries_retry_after()
    {
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("") };
            response.Headers.TryAddWithoutValidation("Retry-After", "7");
            return Task.FromResult(response);
        });

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
    }

    [Fact]
    public async Task Rate_limit_with_http_date_carries_date_minus_now()
    {
        var now = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("") };
            response.Headers.TryAddWithoutValidation("Retry-After", now.AddSeconds(90).ToString("R"));
            return Task.FromResult(response);
        });

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeProvider: new FixedTimeProvider(now)).GetAsync(Myself(), Ct));

        Assert.Equal(TimeSpan.FromSeconds(90), exception.RetryAfter);
    }

    [Fact]
    public async Task Rate_limit_with_a_past_http_date_retries_now()
    {
        var now = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("") };
            response.Headers.TryAddWithoutValidation("Retry-After", now.AddSeconds(-30).ToString("R"));
            return Task.FromResult(response);
        });

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeProvider: new FixedTimeProvider(now)).GetAsync(Myself(), Ct));

        Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
    }

    [Fact]
    public void Json_timeout_is_30_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), HttpAtlassianTransport.JsonTimeout);
    }

    [Fact]
    public async Task No_response_within_the_timeout_raises_timeout()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeout: TimeSpan.FromMilliseconds(100)).GetAsync(Myself(), Ct));

        Assert.Equal(TransportFailureKind.Timeout, exception.Kind);
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public async Task Timeout_covers_the_body_read()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StallingContent() }));

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeout: TimeSpan.FromMilliseconds(100)).GetAsync(Myself(), Ct));

        Assert.Equal(TransportFailureKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_cancellation_not_timeout()
    {
        using var caller = new CancellationTokenSource();
        var handler = new RecordingHandler(async (_, token) =>
        {
            await caller.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exception = await Record.ExceptionAsync(() => Create(handler).GetAsync(Myself(), caller.Token));

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(caller.Token, cancelled.CancellationToken);
    }

    [Fact]
    public async Task Caller_cancellation_during_the_body_read_propagates_as_cancellation()
    {
        using var caller = new CancellationTokenSource();
        var bodyStarted = false;
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StallingContent(async () =>
            {
                bodyStarted = true;
                await caller.CancelAsync();
            }),
        }));

        var exception = await Record.ExceptionAsync(() => Create(handler).GetAsync(Myself(), caller.Token));

        Assert.True(bodyStarted);
        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(caller.Token, cancelled.CancellationToken);
    }

    [Fact]
    public async Task Caller_cancellation_reported_as_a_request_failure_still_propagates_as_cancellation()
    {
        using var caller = new CancellationTokenSource();
        var handler = new RecordingHandler(async (_, _) =>
        {
            await caller.CancelAsync();
            throw new HttpRequestException("aborted");
        });

        var exception = await Record.ExceptionAsync(() => Create(handler).GetAsync(Myself(), caller.Token));

        Assert.IsNotType<TransportException>(exception);
        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(caller.Token, cancelled.CancellationToken);
    }

    [Fact]
    // HttpClient.SendAsync itself turns a handler failure into TaskCanceledException once its token
    // is cancelled, so a status-less HttpRequestException after the timeout can only come from the
    // body read, where HttpContent wraps the stream's IOException.
    public async Task Timeout_reported_as_a_request_failure_without_status_raises_timeout()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new AbortingContent() }));

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeout: TimeSpan.FromMilliseconds(100)).GetAsync(Myself(), Ct));

        Assert.Equal(TransportFailureKind.Timeout, exception.Kind);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Connection_failure_without_status_raises_connection_with_inner()
    {
        var inner = new HttpRequestException("No such host is known.");
        var handler = new RecordingHandler((_, _) => throw inner);

        var exception = await Assert.ThrowsAsync<TransportException>(() => Create(handler).GetAsync(Myself(), Ct));

        Assert.Equal(TransportFailureKind.Connection, exception.Kind);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.StatusCode);
    }

    public static TheoryData<string> FailureScenarios => new() { "status", "rateLimited", "timeout", "connection", "redirect" };

    [Theory]
    [MemberData(nameof(FailureScenarios))]
    public async Task No_exception_message_or_ToString_contains_the_token(string scenario)
    {
        var handler = scenario switch
        {
            "status" => new RecordingHandler(HttpStatusCode.Unauthorized, "bad"),
            "rateLimited" => new RecordingHandler(HttpStatusCode.TooManyRequests, "slow"),
            "redirect" => new RecordingHandler(HttpStatusCode.Found, ""),
            "timeout" => new RecordingHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            _ => new RecordingHandler((_, _) => throw new HttpRequestException("refused")),
        };

        var exception = await Assert.ThrowsAsync<TransportException>(
            () => Create(handler, timeout: TimeSpan.FromMilliseconds(100)).GetAsync(Myself(), Ct));

        foreach (var text in new[] { exception.Message, exception.ToString() })
        {
            Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain(HeaderValue, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Production_handler_does_not_follow_redirects_and_keeps_tls_and_proxy_defaults()
    {
        using var handler = HttpAtlassianTransport.CreateProductionHandler();
        using var defaults = new SocketsHttpHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.Equal(defaults.SslOptions.EnabledSslProtocols, handler.SslOptions.EnabledSslProtocols);
        Assert.Equal(defaults.SslOptions.CertificateRevocationCheckMode, handler.SslOptions.CertificateRevocationCheckMode);
        Assert.True(handler.UseProxy);
        Assert.Null(handler.Proxy);
    }

    [Fact]
    public void Public_factory_returns_a_transport_without_sending_anything()
    {
        Assert.IsAssignableFrom<IAtlassianTransport>(AtlassianTransport.Create(Classic, "jira"));
        Assert.IsAssignableFrom<IAtlassianTransport>(AtlassianTransport.Create(Scoped, "jira", TimeProvider.System));
    }

    [Fact]
    public void Public_factory_wires_the_production_handler_and_the_json_timeout()
    {
        var transport = Assert.IsType<HttpAtlassianTransport>(AtlassianTransport.Create(Classic, "jira"));

        var handler = Assert.IsType<SocketsHttpHandler>(transport.Handler);
        Assert.False(handler.AllowAutoRedirect);
        Assert.Equal(HttpAtlassianTransport.JsonTimeout, transport.RequestTimeout);
    }

    [Fact]
    public void Public_factory_refuses_an_invalid_product()
    {
        Assert.ThrowsAny<ArgumentException>(() => AtlassianTransport.Create(Classic, "Not/A/Segment"));
    }

    [Fact]
    public void Public_factory_refuses_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => AtlassianTransport.Create(null!, "jira"));
        Assert.Throws<ArgumentNullException>(() => AtlassianTransport.Create(Classic, null!));
    }

    [Fact]
    public async Task Null_request_is_refused_without_sending()
    {
        var handler = new RecordingHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => Create(handler).GetAsync(null!, Ct));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_injected_builder_decides_the_uri_that_is_sent()
    {
        var handler = new RecordingHandler();
        var builder = new FixedUriBuilder("https://acme.atlassian.net/rest/api/3/project/ABC");
        var transport = new HttpAtlassianTransport(Classic, "jira", handler, HttpAtlassianTransport.JsonTimeout, TimeProvider.System, builder);

        using var response = await transport.GetAsync(Myself(), Ct);

        Assert.Equal(["rest", "api", "3", "myself"], Assert.Single(builder.Requests).Segments);
        Assert.Equal("https://acme.atlassian.net/rest/api/3/project/ABC", Assert.Single(handler.Requests).AbsoluteUri);
        Assert.Equal(ExpectedAuthorization, handler.Requests[0].Authorization);
    }

    [Fact]
    public async Task A_builder_refusal_sends_nothing()
    {
        var handler = new RecordingHandler();
        var transport = new HttpAtlassianTransport(
            Classic, "jira", handler, HttpAtlassianTransport.JsonTimeout, TimeProvider.System, new RefusingUriBuilder());

        await Assert.ThrowsAsync<ArgumentException>(() => transport.GetAsync(Myself(), Ct));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void An_invalid_product_is_refused_even_with_an_injected_builder()
    {
        Assert.ThrowsAny<ArgumentException>(() => new HttpAtlassianTransport(
            Classic, "Jira", new RecordingHandler(), HttpAtlassianTransport.JsonTimeout, TimeProvider.System, new RefusingUriBuilder()));
    }

    /// <summary>A builder that refuses every request, as <see cref="RequestUriBuilder"/> does for one outside the base.</summary>
    private sealed class RefusingUriBuilder : IRequestUriBuilder
    {
        public Uri Build(RelativeRequest request) => throw new ArgumentException("outside the base", nameof(request));
    }

    /// <summary>A builder that always returns the same URI.</summary>
    private sealed class FixedUriBuilder(string uri) : IRequestUriBuilder
    {
        public List<RelativeRequest> Requests { get; } = [];

        public Uri Build(RelativeRequest request)
        {
            Requests.Add(request);
            return new Uri(uri, UriKind.Absolute);
        }
    }
}
