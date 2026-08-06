namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// A VAT line on an invoice: rate + taxable base + computed VAT amount.
/// </summary>
/// <param name="Rate">Rate id.</param>
/// <param name="TaxableBase">Net amount before VAT.</param>
/// <param name="Amount">VAT amount.</param>
public sealed record VatLine(VatRate Rate, decimal TaxableBase, decimal Amount);
