using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Common.Soap;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.IncomeTax.Reporting.Models;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.IncomeTax.Reporting.Soap;

/// <summary>
/// Thin SOAP client over SIRE-WS. The wire format is modeled after AFIP's published
/// SIRE specification; consumers can override the endpoint via
/// <see cref="AfipEndpoints.Sire"/> if AFIP relocates the service.
/// </summary>
public sealed class SireSoapClient
{
    /// <summary>The AFIP service identifier used to obtain TAs for SIRE.</summary>
    public const string ServiceName = "sire-ws";

    private static readonly XNamespace Sire = "http://sire.afip.gob.ar/";

    private readonly IHttpSoapInvoker _invoker;
    private readonly IOptionsMonitor<AfipOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="SireSoapClient"/> class.</summary>
    public SireSoapClient(IHttpSoapInvoker invoker, IOptionsMonitor<AfipOptions> options)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private Uri Endpoint => _options.CurrentValue.ResolveEndpoints().Sire;
    private string AgentCuit => _options.CurrentValue.Cuit;

    /// <summary>Calls the <c>emitir</c> operation.</summary>
    public async Task<WithholdingCertificateResult> IssueAsync(
        AccessTicket ticket,
        WithholdingCertificateRequest request,
        CancellationToken cancellationToken)
    {
        var body = new XElement(Sire + "emitir",
            BuildAuth(ticket),
            new XElement(Sire + "cuitAgente", AgentCuit),
            new XElement(Sire + "certificado",
                new XElement(Sire + "impuesto", (int)request.TaxCode),
                new XElement(Sire + "regimen", request.Regime),
                new XElement(Sire + "fechaRetencion", FormatDate(request.WithholdingDate)),
                new XElement(Sire + "cuitRetenido", request.WithheldCuit),
                new XElement(Sire + "importeBase", FormatDecimal(request.TaxableBase)),
                new XElement(Sire + "importeRetencion", FormatDecimal(request.WithheldAmount)),
                new XElement(Sire + "tipoComprobante", request.SourceComprobanteType),
                new XElement(Sire + "numeroComprobante", request.SourceComprobanteNumber),
                new XElement(Sire + "condicion", (int)request.Condition)));

        var response = await _invoker.InvokeAsync(Endpoint, "http://sire.afip.gob.ar/emitir", body, cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    /// <summary>Calls the <c>anular</c> operation.</summary>
    public async Task<WithholdingCertificateResult> CancelAsync(
        AccessTicket ticket,
        string certificateNumber,
        CancellationToken cancellationToken)
    {
        var body = new XElement(Sire + "anular",
            BuildAuth(ticket),
            new XElement(Sire + "cuitAgente", AgentCuit),
            new XElement(Sire + "numeroCertificado", certificateNumber));

        var response = await _invoker.InvokeAsync(Endpoint, "http://sire.afip.gob.ar/anular", body, cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    /// <summary>Calls the <c>consultar</c> operation.</summary>
    public async Task<WithholdingCertificateResult> GetAsync(
        AccessTicket ticket,
        string certificateNumber,
        CancellationToken cancellationToken)
    {
        var body = new XElement(Sire + "consultar",
            BuildAuth(ticket),
            new XElement(Sire + "cuitAgente", AgentCuit),
            new XElement(Sire + "numeroCertificado", certificateNumber));

        var response = await _invoker.InvokeAsync(Endpoint, "http://sire.afip.gob.ar/consultar", body, cancellationToken).ConfigureAwait(false);
        return ParseResult(response);
    }

    private XElement BuildAuth(AccessTicket ticket) =>
        new(Sire + "Auth",
            new XElement(Sire + "Token", ticket.Token),
            new XElement(Sire + "Sign", ticket.Sign));

    private static WithholdingCertificateResult ParseResult(XElement response)
    {
        var errors = new List<(int Code, string Message)>();
        foreach (var err in response.Descendants().Where(e => e.Name.LocalName == "error"))
        {
            var code = int.TryParse(err.Element(err.Name.Namespace + "codigo")?.Value, out var n) ? n : 0;
            var msg = err.Element(err.Name.Namespace + "mensaje")?.Value ?? string.Empty;
            errors.Add((code, msg));
        }

        var number = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "numeroCertificado")?.Value;
        var status = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "estado")?.Value;
        var issueDateRaw = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "fechaEmision")?.Value;

        DateOnly? issueDate = null;
        if (!string.IsNullOrWhiteSpace(issueDateRaw) &&
            DateOnly.TryParse(issueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            issueDate = d;
        }

        var isSuccess = errors.Count == 0 && !string.IsNullOrWhiteSpace(number);

        return new WithholdingCertificateResult(
            isSuccess,
            number,
            issueDate,
            status,
            errors);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
