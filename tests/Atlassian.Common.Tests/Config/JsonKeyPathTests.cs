using Atlassian.Common.Config;

namespace Atlassian.Common.Tests.Config;

public sealed class JsonKeyPathTests
{
    [Theory]
    [InlineData("$.site", "site")]
    [InlineData("$['my-app'].count", "my-app.count")]
    [InlineData("$['my-app'].items[0]", "my-app.items[0]")]
    [InlineData("$.a.b[12].c", "a.b[12].c")]
    [InlineData("$[3]", "[3]")]
    [InlineData("$", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("site", null)]
    [InlineData("$['unterminated", null)]
    [InlineData("$[x]", null)]
    public void Converts_json_paths_to_readable_key_paths(string? jsonPath, string? expected)
    {
        Assert.Equal(expected, JsonKeyPath.Readable(jsonPath));
    }
}
