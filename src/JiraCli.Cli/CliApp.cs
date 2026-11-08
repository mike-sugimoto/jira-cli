using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli;

/// <summary>
/// Parses the command line and runs it. <c>--help</c> and <c>--version</c> print plain text
/// and return without building the DI container; every other invocation builds the
/// container and runs inside one <see cref="AsyncServiceScope"/>.
/// </summary>
internal static class CliApp
{
    public static Task<int> RunAsync(string[] args, Func<ServiceProvider> containerFactory) =>
        RunAsync(args, containerFactory, PlainTextOutput.Create);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<ServiceProvider> containerFactory,
        Func<TextWriter> plainTextOutputFactory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(containerFactory);
        ArgumentNullException.ThrowIfNull(plainTextOutputFactory);

        var root = CreateRootCommand();
        var parseResult = root.Parse(args);

        if (IsPlainTextRequest(root, parseResult))
        {
            await using var output = plainTextOutputFactory();
            var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration { Output = output });
            await output.FlushAsync();
            return exitCode;
        }

        await using var provider = containerFactory();
        await using var scope = provider.CreateAsyncScope();
        return await parseResult.InvokeAsync();
    }

    internal static RootCommand CreateRootCommand() =>
        new("Read-only Jira Cloud CLI for Claude Code.");

    private static bool IsPlainTextRequest(RootCommand root, ParseResult parseResult)
    {
        var action = parseResult.Action;
        if (action is null)
        {
            return false;
        }

        return root.Options.Any(option =>
            option is HelpOption or VersionOption && ReferenceEquals(option.Action, action));
    }
}
