namespace Atlassian.Common.Transport;

/// <summary>
/// Turns a <see cref="RelativeRequest"/> into the absolute URI to send. The transport attaches
/// credentials to whatever this returns, so an implementation must never return a URI outside the
/// base URL. Production always uses <see cref="RequestUriBuilder"/>; other implementations exist
/// only in tests.
/// </summary>
internal interface IRequestUriBuilder
{
    Uri Build(RelativeRequest request);
}
