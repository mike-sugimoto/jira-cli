namespace JiraCli.Domain.Tests;

public sealed class ProjectKeyTests
{
    [Theory]
    [InlineData("ABC", "ABC")]
    [InlineData("abc", "ABC")]
    [InlineData("aBc_1", "ABC_1")]
    [InlineData("A1", "A1")]
    [InlineData("AB_", "AB_")]
    public void Valid_keys_are_upper_cased(string text, string expected)
    {
        Assert.True(ProjectKey.TryParse(text, out var key));
        Assert.Equal(expected, key.Value);
        Assert.Equal(expected, key.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1X")]
    [InlineData("_AB")]
    [InlineData("AB-1")]
    [InlineData("A B")]
    [InlineData(" AB")]
    [InlineData("AB\n")]
    [InlineData("ıab")] // dotless i, which upper-cases to ASCII 'I'
    [InlineData("abſ")] // long s, which upper-cases to ASCII 'S'
    [InlineData("AB１")] // full-width digit one
    public void Invalid_keys_are_refused(string? text)
    {
        Assert.False(ProjectKey.TryParse(text, out var key));
        Assert.Null(key);
    }

    [Fact]
    public void Keys_compare_by_canonical_value()
    {
        Assert.Equal(ProjectKey.Parse("abc"), ProjectKey.Parse("ABC"));
        Assert.NotEqual(ProjectKey.Parse("ABC"), ProjectKey.Parse("ABD"));
    }

    [Fact]
    public void Parse_throws_without_the_value_in_the_message()
    {
        var exception = Assert.Throws<FormatException>(() => ProjectKey.Parse("SECRET-1"));

        Assert.DoesNotContain("SECRET", exception.Message, StringComparison.Ordinal);
    }
}
