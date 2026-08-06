using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Afip.Arca.Sdk.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Afip.Arca.Sdk.Common.Soap;

/// <summary>
/// Default <see cref="IHttpSoapInvoker"/> implementation backed by an
/// <see cref="HttpClient"/> obtained from <c>IHttpClientFactory</c>.
/// </summary>
public sealed class HttpSoapInvoker : IHttpSoapInvoker
{
    /// <summary>Logical name of the <see cref="HttpClient"/> registered for the SDK.</summary>
    public const string HttpClientName = "Afip.Arca.Sdk";

    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpSoapInvoker> _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpSoapInvoker"/> class.</summary>
    public HttpSoapInvoker(IHttpClientFactory httpClientFactory, ILogger<HttpSoapInvoker> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<XElement> InvokeAsync(
        Uri endpoint,
        string soapAction,
        XElement body,
        CancellationToken cancellationToken)
    {
        if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
        if (body is null) throw new ArgumentNullException(nameof(body));
        // Some AFIP services (notably WSAA loginCms) require an EMPTY SOAPAction
        // header per their WSDL — so we accept null/empty here and only reject `null`.
        soapAction ??= string.Empty;

        var envelope = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SoapNs + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", SoapNs.NamespaceName),
                new XElement(SoapNs + "Body", body)));

        var serialized = SerializeXml(envelope);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(serialized, Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", "\"" + soapAction + "\"");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        _logger.LogDebug("Invoking SOAP action {Action} at {Endpoint}", soapAction, endpoint);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new AfipTransportException("HTTP error while calling " + endpoint, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AfipTransportException("Timeout calling " + endpoint, innerException: ex);
        }

        var responseText = await response.Content
#if NET8_0_OR_GREATER
            .ReadAsStringAsync(cancellationToken)
#else
            .ReadAsStringAsync()
#endif
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var fault = TryParseFault(responseText);
            if (fault is not null)
            {
                throw new AfipTransportException(
                    "SOAP fault: " + fault.FaultString,
                    (int)response.StatusCode,
                    fault.FaultCode);
            }

            throw new AfipTransportException(
                "HTTP " + (int)response.StatusCode + " from " + endpoint,
                (int)response.StatusCode);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(responseText);
        }
        catch (XmlException ex)
        {
            throw new AfipTransportException("Malformed SOAP response from " + endpoint, innerException: ex);
        }

        var responseBody = document.Root?.Element(SoapNs + "Body");
        if (responseBody is null)
        {
            throw new AfipTransportException("SOAP envelope missing Body element");
        }

        var fault2 = responseBody.Element(SoapNs + "Fault");
        if (fault2 is not null)
        {
            var parsed = ParseFault(fault2);
            throw new AfipTransportException("SOAP fault: " + parsed.FaultString, soapFaultCode: parsed.FaultCode);
        }

        var firstChild = responseBody.Elements().FirstOrDefault();
        return firstChild ?? throw new AfipTransportException("SOAP Body contained no payload");
    }

    private static string SerializeXml(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            Indent = false,
        };
        using var sw = new StringWriterUtf8();
        using (var xw = XmlWriter.Create(sw, settings))
        {
            doc.WriteTo(xw);
        }
        return sw.ToString();
    }

    private static SoapFault? TryParseFault(string body)
    {
        try
        {
            var doc = XDocument.Parse(body);
            var fault = doc.Descendants(SoapNs + "Fault").FirstOrDefault();
            return fault is null ? null : ParseFault(fault);
        }
        catch
        {
            return null;
        }
    }

    private static SoapFault ParseFault(XElement fault)
    {
        var code = fault.Element("faultcode")?.Value ?? fault.Element(SoapNs + "faultcode")?.Value ?? "Unknown";
        var msg = fault.Element("faultstring")?.Value ?? fault.Element(SoapNs + "faultstring")?.Value ?? "(no message)";
        var detail = fault.Element("detail")?.ToString() ?? fault.Element(SoapNs + "detail")?.ToString();
        return new SoapFault(code, msg, detail);
    }

    private sealed class StringWriterUtf8 : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
