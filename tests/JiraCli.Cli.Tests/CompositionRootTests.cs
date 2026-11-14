using Atlassian.Common.Config;
using JiraCli.Cli.Config;
using JiraCli.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.Cli.Tests;

public sealed class CompositionRootTests
{
    private static ConfigLoadResult SampleFailure() =>
        ConfigLoadResult.Failed(new ConfigLoadFailure(ConfigFileFailureKind.Missing, null, "No config file.", "Create it.", "/x/config.json"));

    private static ConfigLoadResult SampleSuccess() =>
        ConfigLoadResult.Success(
            new ConnectionSettings("site", "me@example.com", "token"),
            new AccessSettings([ProjectKey.Parse("ABC")], []));

    private interface IMissingDependency;

    private sealed class NeedsMissingDependency(IMissingDependency dependency)
    {
        public IMissingDependency Dependency { get; } = dependency;
    }

    private sealed class ScopedService;

    [Fact]
    public void Real_Build_loads_config_from_the_fixed_path_and_registers_the_result_without_throwing()
    {
        using var provider = CompositionRoot.Build();

        var result = provider.GetRequiredService<ConfigLoadResult>();

        Assert.NotNull(result);
        Assert.Same(result, provider.GetRequiredService<ConfigLoadResult>());
    }

    [Fact]
    public void A_load_failure_is_registered_as_a_value()
    {
        var failure = SampleFailure();

        using var provider = CompositionRoot.Build(failure, configure: null);

        Assert.Same(failure, provider.GetRequiredService<ConfigLoadResult>());
    }

    [Fact]
    public void A_success_is_registered_as_a_singleton_across_scopes()
    {
        var success = SampleSuccess();
        using var provider = CompositionRoot.Build(success, configure: null);

        using var scope = provider.CreateScope();

        Assert.Same(success, scope.ServiceProvider.GetRequiredService<ConfigLoadResult>());
    }

    [Fact]
    public void ValidateOnBuild_rejects_a_service_with_a_missing_dependency()
    {
        var exception = Assert.ThrowsAny<Exception>(() =>
            CompositionRoot.Build(SampleSuccess(), services => services.AddSingleton<NeedsMissingDependency>()));

        Assert.Contains(nameof(IMissingDependency), exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateScopes_rejects_resolving_a_scoped_service_from_the_root()
    {
        using var provider = CompositionRoot.Build(SampleSuccess(), services => services.AddScoped<ScopedService>());

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ScopedService>());

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ScopedService>());
    }
}
