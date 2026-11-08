using Microsoft.Extensions.DependencyInjection;

namespace Atlassian.Common;

/// <summary>The single registration seam of Atlassian.Common (AD-1).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlassianCommon(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
