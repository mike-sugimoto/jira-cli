using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCli_returns_the_same_collection_and_the_provider_validates()
    {
        var services = new ServiceCollection();

        var returned = services.AddCli();

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Assert.NotNull(provider);
    }
}
