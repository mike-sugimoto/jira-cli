using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Atlassian.Common.Config;

namespace Atlassian.Common.Transport;

/// <summary>Creates the production <see cref="IAtlassianTransport"/>.</summary>
public static class AtlassianTransport
{
    /// <summary>
    /// A transport for the base URL that <see cref="AtlassianEndpoint"/> picks for these settings
    /// and caller-supplied product segment. It sends nothing until called.
    /// </summary>
    public static IAtlassianTransport Create(ConnectionSettings settings, string product, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(product);

        // Validate the endpoint before creating the handler, so a bad product leaks nothing.
        _ = AtlassianEndpoint.For(settings, product);

        return new HttpAtlassianTransport(
            settings,
            product,
            HttpAtlassianTransport.CreateProductionHandler(),
            HttpAtlassianTransport.JsonTimeout,
            timeProvider ?? TimeProvider.System);
    }
}
