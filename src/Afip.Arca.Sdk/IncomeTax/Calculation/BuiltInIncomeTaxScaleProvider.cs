using System;
using System.Collections.Generic;
using System.Linq;
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

namespace Afip.Arca.Sdk.IncomeTax.Calculation;

/// <summary>
/// Default <see cref="IIncomeTaxScaleProvider"/> shipping with values from RG 5423
/// (effective 2024-10-01) for the regimes the SDK supports out of the box.
/// </summary>
/// <remarks>
/// These tables MUST be updated when AFIP publishes a new RG. The intent of shipping
/// them in code is convenience for the most common case; production deployments
/// should consider sourcing them from a database, configuration file, or upstream
/// service and registering their own <see cref="IIncomeTaxScaleProvider"/>.
/// </remarks>
public sealed class BuiltInIncomeTaxScaleProvider : IIncomeTaxScaleProvider
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<IncomeTaxScale>> _byRegime;

    /// <summary>Initializes a new instance of the <see cref="BuiltInIncomeTaxScaleProvider"/> class.</summary>
    public BuiltInIncomeTaxScaleProvider()
    {
        _byRegime = BuildScales();
    }

    /// <inheritdoc />
    public IncomeTaxScale GetScale(int regime, DateOnly date)
    {
        if (!_byRegime.TryGetValue(regime, out var scales))
        {
            throw new InvalidOperationException(
                "No scale configured for regime " + regime + ". " +
                "Register a custom IIncomeTaxScaleProvider to extend coverage.");
        }

        var match = scales
            .Where(s => s.EffectiveFrom <= date)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefault();

        return match ?? throw new InvalidOperationException(
            "No scale effective on " + date + " for regime " + regime + ".");
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<IncomeTaxScale>> BuildScales()
    {
        var dict = new Dictionary<int, IReadOnlyList<IncomeTaxScale>>();

        // RG 5423 — Profesionales y oficios (régimen 19) — vigencia 2024-10-01
        dict[(int)IncomeTaxRegime.ProfessionalsAndTrades] = new List<IncomeTaxScale>
        {
            new(
                Regime: (int)IncomeTaxRegime.ProfessionalsAndTrades,
                EffectiveFrom: new DateOnly(2024, 10, 1),
                NonTaxableMinimum: 160_000m,
                MinimumWithholding: 240m,
                UnregisteredRate: 0.28m,
                Brackets: new List<IncomeTaxScaleBracket>
                {
                    new(From:       0m, To:    7_500m, FixedAmount:      0m, MarginalRate: 0.05m),
                    new(From:   7_500m, To:   15_000m, FixedAmount:    375m, MarginalRate: 0.09m),
                    new(From:  15_000m, To:   22_500m, FixedAmount:  1_050m, MarginalRate: 0.12m),
                    new(From:  22_500m, To:   45_000m, FixedAmount:  1_950m, MarginalRate: 0.15m),
                    new(From:  45_000m, To:   75_000m, FixedAmount:  5_325m, MarginalRate: 0.19m),
                    new(From:  75_000m, To:  112_500m, FixedAmount: 11_025m, MarginalRate: 0.23m),
                    new(From: 112_500m, To:  187_500m, FixedAmount: 19_650m, MarginalRate: 0.27m),
                    new(From: 187_500m, To:  decimal.MaxValue, FixedAmount: 39_900m, MarginalRate: 0.31m),
                }),
        };

        return dict;
    }
}
