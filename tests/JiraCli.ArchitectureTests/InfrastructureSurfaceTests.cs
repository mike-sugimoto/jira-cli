using NetArchTest.Rules;

namespace JiraCli.ArchitectureTests;

/// <summary>AD-1: Cli uses Infrastructure only through its <c>AddInfrastructure()</c> method.</summary>
public sealed class InfrastructureSurfaceTests
{
    [Fact]
    public void Only_CompositionRoot_in_Cli_depends_on_Infrastructure()
    {
        var dependents = Types.InAssembly(SolutionLayout.Assemblies[SolutionLayout.Cli])
            .That()
            .HaveDependencyOnAny(SolutionLayout.Infrastructure)
            .GetTypes()
            .Select(type => SolutionLayout.Outermost(type.ReflectionType).FullName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["JiraCli.Cli.CompositionRoot"], dependents);
    }

    [Fact]
    public void Infrastructure_exports_only_its_registration_class()
    {
        var exported = SolutionLayout.Assemblies[SolutionLayout.Infrastructure]
            .GetExportedTypes()
            .Select(type => type.FullName);

        Assert.Equal(["JiraCli.Infrastructure.ServiceCollectionExtensions"], exported);
    }
}
