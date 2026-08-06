using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Common.Soap;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.Invoicing.Models;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Invoicing.Soap;

/// <summary>
/// Thin SOAP client over the WSFEv1 service. Translates between the domain model and
/// the wire format. Does not handle authentication retries — that is up to the
/// service layer.
/// </summary>
public sealed class WsfeSoapClient
{
    /// <summary>The AFIP service identifier used to obtain TAs for this client.</summary>
    public const string ServiceName = "wsfe";

    private static readonly XNamespace Ar = "http://ar.gov.afip.dif.FEV1/";

    private readonly IHttpSoapInvoker _invoker;
    private readonly IOptionsMonitor<AfipOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="WsfeSoapClient"/> class.</summary>
    public WsfeSoapClient(IHttpSoapInvoker invoker, IOptionsMonitor<AfipOptions> options)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private Uri Endpoint => _options.CurrentValue.ResolveEndpoints().Wsfev1;
    private string Cuit => _options.CurrentValue.Cuit;

    /// <summary>Calls <c>FEDummy</c> for health-check purposes.</summary>
    public async Task<(string AppServer, string DbServer, string AuthServer)> DummyAsync(CancellationToken cancellationToken)
    {
        var body = new XElement(Ar + "FEDummy");
        var response = await _invoker.InvokeAsync(Endpoint, "http://ar.gov.afip.dif.FEV1/FEDummy", body, cancellationToken).ConfigureAwait(false);
        var result = response.Descendants(Ar + "FEDummyResult").FirstOrDefault() ?? response;
        return (
            result.Element(Ar + "AppServer")?.Value ?? "?",
            result.Element(Ar + "DbServer")?.Value ?? "?",
            result.Element(Ar + "AuthServer")?.Value ?? "?");
    }

    /// <summary>Calls <c>FECompUltimoAutorizado</c> to find the last authorized number.</summary>
    public async Task<long> GetLastAuthorizedNumberAsync(
        AccessTicket ticket,
        InvoiceType type,
        int pointOfSale,
        CancellationToken cancellationToken)
    {
        var body = new XElement(Ar + "FECompUltimoAutorizado",
            BuildAuth(ticket),
            new XElement(Ar + "PtoVta", pointOfSale),
            new XElement(Ar + "CbteTipo", (int)type));

        var response = await _invoker.InvokeAsync(Endpoint,
            "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado", body, cancellationToken).ConfigureAwait(false);

        var result = response.Descendants(Ar + "FECompUltimoAutorizadoResult").FirstOrDefault() ?? response;
        ThrowOnErrors(result);
        var cbteNro = result.Element(Ar + "CbteNro")?.Value;
        return long.TryParse(cbteNro, out var n) ? n : 0L;
    }

    /// <summary>Calls <c>FECAESolicitar</c> to authorize a comprobante.</summary>
    public async Task<InvoiceAuthorizationResult> AuthorizeAsync(
        AccessTicket ticket,
        Invoice invoice,
        long invoiceNumber,
        CancellationToken cancellationToken)
    {
        var body = new XElement(Ar + "FECAESolicitar",
            BuildAuth(ticket),
            new XElement(Ar + "FeCAEReq",
                new XElement(Ar + "FeCabReq",
                    new XElement(Ar + "CantReg", 1),
                    new XElement(Ar + "PtoVta", invoice.PointOfSale),
                    new XElement(Ar + "CbteTipo", (int)invoice.Type)),
                new XElement(Ar + "FeDetReq",
                    BuildDetail(invoice, invoiceNumber))));

        var response = await _invoker.InvokeAsync(Endpoint,
            "http://ar.gov.afip.dif.FEV1/FECAESolicitar", body, cancellationToken).ConfigureAwait(false);

        var result = response.Descendants(Ar + "FECAESolicitarResult").FirstOrDefault() ?? response;
        return ParseAuthorizationResult(result, invoice);
    }

    private XElement BuildAuth(AccessTicket ticket) =>
        new(Ar + "Auth",
            new XElement(Ar + "Token", ticket.Token),
            new XElement(Ar + "Sign", ticket.Sign),
            new XElement(Ar + "Cuit", Cuit));

