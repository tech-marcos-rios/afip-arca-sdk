namespace Afip.Arca.Sdk.IncomeTax.Calculation.Models;

/// <summary>
/// Outcome of an RG 830 withholding calculation.
/// </summary>
/// <param name="WithholdableBase">Base subject to withholding after subtracting the non-taxable minimum.</param>
/// <param name="AccumulatedWithholding">Withholding computed from the scale (before subtracting prior).</param>
/// <param name="PreviouslyWithheld">Withholdings already practiced this month.</param>
/// <param name="WithholdingAmount">Final amount to withhold from the current payment.</param>
/// <param name="Applies">False when the amount is below the minimum and no withholding is due.</param>
/// <param name="NotAppliedReason">Human-readable reason when <paramref name="Applies"/> is false.</param>
public sealed record IncomeTaxWithholdingResult(
    decimal WithholdableBase,
    decimal AccumulatedWithholding,
    decimal PreviouslyWithheld,
    decimal WithholdingAmount,
    bool Applies,
    string? NotAppliedReason);
