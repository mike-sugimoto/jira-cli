using System.Text;

namespace Atlassian.Common.Transport;

/// <summary>
/// Base URI + escaped segments + escaped query (AD-5: the transport alone percent-encodes), then
/// checked to sit under the base URL before it is returned (AD-12: credentials go only there).
/// </summary>
internal sealed class RequestUriBuilder : IRequestUriBuilder
{
    private readonly Uri _baseUri;

    public RequestUriBuilder(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _baseUri = baseUri;
    }

    public Uri Build(RelativeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var text = new StringBuilder(_baseUri.AbsoluteUri);
        text.AppendJoin('/', request.Segments.Select(Uri.EscapeDataString));

        for (var i = 0; i < request.Query.Count; i++)
        {
            var (name, value) = request.Query[i];
            text.Append(i == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        return EnsureUnderBase(new Uri(text.ToString(), UriKind.Absolute));
    }

    /// <summary>
    /// Returns <paramref name="candidate"/> when it sits strictly under the base URL (same scheme,
    /// host, and port, no user info or fragment, longer path with the base path as prefix);
    /// otherwise throws. Escaping in <see cref="Build"/> makes this unreachable today, so tests
    /// call it directly.
    /// </summary>
    internal Uri EnsureUnderBase(Uri candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var underBase =
            string.Equals(candidate.Scheme, _baseUri.Scheme, StringComparison.Ordinal)
            && string.Equals(candidate.Host, _baseUri.Host, StringComparison.Ordinal)
            && candidate.Port == _baseUri.Port
            && candidate.UserInfo.Length == 0
            && candidate.Fragment.Length == 0
            && candidate.AbsolutePath.StartsWith(_baseUri.AbsolutePath, StringComparison.Ordinal)
            && candidate.AbsolutePath.Length > _baseUri.AbsolutePath.Length;

        return underBase
            ? candidate
            : throw new ArgumentException("The request does not resolve under the base URL.", nameof(candidate));
    }
}
