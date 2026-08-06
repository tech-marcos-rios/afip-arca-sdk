using System;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Demo.Helpers;
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

namespace Afip.Arca.Sdk.Demo.Demos;

internal static class IncomeTaxDemo
{
    public static void Run(IAfipClient afip)
    {
        Prompt.Header("Cálculo de Retención de Ganancias (RG 830)");
        Prompt.Info("Cálculo local, sin conexión a AFIP — escala embebida RG 5423 (vigente 2024-10).");

        var regime = Prompt.AskEnum<IncomeTaxRegime>("Régimen aplicable",
            defaultValue: IncomeTaxRegime.ProfessionalsAndTrades);
        var date = Prompt.AskDate("Fecha del pago");
        var current = Prompt.AskDecimal("Importe del pago actual (sin retención)", min: 0);
        var accum = Prompt.AskDecimal("Pagos acumulados al mismo sujeto en el mes", defaultValue: 0m, min: 0);
        var prev = Prompt.AskDecimal("Retenciones ya practicadas en el mes", defaultValue: 0m, min: 0);
        var isRegistered = Prompt.AskYesNo("¿El sujeto está inscripto en Ganancias?", defaultYes: true);

        var request = new IncomeTaxWithholdingRequest(
            Regime: (int)regime,
            PaymentDate: date,
            CurrentPaymentAmount: current,
            AccumulatedMonthlyPayments: accum,
            PreviouslyWithheld: prev,
            IsRegistered: isRegistered);

        try
        {
            var result = afip.IncomeTaxCalculator.Calculate(request);

            Console.WriteLine();
            Console.WriteLine($"  Base sujeta a retención     : {result.WithholdableBase,15:N2}");
            Console.WriteLine($"  Retención acumulada (escala): {result.AccumulatedWithholding,15:N2}");
            Console.WriteLine($"  Retenciones previas         : {result.PreviouslyWithheld,15:N2}");
            Console.WriteLine($"  ────────────────────────────────────────────────");
            Console.WriteLine($"  Retención a practicar       : {result.WithholdingAmount,15:N2}");

            if (result.Applies)
            {
                Prompt.Success("Corresponde retener el importe indicado.");
            }
            else
            {
                Prompt.Warning("No corresponde retener.");
                if (result.NotAppliedReason is not null)
                {
                    Console.WriteLine("  Motivo: " + result.NotAppliedReason);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            Prompt.Error(ex.Message);
            Prompt.Info("Tip: registrá un IIncomeTaxScaleProvider custom para regímenes no incluidos.");
        }
    }
}
