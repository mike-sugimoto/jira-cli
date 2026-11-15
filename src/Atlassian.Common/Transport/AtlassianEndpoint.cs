using System.Text.RegularExpressions;
using Atlassian.Common.Config;

namespace Atlassian.Common.Transport;

/// <summary>
/// The single place that picks the base URL (AD-12): the site URL for classic tokens, the
/// <c>api.atlassian.com</c> gateway for scoped tokens (when a cloud id is set). The product
/// segment is caller-supplied, so this assembly names no product (AD-2).
/// </summary>
public static partial class AtlassianEndpoint
{
    public static Uri For(ConnectionSettings settings, string product)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(product);

        if (!ProductSegment().IsMatch(product))
        {
            throw new ArgumentException("The product must be one lower-case path segment.", nameof(product));
        }

        return settings.CloudId is { } cloudId
            ? new Uri($"https://api.atlassian.com/ex/{product}/{cloudId:D}/", UriKind.Absolute)
            : new Uri($"https://{settings.Site}.atlassian.net/", UriKind.Absolute);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ProductSegment();
}
