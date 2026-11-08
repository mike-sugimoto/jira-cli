using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli.Tests;

public sealed class CliAppTests
{
    private static ServiceProvider ThrowingFactory() =>
        throw new InvalidOperationException("The container factory must not be called for --help or --version.");

    [Fact]
    public async Task Version_prints_the_version_as_plain_text_without_building_the_container()
    {
        var output = new StringWriter();

        var exitCode = await CliApp.RunAsync(["--version"], ThrowingFactory, () => output);

        // In-process, System.CommandLine reads the version from the entry assembly (the test host),
        // so the exact "0.1.0" is asserted against the built tool in BuiltToolTests.
        Assert.Equal(0, exitCode);
        var text = output.ToString().Trim();
        Assert.NotEmpty(text);
        Assert.False(text.StartsWith('{'), "Version output must be plain text, not JSON.");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public async Task Help_prints_plain_text_usage_without_building_the_container(string helpArgument)
    {
        var output = new StringWriter();

        var exitCode = await CliApp.RunAsync([helpArgument], ThrowingFactory, () => output);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("Usage:", text, StringComparison.Ordinal);
        Assert.Contains("Read-only Jira Cloud CLI", text, StringComparison.Ordinal);
        Assert.False(text.TrimStart().StartsWith('{'), "Help output must be plain text, not JSON.");
    }

    [Fact]
    public async Task Other_invocations_build_the_container_exactly_once()
    {
        var calls = 0;
        ServiceProvider CountingFactory()
        {
            calls++;
            return CompositionRoot.Build();
        }

        var exitCode = await CliApp.RunAsync([], CountingFactory, () => throw new InvalidOperationException("No plain-text output expected."));

        Assert.Equal(0, exitCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CompositionRoot_builds_a_validated_container()
    {
        using var provider = CompositionRoot.Build();

        Assert.NotNull(provider);
    }
}
