namespace JiraCli.Domain.Tests;

public sealed class AccessSettingsTests
{
    private static readonly ProjectKey Abc = ProjectKey.Parse("ABC");
    private static readonly ProjectKey Xyz = ProjectKey.Parse("XYZ");

    [Fact]
    public void Defaults_are_20_and_100()
    {
        var settings = new AccessSettings([Abc], []);

        Assert.Equal(20, settings.DefaultPageSize);
        Assert.Equal(100, settings.MaxPageSize);
        Assert.Empty(settings.CustomFieldPassthrough);
    }

    [Fact]
    public void Projects_and_passthrough_ids_are_deduplicated_in_order()
    {
        var settings = new AccessSettings(
            [Xyz, Abc, ProjectKey.Parse("xyz")],
            ["customfield_2", "customfield_1", "customfield_2"]);

        Assert.Equal([Xyz, Abc], settings.AllowedProjects);
        Assert.Equal(["customfield_2", "customfield_1"], settings.CustomFieldPassthrough);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(5, 5000)]
    public void Page_sizes_within_bounds_are_kept(int defaultPageSize, int maxPageSize)
    {
        var settings = new AccessSettings([Abc], [], defaultPageSize, maxPageSize);

        Assert.Equal(defaultPageSize, settings.DefaultPageSize);
        Assert.Equal(maxPageSize, settings.MaxPageSize);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(20, 0)]
    [InlineData(101, 100)]
    public void Page_sizes_out_of_bounds_are_refused(int defaultPageSize, int maxPageSize)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AccessSettings([Abc], [], defaultPageSize, maxPageSize));
    }

    [Fact]
    public void At_least_one_project_is_required()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AccessSettings([], []));
    }

    [Theory]
    [InlineData("cf_1")]
    [InlineData("customfield_")]
    [InlineData("customfield_1a")]
    [InlineData("Customfield_1")]
    [InlineData("customfield_1\n")]
    [InlineData("customfield_１")]
    public void Passthrough_entries_must_be_custom_field_ids(string id)
    {
        Assert.False(AccessSettings.IsValidCustomFieldId(id));
        Assert.ThrowsAny<ArgumentException>(() => new AccessSettings([Abc], [id]));
    }

    [Fact]
    public void Custom_field_ids_are_accepted()
    {
        Assert.True(AccessSettings.IsValidCustomFieldId("customfield_10010"));
    }
}
