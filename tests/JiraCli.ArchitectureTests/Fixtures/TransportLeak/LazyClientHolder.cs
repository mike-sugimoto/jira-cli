using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>
/// Self-test fixture for <see cref="TransportSurfaceTests"/>: the only mention of <c>HttpClient</c>
/// is a generic argument of a field's type. Never used at run time.
/// </summary>
internal sealed class LazyClientHolder
{
#pragma warning disable CS0649 // Never assigned: the field exists only to be scanned.
    public Lazy<HttpClient>? Client;
#pragma warning restore CS0649
}
