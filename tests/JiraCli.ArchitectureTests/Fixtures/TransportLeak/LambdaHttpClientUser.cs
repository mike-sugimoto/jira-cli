using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>
/// Self-test fixture for <see cref="TransportSurfaceTests"/>: an <c>HttpClient</c> created and used
/// only inside an async lambda. Never used at run time.
/// </summary>
internal static class LambdaHttpClientUser
{
    public static Func<Task<string>> Fetch() => async () =>
    {
        await Task.Yield();
        using var client = new HttpClient();
        return await client.GetStringAsync(new Uri("https://example.invalid/"));
    };
}
