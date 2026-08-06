using System;
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

namespace Afip.Arca.Sdk.IncomeTax.Calculation;

/// <summary>
/// Source of withholding scales. AFIP updates these periodically; concrete providers
/// can return hard-coded versioned data, fetch from a database, or pull from a feed.
/// </summary>
public interface IIncomeTaxScaleProvider
{
    /// <summary>Returns the scale in effect on <paramref name="date"/> for the given regime.</summary>
    /// <exception cref="InvalidOperationException">When no scale is configured for that combination.</exception>
    IncomeTaxScale GetScale(int regime, DateOnly date);
}
