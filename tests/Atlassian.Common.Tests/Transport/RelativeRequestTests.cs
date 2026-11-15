using Atlassian.Common.Transport;

namespace Atlassian.Common.Tests.Transport;

public sealed class RelativeRequestTests
{
    [Fact]
    public void Keeps_segments_query_and_kind_as_copies()
    {
        var segments = new List<string> { "rest", "api" };
        var query = new List<KeyValuePair<string, string>> { new("a", "b") };

        var request = new RelativeRequest(segments, query, ResponseKind.Json);
        segments.Add("evil");
        query.Clear();

        Assert.Equal(["rest", "api"], request.Segments);
        Assert.Equal([new KeyValuePair<string, string>("a", "b")], request.Query);
        Assert.Equal(ResponseKind.Json, request.Kind);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    public void Dot_and_empty_segments_are_refused(string segment)
    {
        Assert.Throws<ArgumentException>(() => new RelativeRequest(["rest", segment, "x"], [], ResponseKind.Json));
    }

    [Fact]
    public void Missing_or_null_parts_are_refused()
    {
        Assert.Throws<ArgumentException>(() => new RelativeRequest([], [], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(null!, [], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a"], null!, ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a", null!], [], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a"], [new("", "v")], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a"], [new(null!, "v")], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a"], [new("n", null!)], ResponseKind.Json));
        Assert.ThrowsAny<ArgumentException>(() => new RelativeRequest(["a"], [], (ResponseKind)99));
    }

    [Fact]
    public void Empty_query_values_are_allowed()
    {
        var request = new RelativeRequest(["a"], [new("n", "")], ResponseKind.Json);

        Assert.Equal("", request.Query[0].Value);
    }
}
