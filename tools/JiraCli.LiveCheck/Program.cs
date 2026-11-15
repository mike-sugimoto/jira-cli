using Atlassian.Common.Transport;
using JiraCli.Cli.Config;
using JiraCli.LiveCheck;

// Engineer-run live check (Story 1.3b): no arguments, no environment. See docs/live-check.md.
return await LiveCheckRunner.RunAsync(
    JiraCliConfig.Load(),
    settings => AtlassianTransport.Create(settings, LiveCheckRunner.Product),
    Console.Out,
    CancellationToken.None);
