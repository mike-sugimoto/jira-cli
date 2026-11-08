using Atlassian.Common;
using JiraCli.Application;
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
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services
            .AddAtlassianCommon()
            .AddDomain()
            .AddApplication()
            .AddInfrastructure()
            .AddCli();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
