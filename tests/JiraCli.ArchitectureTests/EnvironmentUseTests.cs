using System.Reflection;
using JiraCli.ArchitectureTests.Fixtures.EnvironmentLeak;
using Mono.Cecil;

namespace JiraCli.ArchitectureTests;

/// <summary>
/// AD-11: no src code reads the environment for paths or settings. Calls to
/// <c>Environment.GetFolderPath</c>, <c>GetEnvironmentVariable(s)</c>, and
/// <c>ExpandEnvironmentVariables</c> are banned everywhere in src, including inside lambdas.
/// </summary>
public sealed class EnvironmentUseTests
{
    internal static readonly IReadOnlySet<string> BannedMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        "GetFolderPath",
        "GetEnvironmentVariable",
        "GetEnvironmentVariables",
        "ExpandEnvironmentVariables",
    };

    public static TheoryData<string> Projects => new(SolutionLayout.Assemblies.Keys);

    [Theory]
    [MemberData(nameof(Projects))]
    public void No_src_type_reads_the_environment(string project)
    {
        Assert.Empty(FindEnvironmentReads(SolutionLayout.Assemblies[project]));
    }

    [Fact]
    public void Rule_flags_every_banned_call_including_one_inside_an_async_lambda()
    {
        var fixtureNamespace = typeof(EnvironmentReader).Namespace;
        var fixture = typeof(EnvironmentReader).FullName;

        var violations = FindEnvironmentReads(
            typeof(EnvironmentReader).Assembly,
            outermost => outermost.Namespace == fixtureNamespace);

        Assert.Equal(
            [
                $"'{fixture}' calls System.Environment::ExpandEnvironmentVariables",
                $"'{fixture}' calls System.Environment::GetEnvironmentVariable",
                $"'{fixture}' calls System.Environment::GetEnvironmentVariables",
                $"'{fixture}' calls System.Environment::GetFolderPath",
            ],
            violations);
    }

    internal static IReadOnlyList<string> FindEnvironmentReads(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null) =>
        IlScanner.Types(assembly, outermostFilter)
            .SelectMany(type => IlScanner.Instructions(type)
                .Select(instruction => instruction.Operand)
                .OfType<MethodReference>()
                .Where(method => method.DeclaringType.FullName == "System.Environment" && BannedMembers.Contains(method.Name))
                .Select(method => $"'{IlScanner.Outermost(type).FullName}' calls System.Environment::{method.Name}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
