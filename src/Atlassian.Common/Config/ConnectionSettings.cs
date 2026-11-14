using System.Text.RegularExpressions;

namespace Atlassian.Common.Config;

/// <summary>
/// Where and as whom to connect to an Atlassian Cloud site. Only the transport reads the
/// credentials. This is deliberately not a record, so no generated <c>ToString</c> prints the token.
/// </summary>
public sealed partial class ConnectionSettings
{
    public ConnectionSettings(string site, string email, string apiToken, Guid? cloudId = null)
    {
        if (!IsValidSite(site))
        {
            throw new ArgumentException("The site must be a single DNS label.", nameof(site));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("The email must not be blank.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            throw new ArgumentException("The API token must not be blank.", nameof(apiToken));
        }

        if (cloudId == Guid.Empty)
        {
            throw new ArgumentException("The cloud id must not be the empty GUID.", nameof(cloudId));
        }

        Site = site.ToLowerInvariant();
        Email = email;
        ApiToken = apiToken;
        CloudId = cloudId;
    }

    /// <summary>The site name, lower-cased: the <c>&lt;site&gt;</c> in <c>https://&lt;site&gt;.atlassian.net/</c>.</summary>
    public string Site { get; }

    public string Email { get; }

    public string ApiToken { get; }

    /// <summary>Set for scoped tokens, which go through the <c>api.atlassian.com</c> gateway.</summary>
    public Guid? CloudId { get; }

    /// <summary>True when <paramref name="site"/> is a single ASCII DNS label (1 to 63 letters, digits, or inner hyphens).</summary>
    public static bool IsValidSite(string? site) => site is not null && SiteLabel().IsMatch(site);

    public override string ToString() => $"{nameof(ConnectionSettings)} {{ Site = {Site} }}";

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex SiteLabel();
}
