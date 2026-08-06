using System;
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

namespace Afip.Arca.Sdk.IncomeTax.Calculation;

/// <summary>
/// Default implementation of <see cref="IIncomeTaxCalculator"/>. Applies the standard
/// RG 830 algorithm: monthly accumulation, non-taxable minimum, progressive scale,
/// subtraction of previously practiced withholdings, and minimum-amount threshold.
/// </summary>
public sealed class IncomeTaxCalculator : IIncomeTaxCalculator
{
    private readonly IIncomeTaxScaleProvider _scaleProvider;

    /// <summary>Initializes a new instance of the <see cref="IncomeTaxCalculator"/> class.</summary>
    public IncomeTaxCalculator(IIncomeTaxScaleProvider scaleProvider)
    {
        _scaleProvider = scaleProvider ?? throw new ArgumentNullException(nameof(scaleProvider));
    }

    /// <inheritdoc />
    public IncomeTaxWithholdingResult Calculate(IncomeTaxWithholdingRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.CurrentPaymentAmount < 0) throw new ArgumentOutOfRangeException(nameof(request));

        var scale = _scaleProvider.GetScale(request.Regime, request.PaymentDate);
        var monthlyTotal = request.AccumulatedMonthlyPayments + request.CurrentPaymentAmount;

        if (monthlyTotal <= scale.NonTaxableMinimum)
        {
            return new IncomeTaxWithholdingResult(
                WithholdableBase: 0m,
                AccumulatedWithholding: 0m,
                PreviouslyWithheld: request.PreviouslyWithheld,
                WithholdingAmount: 0m,
                Applies: false,
                NotAppliedReason: "Monthly accumulated payments do not exceed the non-taxable minimum.");
        }

        var withholdableBase = Math.Round(monthlyTotal - scale.NonTaxableMinimum, 2, MidpointRounding.AwayFromZero);

        decimal accumulated = request.IsRegistered
            ? ApplyScale(scale, withholdableBase)
            : Math.Round(withholdableBase * scale.UnregisteredRate, 2, MidpointRounding.AwayFromZero);

        var net = accumulated - request.PreviouslyWithheld;
        if (net <= 0)
        {
            return new IncomeTaxWithholdingResult(
                WithholdableBase: withholdableBase,
                AccumulatedWithholding: accumulated,
                PreviouslyWithheld: request.PreviouslyWithheld,
                WithholdingAmount: 0m,
                Applies: false,
                NotAppliedReason: "Previously practiced withholdings already cover the obligation.");
        }

        var rounded = Math.Round(net, 2, MidpointRounding.AwayFromZero);

        if (rounded < scale.MinimumWithholding)
        {
            return new IncomeTaxWithholdingResult(
                WithholdableBase: withholdableBase,
                AccumulatedWithholding: accumulated,
                PreviouslyWithheld: request.PreviouslyWithheld,
                WithholdingAmount: 0m,
                Applies: false,
                NotAppliedReason: "Calculated amount (" + rounded + ") is below the minimum withholding (" + scale.MinimumWithholding + ").");
        }

        return new IncomeTaxWithholdingResult(
            WithholdableBase: withholdableBase,
            AccumulatedWithholding: accumulated,
            PreviouslyWithheld: request.PreviouslyWithheld,
            WithholdingAmount: rounded,
            Applies: true,
            NotAppliedReason: null);
    }

    private static decimal ApplyScale(IncomeTaxScale scale, decimal withholdableBase)
    {
        foreach (var bracket in scale.Brackets)
        {
            if (withholdableBase >= bracket.From && withholdableBase < bracket.To)
            {
                return Math.Round(
                    bracket.FixedAmount + (withholdableBase - bracket.From) * bracket.MarginalRate,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        // Fallback: above the last defined bracket → use the topmost.
        var top = scale.Brackets[scale.Brackets.Count - 1];
        return Math.Round(
            top.FixedAmount + (withholdableBase - top.From) * top.MarginalRate,
            2,
            MidpointRounding.AwayFromZero);
    }
}
