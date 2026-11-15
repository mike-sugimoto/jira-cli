using System.Globalization;
using System.Net;
using System.Text;
using Atlassian.Common.Config;
using Atlassian.Common.Transport;
using JiraCli.Cli.Config;

namespace JiraCli.LiveCheck;

/// <summary>
/// Sends <c>myself</c> and one <c>project/{key}</c> per allowed project, in order, and prints the
/// endpoint, each request path, and each status and body as received. It never reads the email or
/// token and never prints a header, so the credentials cannot reach the output. No retry; a failure
/// never stops the remaining requests.
/// </summary>
internal static class LiveCheckRunner
{
    /// <summary>The product segment of the scoped-token gateway URL.</summary>
    public const string Product = "jira";

    public const int AllSucceeded = 0;
    public const int SomeFailed = 1;
    public const int ConfigFailed = 2;

    private static readonly string[] ApiPrefix = ["rest", "api", "3"];

    public static async Task<int> RunAsync(
        ConfigLoadResult config,
        Func<ConnectionSettings, IAtlassianTransport> createTransport,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(createTransport);
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteLineAsync("LiveCheck: GET-only requests to the configured Jira. Credentials are never printed.");

        if (config is not { IsSuccess: true, Connection: { } connection, Access: { } access })
        {
            var failure = config.Failure;
            await output.WriteLineAsync($"Config failure: {failure?.Message ?? "The config did not load."}");
            await output.WriteLineAsync($"Hint: {failure?.Hint ?? "Check the config file."}");
            return ConfigFailed;
        }

        await output.WriteLineAsync($"Endpoint: {AtlassianEndpoint.For(connection, Product)}");

        var transport = createTransport(connection);
        string[][] requests =
        [
            [.. ApiPrefix, "myself"],
            .. access.AllowedProjects.Select(project => (string[])[.. ApiPrefix, "project", project.Value]),
        ];

        var succeeded = 0;
        foreach (var segments in requests)
        {
            await output.WriteLineAsync();
            if (await SendAsync(transport, segments, output, cancellationToken))
            {
                succeeded++;
            }
        }

        await output.WriteLineAsync();
        await output.WriteLineAsync($"Summary: {succeeded} of {requests.Length} requests returned 2xx.");
        return succeeded == requests.Length ? AllSucceeded : SomeFailed;
    }

    private static async Task<bool> SendAsync(
        IAtlassianTransport transport, string[] segments, TextWriter output, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"GET {string.Join('/', segments)}");
        var request = new RelativeRequest(segments, [], ResponseKind.Json);

        try
        {
            using var response = await transport.GetAsync(request, cancellationToken);
            await output.WriteLineAsync($"Status: {Describe(response.StatusCode)}");
            using var reader = new StreamReader(response.Body, Encoding.UTF8);
            await WriteBodyAsync(output, await reader.ReadToEndAsync(cancellationToken));
            return (int)response.StatusCode is >= 200 and < 300;
        }
        catch (TransportException exception)
        {
            await output.WriteLineAsync($"Failure: {exception.Kind}");
            if (exception.Kind == TransportFailureKind.Status)
            {
                await output.WriteLineAsync($"Status: {(exception.StatusCode is { } status ? Describe(status) : "(none)")}");
                await output.WriteLineAsync($"Retry-After: {(exception.RetryAfter is { } retryAfter ? $"{Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture)} s" : "(none)")}");
                await WriteBodyAsync(output, exception.ResponseBody);
            }
            else
            {
                // Types and enum values only, never messages: enough to tell DNS, TLS, proxy, and refusals apart.
                for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
                {
                    var detail = inner is HttpRequestException http ? $" (HttpRequestError: {http.HttpRequestError})" : "";
                    await output.WriteLineAsync($"Inner exception: {inner.GetType().FullName}{detail}");
                }
            }

            return false;
        }
    }

    private static string Describe(HttpStatusCode status) =>
        $"{((int)status).ToString(CultureInfo.InvariantCulture)} ({status})";

    private static async Task WriteBodyAsync(TextWriter output, string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            await output.WriteLineAsync("Body: (none)");
            return;
        }

        await output.WriteLineAsync("Body:");
        await output.WriteLineAsync(body);
    }
}
