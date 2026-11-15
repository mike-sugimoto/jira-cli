using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>Self-test fixture: a request shape with a raw URL and a method, both of which are banned.</summary>
internal sealed class UrlAndVerbRequest
{
    public string Url { get; } = string.Empty;

    public Uri? Target { get; }

    public HttpMethod Method { get; } = HttpMethod.Post;

    public string Verb() => "POST";
}
