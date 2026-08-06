using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Demo.Helpers;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;

namespace Afip.Arca.Sdk.Demo.Demos;

internal static class InvoicingDemo
{
    public static async Task EmitAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Emitir comprobante (FECAESolicitar)");

        var type = Prompt.AskEnum<InvoiceType>("Tipo de comprobante", defaultValue: InvoiceType.FacturaB);
        var pos = Prompt.AskInt("Punto de venta (PtoVta)", defaultValue: 1, min: 1, max: 99999);
        var concept = Prompt.AskEnum<Concept>("Concepto", defaultValue: Concept.Products);
        var date = Prompt.AskDate("Fecha del comprobante");

        var docKind = Prompt.AskInt("Receptor: [1] Consumidor Final  [2] CUIT  [3] DNI", defaultValue: 1, min: 1, max: 3);

        var builder = InvoiceBuilder
            .ForType(type)
            .AtPointOfSale(pos)
            .WithConcept(concept)
            .WithDate(date);

        builder = docKind switch
        {
            2 => builder.ToCuit(Prompt.AskLong("CUIT del receptor (11 dígitos)", min: 10_000_000_000)),
            3 => builder.ToDni(Prompt.AskLong("DNI del receptor", min: 1)),
            _ => builder.ToConsumerFinal(),
        };

        // RG 5616/2024: CondicionIVAReceptorId obligatorio.
        // Para CF el default (ConsumerFinal) sirve; para CUIT/DNI preguntamos.
        if (docKind != 1)
        {
            Prompt.Info("Condición frente al IVA del receptor (RG 5616/2024):");
            var vatCondition = Prompt.AskEnum<ReceiverVatCondition>(
                "Elegí",
                defaultValue: docKind == 2 ? ReceiverVatCondition.RegisteredVat : ReceiverVatCondition.ConsumerFinal);
            builder = builder.WithReceiverVatCondition(vatCondition);
        }

        if (concept is Concept.Services or Concept.ProductsAndServices)
        {
            var from = Prompt.AskDate("Servicio desde");
            var to = Prompt.AskDate("Servicio hasta", defaultValue: from);
            var due = Prompt.AskDate("Vencimiento de pago", defaultValue: to);
            builder = builder.WithServicePeriod(from, to, due);
        }

        Prompt.Info("Ingresá una o más líneas de IVA. Dejá net en 0 para terminar.");
        var lineCount = 0;
        while (true)
        {
            var net = Prompt.AskDecimal($"Línea {lineCount + 1} — Neto gravado (0 para terminar)", min: 0);
            if (net == 0m) break;
            var rate = Prompt.AskEnum<VatRate>("Alícuota de IVA", defaultValue: VatRate.TwentyOne);
            builder = builder.WithVatBase(net, rate);
            lineCount++;
        }

        if (lineCount == 0 && Prompt.AskYesNo("¿La factura no tiene IVA (ej. Factura C)?", defaultYes: true))
        {
            var net = Prompt.AskDecimal("Importe neto total", min: 0);
            builder = builder.WithNetAmount(net).WithTotalAmount(net);
        }

        var invoice = builder.Build();

        Prompt.Info($"Resumen: Neto={invoice.NetAmount:N2}  Total={invoice.TotalAmount:N2}  Líneas IVA={invoice.VatLines.Count}");
        if (!Prompt.AskYesNo("¿Enviar a AFIP?", defaultYes: true)) return;

        try
        {
            var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);
            if (result.IsSuccess)
            {
                Prompt.Success($"CAE: {result.Cae}  (vence {result.CaeExpiration:yyyy-MM-dd})");
                Prompt.Info($"Comprobante: {invoice.Type} {invoice.PointOfSale:D4}-{result.AssignedNumber:D8}");
            }
            else
            {
                Prompt.Error($"AFIP rechazó la solicitud ({result.Errors.Count} error/es):");
                foreach (var e in result.Errors)
                {
                    Console.WriteLine($"    [{e.Code}] {e.Message}");
                }
            }
            foreach (var obs in result.Observations)
            {
                Prompt.Warning($"Observación [{obs.Code}]: {obs.Message}");
            }
        }
        catch (AfipValidationException ex)
        {
            Prompt.Error("Validación local previa falló:");
            foreach (var f in ex.Failures) Console.WriteLine("    - " + f);
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error AFIP: " + ex.Message);
        }
    }

    public static async Task CancelAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Anular comprobante (vía Nota de Crédito)");
        Prompt.Info("AFIP no permite anular: se emite una NC asociada al comprobante original.");

        var origType = Prompt.AskEnum<InvoiceType>("Tipo del comprobante original", defaultValue: InvoiceType.FacturaB);
        var origPos = Prompt.AskInt("Punto de venta original", defaultValue: 1, min: 1);
        var origNumber = Prompt.AskLong("Número del comprobante original", min: 1);
        var total = Prompt.AskDecimal("Importe total a acreditar", min: 0.01m);

        var reference = new InvoiceReference(origType, origPos, origNumber);

        try
        {
            var result = await afip.Invoicing.CancelAsync(reference, total, ct);
            if (result.IsSuccess)
            {
                Prompt.Success($"Nota de Crédito emitida — CAE: {result.Cae}");
            }
            else
            {
                Prompt.Error("AFIP rechazó la NC:");
                foreach (var e in result.Errors) Console.WriteLine($"    [{e.Code}] {e.Message}");
            }
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }

    public static async Task LastNumberAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Último número autorizado (FECompUltimoAutorizado)");
        var type = Prompt.AskEnum<InvoiceType>("Tipo de comprobante", defaultValue: InvoiceType.FacturaB);
        var pos = Prompt.AskInt("Punto de venta", defaultValue: 1, min: 1);

        try
        {
            var last = await afip.Invoicing.GetLastAuthorizedNumberAsync(type, pos, ct);
            Prompt.Success($"Último número autorizado para {type} en PtoVta {pos}: {last}");
            Prompt.Info($"El próximo comprobante a emitir debe ser el N° {last + 1}.");
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }
}
