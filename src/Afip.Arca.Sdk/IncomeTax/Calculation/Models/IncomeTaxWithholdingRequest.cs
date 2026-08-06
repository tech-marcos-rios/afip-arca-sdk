using System;

namespace Afip.Arca.Sdk.IncomeTax.Calculation.Models;

/// <summary>
/// Input to <c>IIncomeTaxCalculator.Calculate</c>.
/// </summary>
/// <param name="Regime">Withholding regime code (typically a value of <see cref="IncomeTaxRegime"/>).</param>
/// <param name="PaymentDate">Date of the payment being processed.</param>
/// <param name="CurrentPaymentAmount">Amount being paid right now (gross, before withholding).</param>
/// <param name="AccumulatedMonthlyPayments">Sum of prior payments to the same subject for the same regime in the same calendar month.</param>
/// <param name="PreviouslyWithheld">Total withholding already practiced this month to the same subject for the same regime.</param>
/// <param name="IsRegistered">True if the subject is registered in the income-tax padrón.</param>
public sealed record IncomeTaxWithholdingRequest(
    int Regime,
    DateOnly PaymentDate,
    decimal CurrentPaymentAmount,
    decimal AccumulatedMonthlyPayments,
    decimal PreviouslyWithheld,
    bool IsRegistered);
