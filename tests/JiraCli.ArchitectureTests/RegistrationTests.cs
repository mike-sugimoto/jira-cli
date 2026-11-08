using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace JiraCli.ArchitectureTests;

/// <summary>AD-1: each src project exposes exactly one public <c>Add…(IServiceCollection)</c> method.</summary>
public sealed class RegistrationTests
{
    public static TheoryData<string> Projects => new(SolutionLayout.Assemblies.Keys);

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_exposes_exactly_one_registration_method(string project)
    {
        var registrations = SolutionLayout.Assemblies[project]
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetParameters() is [var first, ..] && first.ParameterType == typeof(IServiceCollection))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}");

        var expected = $"{project}.ServiceCollectionExtensions.{SolutionLayout.RegistrationMethods[project]}";
        Assert.Equal([expected], registrations);
    }
}
