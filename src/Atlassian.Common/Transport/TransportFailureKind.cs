using System.Net;
using System.Text;

namespace Atlassian.Common.Transport;

/// <summary>Why a transport call did not produce a 2xx response.</summary>
public enum TransportFailureKind
{
    /// <summary>The server answered with a non-2xx status (3xx included; redirects are never followed).</summary>
    Status,

    /// <summary>No complete response, body included, arrived within the timeout.</summary>
    Timeout,

    /// <summary>The connection failed before any status arrived.</summary>
    Connection,
}
