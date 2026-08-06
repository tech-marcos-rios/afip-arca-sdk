namespace Afip.Arca.Sdk.IncomeTax.Calculation.Models;

/// <summary>
/// AFIP income-tax withholding regimes under RG 830/2000. Only the most commonly used
/// codes are exposed as named values; arbitrary codes can be supplied as raw integers
/// when needed.
/// </summary>
public enum IncomeTaxRegime
{
    /// <summary>Regime 116 — Lease of urban real estate.</summary>
    UrbanRealEstateLease = 116,

    /// <summary>Regime 19 — Professionals and trades (RG 5423 updated).</summary>
    ProfessionalsAndTrades = 19,

    /// <summary>Regime 25 — Goods.</summary>
    Goods = 25,

    /// <summary>Regime 78 — Sundry services.</summary>
    SundryServices = 78,

    /// <summary>Regime 94 — Sales commissions.</summary>
    Commissions = 94,
}
