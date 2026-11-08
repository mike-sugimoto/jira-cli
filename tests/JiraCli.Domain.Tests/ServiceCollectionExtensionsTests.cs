using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Domain.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDomain_returns_the_same_collection_and_the_provider_validates()
    {
        var services = new ServiceCollection();

        var returned = services.AddDomain();

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Assert.NotNull(provider);
    }
}
