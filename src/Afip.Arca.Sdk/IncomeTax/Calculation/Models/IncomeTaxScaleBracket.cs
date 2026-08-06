namespace Afip.Arca.Sdk.IncomeTax.Calculation.Models;

/// <summary>One bracket of a progressive withholding scale.</summary>
/// <param name="From">Inclusive lower bound (after subtracting the non-taxable minimum).</param>
/// <param name="To">Exclusive upper bound. <see cref="decimal.MaxValue"/> for the top bracket.</param>
/// <param name="FixedAmount">Fixed amount applicable when the base falls inside this bracket.</param>
/// <param name="MarginalRate">Marginal rate (e.g. 0.31m for 31%) applied to the amount exceeding <paramref name="From"/>.</param>
public sealed record IncomeTaxScaleBracket(decimal From, decimal To, decimal FixedAmount, decimal MarginalRate);
