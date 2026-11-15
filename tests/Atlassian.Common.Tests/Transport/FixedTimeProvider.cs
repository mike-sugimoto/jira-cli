using System.Net;

namespace Atlassian.Common.Tests.Transport;

/// <summary>A clock frozen at one instant; timers still run on the system clock.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
