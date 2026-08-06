using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Demo.Helpers;
using Afip.Arca.Sdk.IncomeTax.Reporting.Models;

namespace Afip.Arca.Sdk.Demo.Demos;

internal static class SireDemo
{
    public static async Task IssueAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Informar retención a SIRE (emitir certificado)");

        var tax = Prompt.AskEnum<TaxCode>("Impuesto", defaultValue: TaxCode.IncomeTax);
        var regime = Prompt.AskInt("Código de régimen (ej. 19 = Profesionales)", defaultValue: 19, min: 1);
        var date = Prompt.AskDate("Fecha de la retención");
        var heldCuit = Prompt.AskString("CUIT del sujeto retenido (11 dígitos)");
        var taxableBase = Prompt.AskDecimal("Base imponible", min: 0);
        var amount = Prompt.AskDecimal("Importe retenido", min: 0);
        var cbteType = Prompt.AskInt("Tipo de comprobante asociado (ej. 6 = Factura B)", defaultValue: 6, min: 1);
        var cbteNumber = Prompt.AskString("Número del comprobante asociado (ej. 00001-00000042)");
        var condition = Prompt.AskEnum<SubjectCondition>("Condición del sujeto",
            defaultValue: SubjectCondition.Registered);

        var request = new WithholdingCertificateRequest(
            TaxCode: tax,
            Regime: regime,
            WithholdingDate: date,
            WithheldCuit: heldCuit,
            TaxableBase: taxableBase,
            WithheldAmount: amount,
            SourceComprobanteType: cbteType,
            SourceComprobanteNumber: cbteNumber,
            Condition: condition);

        try
        {
            var result = await afip.Sire.IssueAsync(request, ct);
            if (result.IsSuccess)
            {
                Prompt.Success($"Certificado emitido: {result.CertificateNumber}");
                if (result.IssueDate is { } d) Prompt.Info($"Fecha de emisión: {d:yyyy-MM-dd}");
                if (!string.IsNullOrEmpty(result.Status)) Prompt.Info("Estado: " + result.Status);
            }
            else
            {
                Prompt.Error("SIRE rechazó la emisión:");
                foreach (var (code, msg) in result.Errors)
                {
                    Console.WriteLine($"    [{code}] {msg}");
                }
            }
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }

    public static async Task QueryAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Consultar certificado SIRE");
        var number = Prompt.AskString("Número de certificado");
        try
        {
            var result = await afip.Sire.GetAsync(number, ct);
            if (result.IsSuccess)
            {
                Prompt.Success($"Certificado {result.CertificateNumber} — {result.Status}");
                if (result.IssueDate is { } d) Prompt.Info($"Emitido: {d:yyyy-MM-dd}");
            }
            else
            {
                Prompt.Error("No se pudo recuperar:");
                foreach (var (code, msg) in result.Errors)
                {
                    Console.WriteLine($"    [{code}] {msg}");
                }
            }
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }

    public static async Task CancelAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Anular certificado SIRE");
        var number = Prompt.AskString("Número de certificado a anular");
        if (!Prompt.AskYesNo($"¿Confirmás la anulación de {number}?", defaultYes: false)) return;

        try
        {
            var result = await afip.Sire.CancelAsync(number, ct);
            if (result.IsSuccess)
            {
                Prompt.Success($"Certificado {number} anulado.");
            }
            else
            {
                Prompt.Error("AFIP rechazó la anulación:");
                foreach (var (code, msg) in result.Errors)
                {
                    Console.WriteLine($"    [{code}] {msg}");
                }
            }
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }
}