    private static XElement BuildDetail(Invoice invoice, long invoiceNumber)
    {
        var detail = new XElement(Ar + "FECAEDetRequest",
            new XElement(Ar + "Concepto", (int)invoice.Concept),
            new XElement(Ar + "DocTipo", (int)invoice.ReceiverDocumentType),
            new XElement(Ar + "DocNro", invoice.ReceiverDocumentNumber),
            new XElement(Ar + "CbteDesde", invoiceNumber),
            new XElement(Ar + "CbteHasta", invoiceNumber),
            new XElement(Ar + "CbteFch", FormatDate(invoice.Date)),
            new XElement(Ar + "ImpTotal", FormatDecimal(invoice.TotalAmount)),
            new XElement(Ar + "ImpTotConc", FormatDecimal(invoice.NonTaxableAmount)),
            new XElement(Ar + "ImpNeto", FormatDecimal(invoice.NetAmount)),
            new XElement(Ar + "ImpOpEx", FormatDecimal(invoice.ExemptAmount)),
            new XElement(Ar + "ImpTrib", FormatDecimal(invoice.OtherTaxesAmount)),
            new XElement(Ar + "ImpIVA", FormatDecimal(SumVat(invoice.VatLines))),
            new XElement(Ar + "MonId", invoice.CurrencyCode),
            new XElement(Ar + "MonCotiz", FormatDecimal(invoice.CurrencyQuotation)),
            new XElement(Ar + "CondicionIVAReceptorId", (int)invoice.ReceiverVatCondition));

        if (invoice.Concept is Concept.Services or Concept.ProductsAndServices)
        {
            if (invoice.ServicePeriodStart is { } from) detail.Add(new XElement(Ar + "FchServDesde", FormatDate(from)));
            if (invoice.ServicePeriodEnd is { } to) detail.Add(new XElement(Ar + "FchServHasta", FormatDate(to)));
            if (invoice.PaymentDueDate is { } due) detail.Add(new XElement(Ar + "FchVtoPago", FormatDate(due)));
        }

        if (invoice.AssociatedInvoices.Count > 0)
        {
            var assoc = new XElement(Ar + "CbtesAsoc");
            foreach (var a in invoice.AssociatedInvoices)
            {
                var item = new XElement(Ar + "CbteAsoc",
                    new XElement(Ar + "Tipo", (int)a.Type),
                    new XElement(Ar + "PtoVta", a.PointOfSale),
                    new XElement(Ar + "Nro", a.Number));
                if (!string.IsNullOrWhiteSpace(a.Cuit)) item.Add(new XElement(Ar + "Cuit", a.Cuit));
                if (a.Date is { } d) item.Add(new XElement(Ar + "CbteFch", FormatDate(d)));
                assoc.Add(item);
            }
            detail.Add(assoc);
        }

        if (invoice.VatLines.Count > 0)
        {
            var iva = new XElement(Ar + "Iva");
            foreach (var l in invoice.VatLines)
            {
                iva.Add(new XElement(Ar + "AlicIva",
                    new XElement(Ar + "Id", (int)l.Rate),
                    new XElement(Ar + "BaseImp", FormatDecimal(l.TaxableBase)),
                    new XElement(Ar + "Importe", FormatDecimal(l.Amount))));
            }
            detail.Add(iva);
        }

        return detail;
    }

    private static InvoiceAuthorizationResult ParseAuthorizationResult(XElement root, Invoice invoice)
    {
        var observations = new List<InvoiceObservation>();
        var errors = new List<InvoiceError>();

        foreach (var err in root.Descendants(Ar + "Err"))
        {
            errors.Add(new InvoiceError(
                ParseInt(err.Element(Ar + "Code")?.Value),
                err.Element(Ar + "Msg")?.Value ?? string.Empty));
        }

        var detail = root.Descendants(Ar + "FECAEDetResponse").FirstOrDefault();
        if (detail is null)
        {
            return new InvoiceAuthorizationResult(false, null, null, null,
                invoice.PointOfSale, invoice.Type, observations, errors);
        }

        foreach (var obs in detail.Descendants(Ar + "Obs"))
        {
            observations.Add(new InvoiceObservation(
                ParseInt(obs.Element(Ar + "Code")?.Value),
                obs.Element(Ar + "Msg")?.Value ?? string.Empty));
        }

        var resultado = detail.Element(Ar + "Resultado")?.Value;
        var cae = detail.Element(Ar + "CAE")?.Value;
        var caeVto = detail.Element(Ar + "CAEFchVto")?.Value;
        var cbteDesde = detail.Element(Ar + "CbteDesde")?.Value;

        var success = string.Equals(resultado, "A", StringComparison.Ordinal) && !string.IsNullOrEmpty(cae);

        return new InvoiceAuthorizationResult(
            success,
            success ? cae : null,
            success && DateOnly.TryParseExact(caeVto, "yyyyMMdd", out var d) ? d : null,
            long.TryParse(cbteDesde, out var n) ? n : null,
            invoice.PointOfSale,
            invoice.Type,
            observations,
            errors);
    }

    private static void ThrowOnErrors(XElement result)
    {
        var errs = result.Descendants(Ar + "Err");
        var pairs = new List<(int Code, string Message)>();
        foreach (var err in errs)
        {
            pairs.Add((ParseInt(err.Element(Ar + "Code")?.Value), err.Element(Ar + "Msg")?.Value ?? string.Empty));
        }
        if (pairs.Count > 0) throw new AfipBusinessException(pairs);
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal SumVat(IReadOnlyList<VatLine> lines)
    {
        decimal acc = 0;
        foreach (var l in lines) acc += l.Amount;
        return acc;
    }
}
