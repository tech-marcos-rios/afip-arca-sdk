using System;
using System.Collections.Generic;

namespace Afip.Arca.Sdk.IncomeTax.Calculation.Models;

/// <summary>
/// A complete withholding scale for one regime, valid from a specific date onward.
/// </summary>
/// <param name="Regime">Regime code.</param>
/// <param name="EffectiveFrom">Inclusive start date.</param>
/// <param name="NonTaxableMinimum">Monthly threshold below which no withholding applies.</param>
/// <param name="MinimumWithholding">Minimum withholding amount to actually act (RG sets this around $240).</param>
/// <param name="UnregisteredRate">Flat rate for sujetos no inscriptos.</param>
/// <param name="Brackets">Brackets of the progressive scale.</param>
public sealed record IncomeTaxScale(
    int Regime,
    DateOnly EffectiveFrom,
    decimal NonTaxableMinimum,
    decimal MinimumWithholding,
    decimal UnregisteredRate,
    IReadOnlyList<IncomeTaxScaleBracket> Brackets);
