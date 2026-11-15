using Atlassian.Common.Transport;

namespace Atlassian.Common.Tests.Transport;

public sealed class RequestUriBuilderTests
{
    private static readonly Uri SiteBase = new("https://acme.atlassian.net/");
    private static readonly Uri GatewayBase = new("https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555/");

    private static string Build(Uri baseUri, string[] segments, params KeyValuePair<string, string>[] query) =>
        new RequestUriBuilder(baseUri).Build(new RelativeRequest(segments, query, ResponseKind.Json)).AbsoluteUri;

    [Fact]
    public void Segments_are_appended_to_the_site_base()
    {
        Assert.Equal("https://acme.atlassian.net/rest/api/3/myself", Build(SiteBase, ["rest", "api", "3", "myself"]));
    }

    [Fact]
    public void Segments_are_appended_to_the_gateway_base_path()
    {
        Assert.Equal(
            "https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555/rest/api/3/project/ABC",
            Build(GatewayBase, ["rest", "api", "3", "project", "ABC"]));
    }

    [Fact]
    public void Query_pairs_keep_their_order_and_are_joined_with_ampersands()
    {
        Assert.Equal(
            "https://acme.atlassian.net/rest/api/3/search?maxResults=20&fields=summary&nextPageToken=",
            Build(SiteBase, ["rest", "api", "3", "search"], new("maxResults", "20"), new("fields", "summary"), new("nextPageToken", "")));
    }

    [Theory]
    [InlineData("a/b c", "a%2Fb%20c")]
    [InlineData("..%2F..", "..%252F..")]
    [InlineData("?x=1", "%3Fx%3D1")]
    [InlineData("#frag", "%23frag")]
    [InlineData("@evil.example", "%40evil.example")]
    [InlineData("\\\\evil", "%5C%5Cevil")]
    [InlineData("あ", "%E3%81%82")]
    public void Each_segment_is_percent_encoded(string segment, string expected)
    {
        Assert.Equal($"https://acme.atlassian.net/{expected}/x", Build(SiteBase, [segment, "x"]));
    }

    [Theory]
    [InlineData("jql", "project = ABC & x", "jql=project%20%3D%20ABC%20%26%20x")]
    [InlineData("a b", "c+d", "a%20b=c%2Bd")]
    [InlineData("k", "#?/", "k=%23%3F%2F")]
    public void Query_names_and_values_are_percent_encoded(string name, string value, string expected)
    {
        Assert.Equal($"https://acme.atlassian.net/s?{expected}", Build(SiteBase, ["s"], new KeyValuePair<string, string>(name, value)));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestUriBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => new RequestUriBuilder(SiteBase).Build(null!));
        Assert.Throws<ArgumentNullException>(() => new RequestUriBuilder(SiteBase).EnsureUnderBase(null!));
    }

    [Theory]
    [InlineData("https://acme.atlassian.net/rest/api/3/myself")]
    [InlineData("https://acme.atlassian.net/x?q=1")]
    public void A_uri_under_the_site_base_is_accepted(string candidate)
    {
        var uri = new Uri(candidate);

        Assert.Same(uri, new RequestUriBuilder(SiteBase).EnsureUnderBase(uri));
    }

    [Theory]
    [InlineData("https://evil.example/rest/api/3/myself")]
    [InlineData("http://acme.atlassian.net/rest/api/3/myself")]
    [InlineData("https://acme.atlassian.net:8443/rest/api/3/myself")]
    [InlineData("https://user@acme.atlassian.net/rest/api/3/myself")]
    [InlineData("https://acme.atlassian.net/rest/api/3/myself#frag")]
    [InlineData("https://acme.atlassian.net.evil.example/rest/api/3/myself")]
    [InlineData("https://acme.atlassian.net/")]
    public void A_uri_outside_the_site_base_is_refused(string outside)
    {
        Assert.Throws<ArgumentException>(() => new RequestUriBuilder(SiteBase).EnsureUnderBase(new Uri(outside)));
    }

    [Theory]
    [InlineData("https://api.atlassian.com/ex/confluence/11111111-2222-3333-4444-555555555555/wiki")]
    [InlineData("https://api.atlassian.com/ex/jira/99999999-2222-3333-4444-555555555555/rest/api/3/myself")]
    [InlineData("https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555")]
    [InlineData("https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555/")]
    public void A_uri_outside_the_gateway_base_path_is_refused(string outside)
    {
        Assert.Throws<ArgumentException>(() => new RequestUriBuilder(GatewayBase).EnsureUnderBase(new Uri(outside)));
    }
}
