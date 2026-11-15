using System.Net;

namespace Atlassian.Common.Tests.Transport;

internal sealed record RecordedRequest(HttpMethod Method, string AbsoluteUri, string? Authorization, string Accept, bool HasContent);
