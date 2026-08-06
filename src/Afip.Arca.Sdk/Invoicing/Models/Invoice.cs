using System;
using System.Collections.Generic;

namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// Domain representation of an AFIP comprobante to be authorized. Immutable after
/// being produced by <see cref="InvoiceBuilder"/>. Maps to the data sent inside a
/// single <c>FECAEDetRequest</c>.
/// </summary>
public sealed record Invoice
{
    /// <summary>Comprobante type.</summary>
    public required InvoiceType Type { get; init; }

    /// <summary>Sales point (<c>PtoVta</c>).</summary>
    public required int PointOfSale { get; init; }

    /// <summary>Concept code.</summary>
    public required Concept Concept { get; init; }

    /// <summary>Receiver document type.</summary>
    public required DocumentType ReceiverDocumentType { get; init; }

    /// <summary>Receiver document number. Use 0 for consumidor final.</summary>
    public required long ReceiverDocumentNumber { get; init; }

    /// <summary>
    /// Receiver's VAT condition (<c>CondicionIVAReceptorId</c>). Mandatory since
    /// RG 5616/2024 — AFIP rejects the request with code 10246 if absent.
    /// </summary>
    public ReceiverVatCondition ReceiverVatCondition { get; init; } = ReceiverVatCondition.ConsumerFinal;

    /// <summary>Comprobante date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Currency code (e.g. <c>PES</c>, <c>DOL</c>).</summary>
    public string CurrencyCode { get; init; } = Currency.ArgentinePeso;

    /// <summary>Quotation against ARS. Must be 1 for PES.</summary>
    public decimal CurrencyQuotation { get; init; } = 1m;

    /// <summary>Service period start (only for service-based concepts).</summary>
    public DateOnly? ServicePeriodStart { get; init; }

    /// <summary>Service period end (only for service-based concepts).</summary>
    public DateOnly? ServicePeriodEnd { get; init; }

    /// <summary>Payment due date (only for service-based concepts).</summary>
    public DateOnly? PaymentDueDate { get; init; }

    /// <summary>Net taxable amount (sum of <see cref="VatLines"/> bases when fully taxed).</summary>
    public required decimal NetAmount { get; init; }

    /// <summary>Amount not subject to VAT (<c>ImpTotConc</c>).</summary>
    public decimal NonTaxableAmount { get; init; }

    /// <summary>Exempt amount (<c>ImpOpEx</c>).</summary>
    public decimal ExemptAmount { get; init; }

    /// <summary>VAT detail.</summary>
    public IReadOnlyList<VatLine> VatLines { get; init; } = Array.Empty<VatLine>();

    /// <summary>Total of other tributos (provincial/municipal taxes).</summary>
    public decimal OtherTaxesAmount { get; init; }

    /// <summary>Total invoiced amount (must equal Net + VAT + NonTaxable + Exempt + OtherTaxes).</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>Associated comprobantes (required for credit/debit notes).</summary>
    public IReadOnlyList<InvoiceReference> AssociatedInvoices { get; init; } = Array.Empty<InvoiceReference>();
}
