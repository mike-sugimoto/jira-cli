using System.Net;
using Atlassian.Common.Transport;

namespace Atlassian.Common.Tests.Transport;

public sealed class TransportExceptionTests
{
    [Fact]
    public void Body_is_kept_but_never_in_the_message()
    {
        var exception = new TransportException(TransportFailureKind.Status, HttpStatusCode.BadRequest, responseBody: "secret body text");

        Assert.Equal("secret body text", exception.ResponseBody);
        Assert.DoesNotContain("secret body text", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret body text", exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Body_is_capped()
    {
        var exception = new TransportException(TransportFailureKind.Status, HttpStatusCode.BadRequest, responseBody: new string('y', 10_000));

        Assert.Equal(TransportException.MaxResponseBodyBytes, exception.ResponseBody!.Length);
    }

    [Theory]
    [InlineData("x", "あ")]
    [InlineData("x", "\U0001F600")]
    [InlineData("", "\U0001F600")]
    public void Multi_byte_body_is_capped_on_a_character_boundary(string prefix, string character)
    {
        var body = prefix + string.Concat(Enumerable.Repeat(character, 3_000));

        var exception = new TransportException(TransportFailureKind.Status, HttpStatusCode.BadRequest, responseBody: body);

        HttpAtlassianTransportTests.AssertCappedOnBoundary(body, exception.ResponseBody!);
    }

    [Fact]
    public void Status_kind_requires_a_status_and_other_kinds_refuse_one()
    {
        Assert.Throws<ArgumentException>(() => new TransportException(TransportFailureKind.Status));
        Assert.Throws<ArgumentException>(() => new TransportException(TransportFailureKind.Timeout, HttpStatusCode.OK));
        Assert.ThrowsAny<ArgumentException>(() => new TransportException((TransportFailureKind)42));
        Assert.ThrowsAny<ArgumentException>(
            () => new TransportException(TransportFailureKind.Status, HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(TransportFailureKind.Timeout)]
    [InlineData(TransportFailureKind.Connection)]
    public void Non_status_kinds_have_a_message(TransportFailureKind kind)
    {
        var exception = new TransportException(kind);

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Null(exception.StatusCode);
    }
}
