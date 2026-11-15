namespace Atlassian.Common.Transport;

/// <summary>
/// What a request expects back. It never selects an HTTP method. Today only <see cref="Json"/>
/// exists and the transport does not read the kind: every request uses the JSON timeout and
/// <c>Accept: application/json</c>. Adding a kind (Story 4.2's <c>Download</c>) must make the
/// transport switch on it to pick the timeout profile and <c>Accept</c> header.
/// </summary>
public enum ResponseKind
{
    /// <summary>A JSON body, read in full within the 30-second JSON timeout.</summary>
    Json,
}
