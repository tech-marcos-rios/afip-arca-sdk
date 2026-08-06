using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Afip.Arca.Sdk.Common.Soap;

/// <summary>
/// Abstraction over a SOAP 1.1 request/response over HTTP. The concrete implementation
/// is responsible for wrapping the payload in a SOAP envelope, setting the SOAPAction
/// header, applying retry/timeout policies and parsing faults.
/// </summary>
public interface IHttpSoapInvoker
{
    /// <summary>
    /// Send a SOAP request and return the response body element (the contents of the
    /// SOAP <c>Body</c>, without the envelope wrapper).
    /// </summary>
    /// <param name="endpoint">Absolute URL of the SOAP service.</param>
    /// <param name="soapAction">Value for the <c>SOAPAction</c> HTTP header.</param>
    /// <param name="body">The XML to embed inside the SOAP <c>Body</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first XML element found inside the response <c>Body</c>.</returns>
    Task<XElement> InvokeAsync(
        Uri endpoint,
        string soapAction,
        XElement body,
        CancellationToken cancellationToken);
}
