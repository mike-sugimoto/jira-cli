using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Infrastructure.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_returns_the_same_collection_and_the_provider_validates()
    {
        var services = new ServiceCollection();

        var returned = services.AddInfrastructure();

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Assert.NotNull(provider);
    }
}
