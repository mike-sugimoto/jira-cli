using Microsoft.Extensions.DependencyInjection;

namespace Atlassian.Common.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAtlassianCommon_returns_the_same_collection_and_the_provider_validates()
    {
        var services = new ServiceCollection();

        var returned = services.AddAtlassianCommon();

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Assert.NotNull(provider);
    }
}
