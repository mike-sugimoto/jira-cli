using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>
/// Self-test fixture for <see cref="TransportSurfaceTests"/>: each banned certificate bypass sits
/// inside a lambda. Never used at run time.
/// </summary>
internal static class LambdaCertificateBypass
{
    public static Func<HttpMessageHandler> Classic() => () => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    };

    public static Func<HttpMessageHandler> Sockets() => () =>
    {
        var handler = new SocketsHttpHandler();
        handler.SslOptions.RemoteCertificateValidationCallback = new RemoteCertificateValidationCallback((_, _, _, _) => true);
        return handler;
    };
}
