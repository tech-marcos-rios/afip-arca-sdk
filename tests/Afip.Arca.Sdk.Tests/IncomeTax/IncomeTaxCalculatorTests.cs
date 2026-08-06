using System;
using Afip.Arca.Sdk.IncomeTax.Calculation;
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;
using FluentAssertions;
using Xunit;

namespace Afip.Arca.Sdk.Tests.IncomeTax;

public sealed class IncomeTaxCalculatorTests
{
    private readonly IncomeTaxCalculator _sut = new(new BuiltInIncomeTaxScaleProvider());

    private const int Regime = (int)IncomeTaxRegime.ProfessionalsAndTrades;
    private static readonly DateOnly Today = new(2025, 6, 15);

    [Fact]
    public void Calculate_WhenMonthlyTotalBelowMinimum_DoesNotWithhold()
    {
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 50_000m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 0m,
            IsRegistered: true));

        result.Applies.Should().BeFalse();
        result.WithholdingAmount.Should().Be(0m);
        result.NotAppliedReason.Should().Contain("non-taxable minimum");
    }

    [Fact]
    public void Calculate_ForRegisteredSubject_AppliesProgressiveScale()
    {
        // Base sujeta = 250.000 - 160.000 = 90.000 → tramo (75.000, 112.500)
        // Retención = 11.025 + (90.000 - 75.000) * 0.23 = 11.025 + 3.450 = 14.475
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 250_000m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 0m,
            IsRegistered: true));

        result.Applies.Should().BeTrue();
        result.WithholdableBase.Should().Be(90_000m);
        result.AccumulatedWithholding.Should().Be(14_475m);
        result.WithholdingAmount.Should().Be(14_475m);
    }

    [Fact]
    public void Calculate_ForUnregisteredSubject_AppliesFlatRate()
    {
        // Base sujeta = 250.000 - 160.000 = 90.000
        // Retención = 90.000 * 0.28 = 25.200
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 250_000m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 0m,
            IsRegistered: false));

        result.Applies.Should().BeTrue();
        result.WithholdingAmount.Should().Be(25_200m);
    }

    [Fact]
    public void Calculate_DiscountsPreviouslyWithheld()
    {
        // Mismo escenario que el test progresivo, pero ya se retuvieron 10.000 antes.
        // Retención neta = 14.475 - 10.000 = 4.475
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 250_000m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 10_000m,
            IsRegistered: true));

        result.Applies.Should().BeTrue();
        result.WithholdingAmount.Should().Be(4_475m);
    }

    [Fact]
    public void Calculate_WhenNetBelowMinimumThreshold_DoesNotWithhold()
    {
        // Base sujeta = 161.000 - 160.000 = 1.000 → primer tramo: 0 + 1000 * 0.05 = 50
        // 50 < mínimo de 240 → no se retiene.
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 161_000m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 0m,
            IsRegistered: true));

        result.Applies.Should().BeFalse();
        result.WithholdingAmount.Should().Be(0m);
        result.NotAppliedReason.Should().Contain("minimum withholding");
    }

    [Fact]
    public void Calculate_AccumulatesPaymentsWithinMonth()
    {
        // Pago anterior 120.000 + pago actual 80.000 = 200.000 acumulado.
        // Base = 200.000 - 160.000 = 40.000 → tramo (22.500, 45.000)
        // Retención = 1.950 + (40.000 - 22.500) * 0.15 = 1.950 + 2.625 = 4.575
        var result = _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: Regime,
            PaymentDate: Today,
            CurrentPaymentAmount: 80_000m,
            AccumulatedMonthlyPayments: 120_000m,
            PreviouslyWithheld: 0m,
            IsRegistered: true));

        result.Applies.Should().BeTrue();
        result.WithholdableBase.Should().Be(40_000m);
        result.WithholdingAmount.Should().Be(4_575m);
    }

    [Fact]
    public void Calculate_WhenRegimeIsUnknown_Throws()
    {
        var action = () => _sut.Calculate(new IncomeTaxWithholdingRequest(
            Regime: 99_999,
            PaymentDate: Today,
            CurrentPaymentAmount: 1m,
            AccumulatedMonthlyPayments: 0m,
            PreviouslyWithheld: 0m,
            IsRegistered: true));

        action.Should().Throw<InvalidOperationException>().WithMessage("*No scale configured*");
    }
}
