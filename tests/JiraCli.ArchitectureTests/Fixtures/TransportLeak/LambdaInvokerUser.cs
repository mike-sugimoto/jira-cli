using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>
/// Self-test fixture for <see cref="TransportSurfaceTests"/>: an <c>HttpMessageInvoker</c>, which can
/// send any verb without <c>HttpClient</c>, used inside an async lambda. Never used at run time.
/// </summary>
internal static class LambdaInvokerUser
{
    public static Func<Task<HttpStatusCodeHolder>> Send() => async () =>
    {
        using var invoker = new HttpMessageInvoker(new SocketsHttpHandler());
        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "https://example.invalid/"), CancellationToken.None);
        return new HttpStatusCodeHolder((int)response.StatusCode);
    };
}
