using System.Reflection;
using JiraCli.ArchitectureTests.Fixtures;
using Mono.Cecil;

namespace JiraCli.ArchitectureTests;

/// <summary>
/// AD-13 / AD-20: only the designated writers touch <see cref="System.Console"/>. The rule bans
/// every <c>System.Console</c> member, not only output, and attributes compiler-generated code
/// (lambdas, async state machines) to its outermost declaring type.
/// </summary>
public sealed class ConsoleUseTests
{
    /// <summary>
    /// The single allow-list of types that may use <c>System.Console</c>.
    /// Story 1.4 adds the envelope writer and the warning writer.
    /// </summary>
    internal static readonly IReadOnlySet<string> AllowedConsoleUsers = new HashSet<string>(StringComparer.Ordinal)
    {
        "JiraCli.Cli.PlainTextOutput",
    };

    public static TheoryData<string> Projects => new(SolutionLayout.Assemblies.Keys);

    [Theory]
    [MemberData(nameof(Projects))]
    public void Only_allow_listed_types_use_System_Console(string project)
    {
        var violations = FindConsoleUsers(SolutionLayout.Assemblies[project])
            .Where(type => !AllowedConsoleUsers.Contains(type));

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_allow_listed_type_exists_in_src()
    {
        var srcTypes = SolutionLayout.Assemblies.Values
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(AllowedConsoleUsers, allowed => Assert.Contains(allowed, srcTypes));
    }

    [Fact]
    public void Rule_flags_console_use_inside_an_async_lambda_as_the_outer_type()
    {
        var fixtureNamespace = typeof(AsyncLambdaConsoleViolation).Namespace;

        var violations = FindConsoleUsers(
            typeof(AsyncLambdaConsoleViolation).Assembly,
            outermost => outermost.Namespace == fixtureNamespace);

        Assert.Equal([typeof(AsyncLambdaConsoleViolation).FullName!], violations);
    }

    /// <summary>Returns the distinct outermost types whose IL references <c>System.Console</c>.</summary>
    internal static IReadOnlyList<string> FindConsoleUsers(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null) =>
        IlScanner.Types(assembly, outermostFilter)
            .Where(type => IlScanner.Instructions(type).Any(instruction => IlScanner.ReferencedTypeName(instruction) == "System.Console"))
            .Select(type => IlScanner.Outermost(type).FullName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
