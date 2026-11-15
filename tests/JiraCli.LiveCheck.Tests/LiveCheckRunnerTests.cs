using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Atlassian.Common.Config;
using Atlassian.Common.Transport;
using JiraCli.Cli.Config;
using JiraCli.Domain;

namespace JiraCli.LiveCheck.Tests;

/// <summary>The I/O and edge-case matrix of Story 1.3b, run against a recording fake transport.</summary>
public sealed class LiveCheckRunnerTests
{
    private const string Email = "me@example.com";
    private const string Token = "ATATT-secret+token/value==";
    private static readonly Guid CloudId = Guid.Parse("0f3b2c1a-4d5e-6f70-8192-a3b4c5d6e7f8");

    private readonly RecordingTransport transport = new();
    private readonly List<ConnectionSettings> factoryCalls = [];
    private readonly StringWriter output = new();

    private static ConfigLoadResult Loaded(Guid? cloudId = null, params string[] projects) =>
        ConfigLoadResult.Success(
            new ConnectionSettings("My-Site", Email, Token, cloudId),
            new AccessSettings(
                (projects.Length == 0 ? ["ABC", "XYZ"] : projects).Select(ProjectKey.Parse),
                []));

    private Task<int> Run(ConfigLoadResult config) =>
        LiveCheckRunner.RunAsync(
            config,
            settings =>
            {
                factoryCalls.Add(settings);
                return transport;
            },
            output,
            TestContext.Current.CancellationToken);

    private string[] Lines => output.ToString().Split(Environment.NewLine);

    [Fact]
    public async Task Classic_config_prints_the_site_endpoint_and_sends_myself_then_each_project()
    {
        var config = Loaded();
        transport
            .Answer("rest/api/3/myself", HttpStatusCode.OK, """{"accountId":"a1"}""")
            .Answer("rest/api/3/project/ABC", HttpStatusCode.OK, """{"key":"ABC"}""")
            .Answer("rest/api/3/project/XYZ", HttpStatusCode.OK, """{"key":"XYZ"}""");

        var exit = await Run(config);

        Assert.Equal(0, exit);
        Assert.Same(config.Connection, Assert.Single(factoryCalls));
        Assert.Equal(
            [
                ["rest", "api", "3", "myself"],
                ["rest", "api", "3", "project", "ABC"],
                ["rest", "api", "3", "project", "XYZ"],
            ],
            transport.Requests.Select(request => request.Segments.ToArray()));
        Assert.All(transport.Requests, request =>
        {
            Assert.Empty(request.Query);
            Assert.Equal(ResponseKind.Json, request.Kind);
        });

        Assert.Contains("Endpoint: https://my-site.atlassian.net/", Lines);
        Assert.Equal(
            [
                "GET rest/api/3/myself",
                "Status: 200 (OK)",
                "Body:",
                """{"accountId":"a1"}""",
            ],
            LinesAfter("GET rest/api/3/myself", 4));
        Assert.Equal(["GET rest/api/3/project/ABC", "Status: 200 (OK)", "Body:", """{"key":"ABC"}"""], LinesAfter("GET rest/api/3/project/ABC", 4));
        Assert.Equal(["GET rest/api/3/project/XYZ", "Status: 200 (OK)", "Body:", """{"key":"XYZ"}"""], LinesAfter("GET rest/api/3/project/XYZ", 4));
        Assert.Contains("Summary: 3 of 3 requests returned 2xx.", Lines);
    }

    [Fact]
    public async Task Scoped_config_prints_the_gateway_endpoint_and_sends_the_same_requests()
    {
        var exit = await Run(Loaded(CloudId));

        Assert.Equal(0, exit);
        Assert.Contains($"Endpoint: https://api.atlassian.com/ex/jira/{CloudId:D}/", Lines);
        Assert.Equal(
            ["rest/api/3/myself", "rest/api/3/project/ABC", "rest/api/3/project/XYZ"],
            transport.Requests.Select(request => string.Join('/', request.Segments)));
    }

    [Fact]
    public async Task Non_2xx_prints_kind_status_and_body_and_continues_with_the_remaining_requests()
    {
        transport
            .Fail("rest/api/3/myself", new TransportException(
                TransportFailureKind.Status, HttpStatusCode.Unauthorized, responseBody: """{"message":"Client must be authenticated"}"""))
            .Fail("rest/api/3/project/XYZ", new TransportException(
                TransportFailureKind.Status, HttpStatusCode.NotFound, responseBody: """{"errorMessages":["No project"]}"""));

        var exit = await Run(Loaded());

        Assert.Equal(1, exit);
        Assert.Equal(3, transport.Requests.Count);
        Assert.Equal(
            [
                "GET rest/api/3/myself",
                "Failure: Status",
                "Status: 401 (Unauthorized)",
                "Retry-After: (none)",
                "Body:",
                """{"message":"Client must be authenticated"}""",
            ],
            LinesAfter("GET rest/api/3/myself", 6));
        Assert.Equal(["GET rest/api/3/project/ABC", "Status: 200 (OK)", "Body:", "{}"], LinesAfter("GET rest/api/3/project/ABC", 4));
        Assert.Equal(
            [
                "GET rest/api/3/project/XYZ",
                "Failure: Status",
                "Status: 404 (NotFound)",
                "Retry-After: (none)",
                "Body:",
                """{"errorMessages":["No project"]}""",
            ],
            LinesAfter("GET rest/api/3/project/XYZ", 6));
        Assert.Contains("Summary: 1 of 3 requests returned 2xx.", Lines);
    }

