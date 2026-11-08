using System.Diagnostics;

namespace JiraCli.Cli.Tests;

/// <summary>Runs the built <c>jira-cli</c> assembly as a real process to check its stdout bytes.</summary>
public sealed class BuiltToolTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("--help")]
    public async Task Built_tool_prints_plain_text_utf8_without_bom_and_exits_zero(string argument)
    {
        var toolPath = Path.Combine(AppContext.BaseDirectory, "jira-cli.dll");
        Assert.True(File.Exists(toolPath), $"Built tool not found at {toolPath}.");

        var startInfo = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        using var stdout = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(stdout, TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        await copy;

        var bytes = stdout.ToArray();
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, await stderr);
        Assert.NotEmpty(bytes);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "stdout must not start with a UTF-8 BOM.");
        Assert.NotEqual((byte)'{', bytes[0]);
        if (argument == "--version")
        {
            Assert.Equal("0.1.0", System.Text.Encoding.UTF8.GetString(bytes).Trim());
        }
    }

    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath
            ? hostPath
            : "dotnet";
}
