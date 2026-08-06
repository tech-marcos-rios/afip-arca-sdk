namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>VAT rate ids per AFIP's <c>FEParamGetTiposIva</c>.</summary>
public enum VatRate
{
    /// <summary>No gravado.</summary>
    NotTaxed = 1,
    /// <summary>Exento.</summary>
    Exempt = 2,
    /// <summary>0%.</summary>
    Zero = 3,
    /// <summary>10.5%.</summary>
    TenAndHalf = 4,
    /// <summary>21%.</summary>
    TwentyOne = 5,
    /// <summary>27%.</summary>
    TwentySeven = 6,
    /// <summary>5%.</summary>
    Five = 8,
    /// <summary>2.5%.</summary>
    TwoAndHalf = 9,
}

/// <summary>Helpers for <see cref="VatRate"/>.</summary>
public static class VatRateExtensions
{
    /// <summary>Returns the decimal multiplier corresponding to the rate (e.g. 0.21m for <see cref="VatRate.TwentyOne"/>).</summary>
    /// <param name="rate">Rate id.</param>
    public static decimal ToMultiplier(this VatRate rate) =>
        rate switch
        {
            VatRate.TwentyOne => 0.21m,
            VatRate.TenAndHalf => 0.105m,
            VatRate.TwentySeven => 0.27m,
            VatRate.Five => 0.05m,
            VatRate.TwoAndHalf => 0.025m,
            VatRate.Zero => 0m,
            VatRate.Exempt => 0m,
            VatRate.NotTaxed => 0m,
            _ => 0m,
        };
}
