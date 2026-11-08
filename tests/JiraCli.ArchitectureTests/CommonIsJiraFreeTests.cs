using System.Reflection;
using JiraCli.ArchitectureTests.Fixtures.CommonLeak;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace JiraCli.ArchitectureTests;

/// <summary>AD-2: <c>Atlassian.Common</c> holds no Jira type, URL, JQL, or field name.</summary>
public sealed class CommonIsJiraFreeTests
{
    /// <summary>Forbidden tokens, matched case-insensitively.</summary>
    internal static readonly IReadOnlyList<string> ForbiddenTokens = ["jira", "jql", "issue", "customfield", "/rest/api"];

    [Fact]
    public void Atlassian_Common_contains_no_Jira_tokens()
    {
        var violations = FindJiraTokens(SolutionLayout.Assemblies[SolutionLayout.Common]);

        Assert.Empty(violations);
    }

    [Fact]
    public void Rule_names_the_offending_type_member_and_literal()
    {
        var fixtureNamespace = typeof(IssueFetcher).Namespace;
        var fixture = typeof(IssueFetcher).FullName;

        var violations = FindJiraTokens(
            typeof(IssueFetcher).Assembly,
            outermost => outermost.Namespace == fixtureNamespace);

        Assert.Contains($"type '{fixture}'", violations);
        Assert.Contains($"member '{fixture}::BuildJql'", violations);
        Assert.Contains($"member '{fixture}::CustomFieldId'", violations);
        Assert.Contains($"literal \"/REST/API/3/search\" in '{fixture}::Path'", violations);
        Assert.Contains($"constant \"/rest/api/3/myself\" of '{fixture}::SelfPath'", violations);
        Assert.Contains($"default \"order by JQL\" of parameter 'query' in '{fixture}::Find'", violations);
        Assert.Contains($"attribute argument \"Jira helper\" on '{fixture}::Describe'", violations);
        Assert.Contains($"parameter 'issueKey' in '{fixture}::Load'", violations);
        Assert.Contains($"generic parameter 'TIssue' in '{fixture}::Map'", violations);
    }

    internal static IReadOnlyList<string> FindJiraTokens(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null)
    {
        var violations = new List<string>();
        var module = IlScanner.Module(assembly);
        AddAttributeViolations(violations, module.Assembly, $"assembly '{module.Assembly.Name.Name}'");
        AddAttributeViolations(violations, module, $"module '{module.Name}'");

        foreach (var type in IlScanner.Types(module, outermostFilter))
        {
            if (ContainsToken(type.Namespace))
            {
                violations.Add($"namespace '{type.Namespace}'");
            }

            if (ContainsToken(type.Name))
            {
                violations.Add($"type '{type.FullName}'");
            }

            AddAttributeViolations(violations, type, $"'{type.FullName}'");
            AddGenericParameterViolations(violations, type, $"'{type.FullName}'");

            IEnumerable<IMemberDefinition> members = [.. type.Methods, .. type.Fields, .. type.Properties, .. type.Events];
            foreach (var member in members)
            {
                if (ContainsToken(member.Name))
                {
                    violations.Add($"member '{type.FullName}::{member.Name}'");
                }

                AddAttributeViolations(violations, member, $"'{type.FullName}::{member.Name}'");
            }

            foreach (var field in type.Fields.Where(field => field.HasConstant && field.Constant is string))
            {
                var value = (string)field.Constant;
                if (ContainsToken(value))
                {
                    violations.Add($"constant \"{value}\" of '{type.FullName}::{field.Name}'");
                }
            }

            foreach (var method in type.Methods)
            {
                var where = $"'{type.FullName}::{method.Name}'";
                AddGenericParameterViolations(violations, method, where);

                foreach (var parameter in method.Parameters)
                {
                    if (ContainsToken(parameter.Name))
                    {
                        violations.Add($"parameter '{parameter.Name}' in {where}");
                    }

                    if (parameter.HasConstant && parameter.Constant is string value && ContainsToken(value))
                    {
                        violations.Add($"default \"{value}\" of parameter '{parameter.Name}' in {where}");
                    }

                    AddAttributeViolations(violations, parameter, $"parameter '{parameter.Name}' in {where}");
                }

                if (method.HasBody)
                {
                    violations.AddRange(method.Body.Instructions
                        .Where(instruction => instruction.OpCode == OpCodes.Ldstr && ContainsToken((string)instruction.Operand))
                        .Select(instruction => $"literal \"{instruction.Operand}\" in {where}"));
                }
            }
        }

        return violations.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AddGenericParameterViolations(List<string> violations, IGenericParameterProvider provider, string where) =>
        violations.AddRange(provider.GenericParameters
            .Where(parameter => ContainsToken(parameter.Name))
            .Select(parameter => $"generic parameter '{parameter.Name}' in {where}"));

    private static void AddAttributeViolations(List<string> violations, Mono.Cecil.ICustomAttributeProvider provider, string where)
    {
        if (!provider.HasCustomAttributes)
        {
            return;
        }

        foreach (var attribute in provider.CustomAttributes)
        {
            IEnumerable<CustomAttributeArgument> arguments =
            [
                .. attribute.ConstructorArguments,
                .. attribute.Fields.Select(named => named.Argument),
                .. attribute.Properties.Select(named => named.Argument),
            ];

            violations.AddRange(arguments
                .SelectMany(StringValues)
                .Where(ContainsToken)
                .Select(value => $"attribute argument \"{value}\" on {where}"));
        }
    }

    private static IEnumerable<string> StringValues(CustomAttributeArgument argument) => argument.Value switch
    {
        string text => [text],
        CustomAttributeArgument inner => StringValues(inner),
        CustomAttributeArgument[] items => items.SelectMany(StringValues),
        _ => [],
    };

    private static bool ContainsToken(string? text) =>
        text is not null && ForbiddenTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
}