    [Fact]
    public async Task Rate_limit_prints_retry_after_and_a_missing_body_as_none()
    {
        transport.Fail("rest/api/3/project/ABC", new TransportException(
            TransportFailureKind.Status, (HttpStatusCode)429, retryAfter: TimeSpan.FromSeconds(30)));

        var exit = await Run(Loaded());

        Assert.Equal(1, exit);
        Assert.Equal(
            [
                "GET rest/api/3/project/ABC",
                "Failure: Status",
                "Status: 429 (TooManyRequests)",
                "Retry-After: 30 s",
                "Body: (none)",
            ],
            LinesAfter("GET rest/api/3/project/ABC", 5));
    }

    [Fact]
    public async Task Timeout_prints_the_kind_and_continues()
    {
        transport.Fail("rest/api/3/myself", new TransportException(
            TransportFailureKind.Timeout,
            innerException: new TaskCanceledException("canceled", new TimeoutException("timed out"))));

        var exit = await Run(Loaded());

        Assert.Equal(1, exit);
        Assert.Equal(
            ["rest/api/3/myself", "rest/api/3/project/ABC", "rest/api/3/project/XYZ"],
            transport.Requests.Select(request => string.Join('/', request.Segments)));
        Assert.Equal(
            [
                "GET rest/api/3/myself",
                "Failure: Timeout",
                "Inner exception: System.Threading.Tasks.TaskCanceledException",
                "Inner exception: System.TimeoutException",
            ],
            LinesAfter("GET rest/api/3/myself", 4));
        Assert.Equal(["GET rest/api/3/project/ABC", "Status: 200 (OK)", "Body:", "{}"], LinesAfter("GET rest/api/3/project/ABC", 4));
        Assert.DoesNotContain(Lines, line => line.Contains("timed out", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fractional_retry_after_is_printed_as_whole_seconds_rounded_up()
    {
        transport.Fail("rest/api/3/myself", new TransportException(
            TransportFailureKind.Status, (HttpStatusCode)429, retryAfter: TimeSpan.FromSeconds(29.98712)));

        await Run(Loaded());

        Assert.Equal("Retry-After: 30 s", LinesAfter("GET rest/api/3/myself", 4)[3]);
    }

    [Fact]
    public async Task Connection_failure_prints_the_kind_and_inner_exception_type_and_continues()
    {
        transport.Fail("rest/api/3/project/ABC", new TransportException(
            TransportFailureKind.Connection,
            innerException: new HttpRequestException(
                HttpRequestError.NameResolutionError,
                "No such host is known.",
                new SocketException((int)SocketError.HostNotFound))));

        var exit = await Run(Loaded());

        Assert.Equal(1, exit);
        Assert.Equal(
            ["rest/api/3/myself", "rest/api/3/project/ABC", "rest/api/3/project/XYZ"],
            transport.Requests.Select(request => string.Join('/', request.Segments)));
        Assert.Equal(
            [
                "GET rest/api/3/project/ABC",
                "Failure: Connection",
                "Inner exception: System.Net.Http.HttpRequestException (HttpRequestError: NameResolutionError)",
                "Inner exception: System.Net.Sockets.SocketException",
            ],
            LinesAfter("GET rest/api/3/project/ABC", 4));
        Assert.Equal(["GET rest/api/3/project/XYZ", "Status: 200 (OK)", "Body:", "{}"], LinesAfter("GET rest/api/3/project/XYZ", 4));
        Assert.DoesNotContain(Lines, line => line.Contains("No such host", StringComparison.Ordinal));
        Assert.DoesNotContain(Lines, line => line.Contains("host is known", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Config_failure_prints_message_and_hint_sends_nothing_and_exits_2()
    {
        var failure = new ConfigLoadFailure(
            ConfigFileFailureKind.Missing, null, "The config file was not found at C:\\x\\config.json.", "Create the file.", "C:\\x\\config.json");

        var exit = await Run(ConfigLoadResult.Failed(failure));

        Assert.Equal(2, exit);
        Assert.Empty(factoryCalls);
        Assert.Empty(transport.Requests);
        Assert.Equal(
            [
                "Config failure: The config file was not found at C:\\x\\config.json.",
                "Hint: Create the file.",
            ],
            Lines.Where(line => line.Length > 0 && !line.StartsWith("LiveCheck:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task No_output_line_contains_the_token_or_its_basic_value()
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"));
        transport.Fail("rest/api/3/myself", new TransportException(TransportFailureKind.Status, HttpStatusCode.Forbidden, responseBody: "denied"));

        await Run(Loaded());
        await Run(Loaded(CloudId));

        var text = output.ToString();
        Assert.NotEmpty(text);
        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("ATATT", text, StringComparison.Ordinal);
        Assert.DoesNotContain(basic, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
    }

    private string[] LinesAfter(string first, int count)
    {
        var lines = Lines;
        var start = Array.IndexOf(lines, first);
        Assert.True(start >= 0, $"'{first}' not found in output:{Environment.NewLine}{output}");
        return lines.Skip(start).Take(count).ToArray();
    }
}
