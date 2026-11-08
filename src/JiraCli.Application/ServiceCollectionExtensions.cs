using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Application;

/// <summary>The single registration seam of JiraCli.Application (AD-1).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
