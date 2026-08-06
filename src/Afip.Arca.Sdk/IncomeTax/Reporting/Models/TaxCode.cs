namespace Afip.Arca.Sdk.IncomeTax.Reporting.Models;

/// <summary>
/// AFIP tax code (<c>impuesto</c>) used when reporting withholdings to SIRE.
/// </summary>
public enum TaxCode
{
    /// <summary>Income tax — Ganancias (217).</summary>
    IncomeTax = 217,

    /// <summary>VAT — IVA (767).</summary>
    Vat = 767,

    /// <summary>Social security — Seguridad Social (308).</summary>
    SocialSecurity = 308,
}
