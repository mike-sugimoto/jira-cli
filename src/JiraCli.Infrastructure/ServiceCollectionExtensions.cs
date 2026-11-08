using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Infrastructure;

/// <summary>The single registration seam of JiraCli.Infrastructure (AD-1).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
