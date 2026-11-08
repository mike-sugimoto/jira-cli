using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Domain;

/// <summary>The single registration seam of JiraCli.Domain (AD-1).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
