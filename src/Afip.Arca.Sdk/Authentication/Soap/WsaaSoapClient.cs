using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Common.Soap;
using Afip.Arca.Sdk.Configuration;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Authentication.Soap;

/// <summary>
/// Thin client for the WSAA <c>loginCms</c> SOAP operation. Returns a parsed
/// <see cref="AccessTicket"/>.
/// </summary>
public sealed class WsaaSoapClient
{
    private static readonly XNamespace WsaaNs = "http://wsaa.view.sua.dvadac.desein.afip.gov";
    private readonly IHttpSoapInvoker _invoker;
    private readonly IOptionsMonitor<AfipOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="WsaaSoapClient"/> class.</summary>
    public WsaaSoapClient(IHttpSoapInvoker invoker, IOptionsMonitor<AfipOptions> options)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Calls <c>loginCms</c> with the given Base64-encoded CMS payload.</summary>
    /// <param name="service">AFIP service the TA is being requested for.</param>
    /// <param name="cuit">CUIT of the contributor.</param>
    /// <param name="cmsBase64">Base64-encoded CMS PKCS#7 signed TRA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<AccessTicket> LoginCmsAsync(
        string service,
        string cuit,
        string cmsBase64,
        CancellationToken cancellationToken)
    {
        var endpoint = _options.CurrentValue.ResolveEndpoints().Wsaa;

        var body = new XElement(WsaaNs + "loginCms",
            new XElement(WsaaNs + "in0", cmsBase64));

        var response = await _invoker.InvokeAsync(endpoint, soapAction: "", body, cancellationToken).ConfigureAwait(false);

        var loginCmsReturn = response.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "loginCmsReturn", StringComparison.Ordinal))?
            .Value;

        if (string.IsNullOrWhiteSpace(loginCmsReturn))
        {
            throw new AfipAuthenticationException("WSAA response did not contain loginCmsReturn.");
        }

        return ParseLoginTicketResponse(service, cuit, loginCmsReturn!);
    }

    private static AccessTicket ParseLoginTicketResponse(string service, string cuit, string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw new AfipAuthenticationException("WSAA returned malformed loginTicketResponse.", ex);
        }

        var header = doc.Root?.Element("header");
        var credentials = doc.Root?.Element("credentials");
        var token = credentials?.Element("token")?.Value;
        var sign = credentials?.Element("sign")?.Value;
        var generationTime = header?.Element("generationTime")?.Value;
        var expirationTime = header?.Element("expirationTime")?.Value;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sign) ||
            string.IsNullOrWhiteSpace(generationTime) || string.IsNullOrWhiteSpace(expirationTime))
        {
            throw new AfipAuthenticationException("loginTicketResponse is missing required fields.");
        }

        return new AccessTicket(
            service,
            cuit,
            token!,
            sign!,
            DateTimeOffset.Parse(generationTime!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal),
            DateTimeOffset.Parse(expirationTime!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal));
    }
}
