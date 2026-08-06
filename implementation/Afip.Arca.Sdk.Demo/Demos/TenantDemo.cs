using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Demo.Data;
using Afip.Arca.Sdk.Demo.Helpers;
using Afip.Arca.Sdk.Demo.Services;
using Afip.Arca.Sdk.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Afip.Arca.Sdk.Demo.Demos;

internal static class TenantDemo
{
    public static async Task RunAsync(
        IAfipClientFactory factory,
        TenantOnboardingService onboarding,
        IDbContextFactory<AfipDemoDbContext> dbFactory,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Prompt.Header("Gestión de Tenants — Multi-tenant");
            Console.WriteLine("  [1] Listar tenants configurados");
            Console.WriteLine("  [2] Registrar / actualizar tenant");
            Console.WriteLine("  [3] Desactivar tenant");
            Console.WriteLine("  [4] Health check para un tenant");
            Console.WriteLine("  [5] Emitir factura para un tenant");
            Console.WriteLine("  [0] Volver");
            Console.WriteLine();

            var opt = Prompt.AskInt("Opción", min: 0, max: 5);
            try
            {
                switch (opt)
                {
                    case 0: return;
                    case 1: await ListAsync(dbFactory, ct); break;
                    case 2: await RegisterAsync(onboarding, ct); break;
                    case 3: await DeactivateAsync(onboarding, ct); break;
                    case 4: await HealthCheckAsync(factory, ct); break;
                    case 5: await EmitInvoiceAsync(factory, ct); break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Prompt.Error(ex.Message); }

            Prompt.Pause();
        }
    }

    private static async Task ListAsync(IDbContextFactory<AfipDemoDbContext> dbFactory, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tenants = await db.TenantConfigs.AsNoTracking().OrderBy(t => t.TenantId).ToListAsync(ct);

        if (tenants.Count == 0)
        {
            Prompt.Warning("No hay tenants registrados todavía.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  {"ID",-22} {"Nombre",-28} {"CUIT",-13} {"Ambiente",-14} Activo   Modificado");
        Console.WriteLine($"  {new string('─', 95)}");
        foreach (var t in tenants)
        {
            var amb = t.UseHomologation ? "Homologación" : "Producción  ";
            var activo = t.IsActive ? "✔" : "✘";
            Console.WriteLine($"  {t.TenantId,-22} {t.DisplayName,-28} {t.Cuit,-13} {amb,-14} {activo,-8} {t.UpdatedAt:yyyy-MM-dd HH:mm}");
        }
    }

    private static async Task RegisterAsync(TenantOnboardingService onboarding, CancellationToken ct)
    {
        Console.WriteLine();
        Prompt.Info("Los datos se guardan en la BD local (SQLite). El certificado se cifra con AES-256-GCM.");

        var tenantId = Prompt.AskString("ID del tenant (ej: 'estudio-garcia', 'empresa-lopez')");
        var displayName = Prompt.AskString("Nombre para mostrar");
        var cuit = Prompt.AskString("CUIT (11 dígitos)");
        while (cuit.Length != 11 || !cuit.All(char.IsDigit))
        {
            Prompt.Error("CUIT inválido — deben ser exactamente 11 dígitos.");
            cuit = Prompt.AskString("CUIT (11 dígitos)");
        }
        var useHomo = Prompt.AskYesNo("¿Usar homologación?", defaultYes: true);
        var pfxPath = Prompt.AskString("Ruta al .pfx del certificado");
        var pfxPwd = Prompt.AskString("Contraseña del .pfx", allowEmpty: true);

        await onboarding.RegisterOrUpdateAsync(tenantId, displayName, cuit, useHomo, pfxPath, pfxPwd, ct);

        Prompt.Success($"Tenant '{tenantId}' guardado.");
        Prompt.Info("El cliente AFIP se creará automáticamente en el primer uso (sin restart).");
    }

    private static async Task DeactivateAsync(TenantOnboardingService onboarding, CancellationToken ct)
    {
        var tenantId = Prompt.AskString("ID del tenant a desactivar");
        await onboarding.DeactivateAsync(tenantId, ct);
        Prompt.Success($"Tenant '{tenantId}' desactivado. La caché fue invalidada.");
    }

    private static async Task HealthCheckAsync(IAfipClientFactory factory, CancellationToken ct)
    {
        var tenantId = Prompt.AskString("ID del tenant");
        Prompt.Info($"Conectando con AFIP para '{tenantId}'...");

        var client = await factory.GetClientAsync(tenantId, ct);
        var (app, db, auth) = await client.Invoicing.HealthCheckAsync(ct);

        Prompt.Success($"AppServer: {app}  |  DbServer: {db}  |  AuthServer: {auth}");
    }

    private static async Task EmitInvoiceAsync(IAfipClientFactory factory, CancellationToken ct)
    {
        var tenantId = Prompt.AskString("ID del tenant que emite la factura");
        var client = await factory.GetClientAsync(tenantId, ct);
        await InvoicingDemo.EmitAsync(client, ct);
    }
}
