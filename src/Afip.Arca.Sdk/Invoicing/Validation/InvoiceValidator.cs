using System;
using System.Collections.Generic;
using Afip.Arca.Sdk.Invoicing.Models;

namespace Afip.Arca.Sdk.Invoicing.Validation;

/// <summary>
/// Pre-flight validation: catches structural and arithmetic problems before sending
/// the request to AFIP. Each failure becomes one entry in the returned list.
/// </summary>
public sealed class InvoiceValidator
{
    private const decimal Epsilon = 0.01m;

    /// <summary>Runs validation. Empty result means the invoice is good to send.</summary>
    public IReadOnlyList<string> Validate(Invoice invoice)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        var failures = new List<string>();

        if (invoice.PointOfSale <= 0)
        {
            failures.Add("PointOfSale must be a positive integer.");
        }

        if (invoice.NetAmount < 0 || invoice.NonTaxableAmount < 0 ||
            invoice.ExemptAmount < 0 || invoice.OtherTaxesAmount < 0 || invoice.TotalAmount < 0)
        {
            failures.Add("Monetary amounts cannot be negative.");
        }

        var vatSum = 0m;
        var vatBaseSum = 0m;
        foreach (var line in invoice.VatLines)
        {
            vatSum += line.Amount;
            vatBaseSum += line.TaxableBase;
        }

        var expectedTotal = invoice.NetAmount + vatSum + invoice.NonTaxableAmount + invoice.ExemptAmount + invoice.OtherTaxesAmount;
        if (Math.Abs(expectedTotal - invoice.TotalAmount) > Epsilon)
        {
            failures.Add(
                "TotalAmount (" + invoice.TotalAmount + ") does not match the sum of Net+VAT+NonTaxable+Exempt+OtherTaxes (" + expectedTotal + "). " +
                "AFIP rejects requests where the math does not close to the cent.");
        }

        if (invoice.NetAmount > 0 && invoice.VatLines.Count == 0 && IsTypeARequiringVat(invoice.Type))
        {
            failures.Add("Comprobantes A/M with a non-zero NetAmount require at least one VAT line.");
        }

        if (invoice.Concept is Concept.Services or Concept.ProductsAndServices)
        {
            if (invoice.ServicePeriodStart is null || invoice.ServicePeriodEnd is null || invoice.PaymentDueDate is null)
            {
                failures.Add("Service-based concepts require ServicePeriodStart, ServicePeriodEnd and PaymentDueDate.");
            }
            else if (invoice.ServicePeriodEnd < invoice.ServicePeriodStart)
            {
                failures.Add("ServicePeriodEnd cannot be before ServicePeriodStart.");
            }
        }

        if (invoice.ReceiverDocumentType == DocumentType.Cuit &&
            invoice.ReceiverDocumentNumber.ToString().Length != 11)
        {
            failures.Add("ReceiverDocumentNumber must be 11 digits when ReceiverDocumentType is CUIT.");
        }

        if (IsNoteType(invoice.Type) && invoice.AssociatedInvoices.Count == 0)
        {
            failures.Add("Credit/debit notes must reference at least one original invoice via AssociatedInvoices.");
        }

        return failures;
    }

    private static bool IsTypeARequiringVat(InvoiceType type) =>
        type is InvoiceType.FacturaA or InvoiceType.NotaDebitoA or InvoiceType.NotaCreditoA
             or InvoiceType.FacturaM or InvoiceType.NotaDebitoM or InvoiceType.NotaCreditoM;

    private static bool IsNoteType(InvoiceType type) =>
        type is InvoiceType.NotaCreditoA or InvoiceType.NotaCreditoB or InvoiceType.NotaCreditoC or InvoiceType.NotaCreditoM
             or InvoiceType.NotaDebitoA or InvoiceType.NotaDebitoB or InvoiceType.NotaDebitoC or InvoiceType.NotaDebitoM;
}
