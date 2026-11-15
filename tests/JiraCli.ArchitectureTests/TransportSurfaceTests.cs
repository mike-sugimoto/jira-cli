using System.Reflection;
using Atlassian.Common.Transport;
using JiraCli.ArchitectureTests.Fixtures.TransportLeak;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace JiraCli.ArchitectureTests;

/// <summary>
/// AD-5 / AD-12 / AD-19: the transport surface can only express a GET relative to the base URL,
/// only the transport implementation touches <c>HttpClient</c>, and nothing disables TLS validation.
/// </summary>
public sealed class TransportSurfaceTests
{
    /// <summary>Types that can send arbitrary network requests; only the transport implementation may use them.</summary>
    internal static readonly IReadOnlySet<string> BannedNetworkTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpMessageInvoker",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest",
        "System.Net.WebClient",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.Sockets.Socket",
    };

    private const string TransportImplementation = "Atlassian.Common.Transport.HttpAtlassianTransport";

    internal static readonly IReadOnlyList<string> BannedCertificateMembers =
    [
        "ServerCertificateCustomValidationCallback",
        "RemoteCertificateValidationCallback",
        "DangerousAcceptAnyServerCertificateValidator",
    ];

    private static readonly IReadOnlyList<string> UrlOrVerbNames = ["url", "uri", "method", "verb"];

    private static readonly IReadOnlySet<Type> UrlOrVerbTypes = new HashSet<Type> { typeof(string), typeof(Uri), typeof(HttpMethod) };

    public static TheoryData<string> Projects => new(SolutionLayout.Assemblies.Keys);

    [Fact]
    public void IAtlassianTransport_has_exactly_one_GET_method()
    {
        var type = typeof(IAtlassianTransport);
        var method = Assert.Single(type.GetMethods());

        Assert.Equal("GetAsync", method.Name);
        Assert.Equal(typeof(Task<TransportResponse>), method.ReturnType);
        Assert.Equal([typeof(RelativeRequest), typeof(CancellationToken)], method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Empty(type.GetProperties());
        Assert.Empty(type.GetEvents());
        Assert.Empty(type.GetInterfaces());
    }

    [Fact]
    public void RelativeRequest_exposes_no_url_and_no_method()
    {
        Assert.Empty(FindUrlOrVerbMembers(typeof(RelativeRequest)));
    }

    [Fact]
    public void Url_and_verb_rule_flags_each_offending_member()
    {
        var violations = FindUrlOrVerbMembers(typeof(UrlAndVerbRequest));

        Assert.Contains("Url", violations);
        Assert.Contains("Target", violations);
        Assert.Contains("Method", violations);
        Assert.Contains("Verb", violations);
    }

    [Fact]
    public void Only_the_transport_implementation_uses_a_network_client()
    {
        var users = SolutionLayout.Assemblies.Values
            .SelectMany(assembly => FindNetworkUsers(assembly))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal([TransportImplementation], users);
    }

    [Fact]
    public void Network_rule_flags_lambda_uses_invoker_uses_and_generic_wrappers()
    {
        var fixtureNamespace = typeof(LambdaHttpClientUser).Namespace;

        var violations = FindNetworkUsers(
            typeof(LambdaHttpClientUser).Assembly,
            outermost => outermost.Namespace == fixtureNamespace);

        Assert.Equal(
            new[] { typeof(LambdaHttpClientUser), typeof(LambdaInvokerUser), typeof(LazyClientHolder) }
                .Select(type => type.FullName!)
                .Order(StringComparer.Ordinal),
            violations);
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void No_src_code_bypasses_certificate_validation(string project)
    {
        Assert.Empty(FindCertificateBypasses(SolutionLayout.Assemblies[project]));
    }

    [Fact]
    public void Certificate_rule_flags_every_bypass_inside_a_lambda()
    {
        var fixture = typeof(LambdaCertificateBypass).FullName;

        var violations = FindCertificateBypasses(
            typeof(LambdaCertificateBypass).Assembly,
            outermost => outermost.FullName == fixture);

        Assert.Equal(
            [
                $"'{fixture}' references DangerousAcceptAnyServerCertificateValidator",
                $"'{fixture}' references RemoteCertificateValidationCallback",
                $"'{fixture}' references ServerCertificateCustomValidationCallback",
            ],
            violations);
    }

    /// <summary>Public members of the type that could carry a raw URL or an HTTP method, by type or by name.</summary>
    internal static IReadOnlyList<string> FindUrlOrVerbMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        IEnumerable<(string Name, Type? MemberType)> members =
        [
            .. type.GetProperties(flags).Select(property => (property.Name, (Type?)property.PropertyType)),
            .. type.GetFields(flags).Select(field => (field.Name, (Type?)field.FieldType)),
            .. type.GetMethods(flags).Where(method => !method.IsSpecialName).Select(method => (method.Name, (Type?)method.ReturnType)),
            .. type.GetEvents(flags).Select(@event => (@event.Name, @event.EventHandlerType)),
        ];

        return members
            .Where(member =>
                (member.MemberType is not null && UrlOrVerbTypes.Contains(member.MemberType))
                || UrlOrVerbNames.Any(name => member.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(member => member.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Outermost types whose base types, interfaces, fields, properties, signatures, locals, or IL
    /// mention a banned network type, including inside generic arguments (<c>Lazy&lt;HttpClient&gt;</c>).
    /// </summary>
    internal static IReadOnlyList<string> FindNetworkUsers(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null) =>
        IlScanner.Types(assembly, outermostFilter)
            .Where(UsesNetworkType)
            .Select(type => IlScanner.Outermost(type).FullName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>One entry per outermost type and banned certificate member it references.</summary>
    internal static IReadOnlyList<string> FindCertificateBypasses(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null) =>
        IlScanner.Types(assembly, outermostFilter)
            .SelectMany(type => IlScanner.Instructions(type)
                .Select(OperandText)
                .OfType<string>()
                .SelectMany(text => BannedCertificateMembers.Where(banned => text.Contains(banned, StringComparison.Ordinal)))
                .Select(banned => $"'{IlScanner.Outermost(type).FullName}' references {banned}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool UsesNetworkType(TypeDefinition type)
    {
        if (BaseTypes(type).Any(Mentions)
            || type.Interfaces.Any(implementation => Mentions(implementation.InterfaceType))
            || type.Fields.Any(field => Mentions(field.FieldType))
            || type.Properties.Any(property => Mentions(property.PropertyType))
            || type.Events.Any(@event => Mentions(@event.EventType)))
        {
            return true;
        }

        return type.Methods.Any(method =>
            Mentions(method.ReturnType)
            || method.Parameters.Any(parameter => Mentions(parameter.ParameterType))
            || (method.HasBody
                && (method.Body.Variables.Any(variable => Mentions(variable.VariableType))
                    || method.Body.Instructions.Any(instruction => OperandMentions(instruction.Operand)))));
    }

    /// <summary>The base-type chain, followed through types that resolve; stops quietly where one does not.</summary>
    private static IEnumerable<TypeReference> BaseTypes(TypeDefinition type)
    {
        for (var current = type.BaseType; current is not null;)
        {
            yield return current;

            TypeDefinition? resolved;
            try
            {
                resolved = current.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                resolved = null;
            }

            current = resolved?.BaseType;
        }
    }

    private static bool OperandMentions(object? operand) => operand switch
    {
        TypeReference type => Mentions(type),
        MethodReference method => Mentions(method.DeclaringType)
            || Mentions(method.ReturnType)
            || method.Parameters.Any(parameter => Mentions(parameter.ParameterType))
            || (method is GenericInstanceMethod generic && generic.GenericArguments.Any(Mentions)),
        FieldReference field => Mentions(field.DeclaringType) || Mentions(field.FieldType),
        _ => false,
    };

    /// <summary>True when the type, its element type, or any generic argument is a banned network type.</summary>
    private static bool Mentions(TypeReference? type) => type switch
    {
        null => false,
        GenericInstanceType generic => Mentions(generic.ElementType) || generic.GenericArguments.Any(Mentions),
        TypeSpecification specification => Mentions(specification.ElementType),
        _ => BannedNetworkTypes.Contains(type.FullName),
    };

    private static string? OperandText(Instruction instruction) => instruction.Operand switch
    {
        MemberReference member => member.FullName,
        _ => null,
    };
}
