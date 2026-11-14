using Atlassian.Common;
using JiraCli.Application;
using JiraCli.Cli.Config;
using JiraCli.Domain;
using JiraCli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli;

/// <summary>
/// The hand-built DI container. This is the only Cli type allowed to depend on
/// JiraCli.Infrastructure, and only through its <c>AddInfrastructure()</c> method (AD-1).
/// </summary>
internal static class CompositionRoot
{
    /// <summary>
    /// Loads the config once from its fixed location and builds the container around the result.
    /// A load failure is registered as a value and never throws here.
    /// </summary>
    public static ServiceProvider Build() => Build(JiraCliConfig.Load(), configure: null);

    /// <summary>Test seam: builds with a given config result and optional extra registrations.</summary>
    internal static ServiceProvider Build(ConfigLoadResult config, Action<IServiceCollection>? configure)
    {
        ArgumentNullException.ThrowIfNull(config);

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services
            .AddAtlassianCommon()
            .AddDomain()
            .AddApplication()
            .AddInfrastructure()
            .AddCli();
        configure?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
