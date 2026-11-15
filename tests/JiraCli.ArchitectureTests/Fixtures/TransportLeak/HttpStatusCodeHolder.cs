using System.Net.Security;

namespace JiraCli.ArchitectureTests.Fixtures.TransportLeak;

/// <summary>Plain result type for <see cref="LambdaInvokerUser"/>, so the lambda's signature names no banned type.</summary>
internal sealed record HttpStatusCodeHolder(int Code);
