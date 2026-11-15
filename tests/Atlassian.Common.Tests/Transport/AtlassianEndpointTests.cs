using Atlassian.Common.Config;
using Atlassian.Common.Transport;

namespace Atlassian.Common.Tests.Transport;

public sealed class AtlassianEndpointTests
{
    private const string Token = "SECRET-api+token/==";

    [Fact]
    public void Classic_token_uses_the_site_url()
    {
        var settings = new ConnectionSettings("Acme", "me@example.com", Token);

        Assert.Equal("https://acme.atlassian.net/", AtlassianEndpoint.For(settings, "jira").AbsoluteUri);
    }

    [Fact]
    public void Scoped_token_uses_the_gateway_with_the_caller_supplied_product()
    {
        var cloudId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var settings = new ConnectionSettings("acme", "me@example.com", Token, cloudId);

        Assert.Equal(
            "https://api.atlassian.com/ex/jira/11111111-2222-3333-4444-555555555555/",
            AtlassianEndpoint.For(settings, "jira").AbsoluteUri);
        Assert.Equal(
            "https://api.atlassian.com/ex/confluence/11111111-2222-3333-4444-555555555555/",
            AtlassianEndpoint.For(settings, "confluence").AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("Jira")]
    [InlineData("ji ra")]
    [InlineData("jira?")]
    public void Product_must_be_one_lower_case_path_segment(string product)
    {
        var settings = new ConnectionSettings("acme", "me@example.com", Token, Guid.NewGuid());

        Assert.ThrowsAny<ArgumentException>(() => AtlassianEndpoint.For(settings, product));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        var settings = new ConnectionSettings("acme", "me@example.com", Token);

        Assert.Throws<ArgumentNullException>(() => AtlassianEndpoint.For(null!, "jira"));
        Assert.Throws<ArgumentNullException>(() => AtlassianEndpoint.For(settings, null!));
    }
}
