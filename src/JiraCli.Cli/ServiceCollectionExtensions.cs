using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli;

/// <summary>The single registration seam of JiraCli.Cli (AD-1).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCli(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
