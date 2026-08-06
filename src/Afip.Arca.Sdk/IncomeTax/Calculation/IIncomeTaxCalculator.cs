using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

namespace Afip.Arca.Sdk.IncomeTax.Calculation;

/// <summary>
/// Pure (no I/O) computation of the income-tax withholding applicable to a payment
/// under RG 830/2000.
/// </summary>
public interface IIncomeTaxCalculator
{
    /// <summary>Calculates the withholding for the given payment.</summary>
    IncomeTaxWithholdingResult Calculate(IncomeTaxWithholdingRequest request);
}
