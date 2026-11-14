using Atlassian.Common.Config;

namespace Atlassian.Common.Tests.Config;

public sealed class ConnectionSettingsTests
{
    private const string Token = "SECRET-api+token/==";

    [Fact]
    public void Site_is_lower_cased_and_values_are_kept()
    {
        var cloudId = Guid.NewGuid();

        var settings = new ConnectionSettings("My-Site", "me@example.com", Token, cloudId);

        Assert.Equal("my-site", settings.Site);
        Assert.Equal("me@example.com", settings.Email);
        Assert.Equal(Token, settings.ApiToken);
        Assert.Equal(cloudId, settings.CloudId);
    }

    [Fact]
    public void Cloud_id_is_optional()
    {
        Assert.Null(new ConnectionSettings("site", "me@example.com", Token).CloudId);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData("a-b")]
    [InlineData("A1")]
    [InlineData("a123456789012345678901234567890123456789012345678901234567890bc")]
    public void Valid_sites_are_single_dns_labels(string site)
    {
        Assert.True(ConnectionSettings.IsValidSite(site));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a b")]
    [InlineData("a_b")]
    [InlineData("https://a")]
    [InlineData("site\n")]
    [InlineData("Key")] // Kelvin sign, which lower-cases to ASCII 'k'
    [InlineData("café")]
    [InlineData("a1234567890123456789012345678901234567890123456789012345678901bc")]
    public void Invalid_sites_are_refused(string? site)
    {
        Assert.False(ConnectionSettings.IsValidSite(site));
        Assert.ThrowsAny<ArgumentException>(() => new ConnectionSettings(site!, "me@example.com", Token));
    }

    [Fact]
    public void Guards_reject_blank_credentials_and_the_empty_guid()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConnectionSettings("site", " ", Token));
        Assert.ThrowsAny<ArgumentException>(() => new ConnectionSettings("site", "me@example.com", ""));
        Assert.ThrowsAny<ArgumentException>(() => new ConnectionSettings("site", "me@example.com", Token, Guid.Empty));
    }

    [Fact]
    public void Guard_messages_never_contain_the_value()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new ConnectionSettings("SECRET.value", "me@example.com", Token));

        Assert.DoesNotContain("SECRET", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_never_prints_the_token_or_email()
    {
        var text = new ConnectionSettings("site", "me@example.com", Token, Guid.NewGuid()).ToString();

        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Is_not_a_record_with_a_generated_ToString()
    {
        Assert.Null(typeof(ConnectionSettings).GetMethod("<Clone>$"));
        Assert.Null(typeof(ConnectionSettings).GetMethod("PrintMembers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
    }
}
