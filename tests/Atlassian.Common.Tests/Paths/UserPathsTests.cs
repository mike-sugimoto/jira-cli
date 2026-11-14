using Atlassian.Common.Paths;

namespace Atlassian.Common.Tests.Paths;

public sealed class UserPathsTests
{
    private sealed class FakeSource(UserPlatform platform, UserPathLookup? roaming = null, UserPathLookup? home = null) : IUserFolderSource
    {
        public UserPlatform Platform => platform;

        public UserPathLookup RoamingAppData() => roaming ?? throw new InvalidOperationException("Roaming lookup not expected.");

        public UserPathLookup PasswdHome() => home ?? throw new InvalidOperationException("Home lookup not expected.");
    }

    [Theory]
    [InlineData(@"C:\Users\mike\AppData\Roaming", @"C:\Users\mike\AppData\Roaming\jira-cli\config.json")]
    [InlineData(@"C:\Users\mike\AppData\Roaming\", @"C:\Users\mike\AppData\Roaming\jira-cli\config.json")]
    [InlineData(@"\\server\profiles\mike\Roaming", @"\\server\profiles\mike\Roaming\jira-cli\config.json")]
    public void Windows_config_file_is_under_roaming_app_data(string roaming, string expected)
    {
        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Windows, roaming: UserPathLookup.FromPath(roaming)));

        Assert.Equal(expected, result.Path);
        Assert.Null(result.Problem);
    }

    [Theory]
    [InlineData("/home/mike", "/home/mike/.config/jira-cli/config.json")]
    [InlineData("/home/mike/", "/home/mike/.config/jira-cli/config.json")]
    [InlineData("/", "/.config/jira-cli/config.json")]
    public void Linux_config_file_is_under_passwd_home(string home, string expected)
    {
        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Linux, home: UserPathLookup.FromPath(home)));

        Assert.Equal(expected, result.Path);
    }

    [Fact]
    public void App_name_comes_from_the_caller()
    {
        var result = UserPaths.ConfigFile("other-app", new FakeSource(UserPlatform.Linux, home: UserPathLookup.FromPath("/home/mike")));

        Assert.Equal("/home/mike/.config/other-app/config.json", result.Path);
    }

    [Fact]
    public void Native_lookup_failure_is_passed_through_without_fallback()
    {
        var failure = UserPathLookup.Unavailable("getpwuid_r failed with error 5");

        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Linux, home: failure));

        Assert.False(result.IsFound);
        Assert.Null(result.Path);
        Assert.Equal("getpwuid_r failed with error 5", result.Problem);
    }

    [Theory]
    [InlineData("relative/home")]
    [InlineData("~")]
    [InlineData(@"C:\Users\mike")]
    public void Linux_relative_home_is_unavailable(string home)
    {
        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Linux, home: UserPathLookup.FromPath(home)));

        Assert.Null(result.Path);
        Assert.NotNull(result.Problem);
    }

    [Theory]
    [InlineData("Roaming")]
    [InlineData(@"\Users\mike")]
    [InlineData("C:")]
    [InlineData("/home/mike")]
    public void Windows_relative_folder_is_unavailable(string roaming)
    {
        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Windows, roaming: UserPathLookup.FromPath(roaming)));

        Assert.Null(result.Path);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public void Unsupported_platform_such_as_macOS_is_unavailable()
    {
        var result = UserPaths.ConfigFile("jira-cli", new FakeSource(UserPlatform.Unsupported));

        Assert.Null(result.Path);
        Assert.Contains("not supported", result.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("c:x")]
    [InlineData(" app")]
    public void App_name_must_be_a_plain_folder_name(string appName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            UserPaths.ConfigFile(appName, new FakeSource(UserPlatform.Linux, home: UserPathLookup.FromPath("/home/mike"))));
    }

    [Fact]
    public void Empty_native_answers_cannot_be_constructed_as_found()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserPathLookup.FromPath(""));
        Assert.ThrowsAny<ArgumentException>(() => UserPathLookup.Unavailable(""));
    }

    [Fact]
    public void Real_lookup_on_Windows_returns_an_absolute_path_under_the_profile()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows only.");
        }

        var result = UserPaths.ConfigFile("jira-cli");

        Assert.True(result.IsFound, result.Problem);
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "jira-cli", "config.json"),
            result.Path);
    }

    [Fact]
    public void Real_lookup_on_Linux_returns_an_absolute_path_under_the_passwd_home()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Linux only.");
        }

        var result = UserPaths.ConfigFile("jira-cli");

        Assert.True(result.IsFound, result.Problem);
        Assert.StartsWith("/", result.Path, StringComparison.Ordinal);
        Assert.EndsWith("/.config/jira-cli/config.json", result.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_lookup_on_other_platforms_is_unavailable()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            Assert.Skip("Only for platforms other than Windows and Linux.");
        }

        Assert.False(UserPaths.ConfigFile("jira-cli").IsFound);
    }
}
