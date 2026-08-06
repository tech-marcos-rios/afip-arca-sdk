using Afip.Arca.Sdk.IncomeTax.Calculation;
using Afip.Arca.Sdk.IncomeTax.Reporting;
using Afip.Arca.Sdk.Invoicing;

namespace Afip.Arca.Sdk;

/// <summary>
/// Top-level facade exposing the three SDK areas. Inject this into application code
/// when you want a single dependency instead of one per service.
/// </summary>
public interface IAfipClient
{
    /// <summary>Electronic invoicing (WSFEv1).</summary>
    IInvoiceService Invoicing { get; }

    /// <summary>RG 830 withholding calculation.</summary>
    IIncomeTaxCalculator IncomeTaxCalculator { get; }

    /// <summary>SIRE — withholding certificate reporting.</summary>
    ISireService Sire { get; }
}
