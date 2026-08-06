using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.Demo.Configuration;
using Afip.Arca.Sdk.Demo.Data;
using Afip.Arca.Sdk.Demo.Demos;
using Afip.Arca.Sdk.Demo.Helpers;
using Afip.Arca.Sdk.Demo.Services;
using Afip.Arca.Sdk.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Afip.Arca.Sdk — Demo Interactivo de Consumo del NuGet         ║");
Console.WriteLine("║   Versión: 1.1.0   |   Ambientes: Homologación / Producción     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.WriteLine("  Modos disponibles:");
Console.WriteLine("    [1] Single-tenant — un CUIT, configurado al inicio");
Console.WriteLine("    [2] Multi-tenant  — N contribuyentes desde base de datos");
Console.WriteLine();

var mode = Prompt.AskInt("Elegí el modo", defaultValue: 1, min: 1, max: 2);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Prompt.Warning("Cancelación solicitada — se interrumpirá la operación actual.");
};

if (mode == 1)
{
    await RunSingleTenantAsync(cts.Token);
}
else
{
    await RunMultiTenantAsync(cts.Token);
}

return 0;

// ─── Single-tenant (modo original, sin cambios) ────────────────────────────

static async Task RunSingleTenantAsync(CancellationToken ct)
{
    IServiceProvider provider;
    try
    {
        provider = SetupWizard.RunSetup();
    }
    catch (Exception ex)
    {
        Prompt.Error("No se pudo inicializar el SDK: " + ex.Message);
        return;
    }

    var afip = provider.GetRequiredService<IAfipClient>();

    while (!ct.IsCancellationRequested)
    {
        Prompt.Header("Menú principal — Single-tenant");
        Console.WriteLine("  [1] Health check (FEDummy)");
        Console.WriteLine("  [2] Emitir comprobante (factura / ND / NC)");
        Console.WriteLine("  [3] Anular comprobante (vía Nota de Crédito)");
        Console.WriteLine("  [4] Consultar último número autorizado");
        Console.WriteLine("  [5] Calcular retención de Ganancias (RG 830)");
        Console.WriteLine("  [6] Emitir certificado de retención a SIRE");
        Console.WriteLine("  [7] Consultar certificado SIRE");
        Console.WriteLine("  [8] Anular certificado SIRE");
        Console.WriteLine("  [0] Salir");
        Console.WriteLine();

        var option = Prompt.AskInt("Elegí una opción", min: 0, max: 8);
        try
        {
            switch (option)
            {
                case 0: Prompt.Info("¡Hasta luego!"); return;
                case 1: await HealthDemo.RunAsync(afip, ct); break;
                case 2: await InvoicingDemo.EmitAsync(afip, ct); break;
                case 3: await InvoicingDemo.CancelAsync(afip, ct); break;
                case 4: await InvoicingDemo.LastNumberAsync(afip, ct); break;
                case 5: IncomeTaxDemo.Run(afip); break;
                case 6: await SireDemo.IssueAsync(afip, ct); break;
                case 7: await SireDemo.QueryAsync(afip, ct); break;
                case 8: await SireDemo.CancelAsync(afip, ct); break;
            }
        }
        catch (OperationCanceledException) { Prompt.Warning("Operación cancelada por el usuario."); }
        catch (Exception ex) { Prompt.Error("Excepción inesperada: " + ex.Message); }

        Prompt.Pause();
    }
}

// ─── Multi-tenant ──────────────────────────────────────────────────────────

static async Task RunMultiTenantAsync(CancellationToken ct)
{
    // La clave de cifrado debe venir de una variable de entorno en producción.
    // Aquí usamos un valor fijo de desarrollo con advertencia explícita.
    var encryptionKey = ResolveEncryptionKey();

    var services = new ServiceCollection();

    services.AddLogging(b =>
    {
        b.AddSimpleConsole(c => { c.SingleLine = true; c.TimestampFormat = "[HH:mm:ss] "; });
        b.SetMinimumLevel(LogLevel.Information);
    });

    // SQLite local (se crea automáticamente si no existe)
    services.AddDbContextFactory<AfipDemoDbContext>(opts =>
        opts.UseSqlite("Data Source=afip_tenants.db"));

    services.AddSingleton(new AesCertificateEncryption(encryptionKey));

    // Registra IAfipClientFactory + ITenantOptionsProvider (DbTenantOptionsProvider)
    services.AddAfipClientFactory<DbTenantOptionsProvider>();

    services.AddSingleton<DbTenantOptionsProvider>();
    services.AddSingleton<TenantOnboardingService>();

    var provider = services.BuildServiceProvider();

    // Asegura que la tabla existe (EF Core sin migraciones formales)
    await using (var db = await provider
        .GetRequiredService<IDbContextFactory<AfipDemoDbContext>>()
        .CreateDbContextAsync(ct))
    {
        await db.Database.EnsureCreatedAsync(ct);
    }

    Prompt.Success("Base de datos lista (afip_tenants.db).");

    var factory = provider.GetRequiredService<IAfipClientFactory>();
    var onboarding = provider.GetRequiredService<TenantOnboardingService>();
    var dbFactory = provider.GetRequiredService<IDbContextFactory<AfipDemoDbContext>>();

    while (!ct.IsCancellationRequested)
    {
        Prompt.Header("Menú principal — Multi-tenant");
        Console.WriteLine("  [1] Gestión de tenants (registrar / listar / desactivar)");
        Console.WriteLine("  [2] Health check para un tenant");
        Console.WriteLine("  [3] Emitir factura para un tenant");
        Console.WriteLine("  [0] Salir");
        Console.WriteLine();

        var option = Prompt.AskInt("Elegí una opción", min: 0, max: 3);
        try
        {
            switch (option)
            {
                case 0: Prompt.Info("¡Hasta luego!"); return;
                case 1: await TenantDemo.RunAsync(factory, onboarding, dbFactory, ct); break;
                case 2:
                {
                    var tid = Prompt.AskString("ID del tenant");
                    var client = await factory.GetClientAsync(tid, ct);
                    await HealthDemo.RunAsync(client, ct);
                    break;
                }
                case 3:
                {
                    var tid = Prompt.AskString("ID del tenant");
                    var client = await factory.GetClientAsync(tid, ct);
                    await InvoicingDemo.EmitAsync(client, ct);
                    break;
                }
            }
        }
        catch (TenantNotFoundException ex) { Prompt.Error($"Tenant no encontrado: {ex.TenantId}"); }
        catch (OperationCanceledException) { Prompt.Warning("Operación cancelada por el usuario."); }
        catch (Exception ex) { Prompt.Error("Excepción inesperada: " + ex.Message); }

        Prompt.Pause();
    }
}

static byte[] ResolveEncryptionKey()
{
    var envKey = Environment.GetEnvironmentVariable("AFIP_DEMO_ENCRYPTION_KEY");
    if (!string.IsNullOrWhiteSpace(envKey))
    {
        try { return Convert.FromBase64String(envKey); }
        catch { /* falls through to dev default */ }
    }

    Prompt.Warning("Variable AFIP_DEMO_ENCRYPTION_KEY no configurada.");
    Prompt.Warning("Usando clave de desarrollo — NO usar en producción.");
    // 32 bytes fijos solo para demos locales
    return new byte[]
    {
        0x41,0x66,0x69,0x70,0x2E,0x41,0x72,0x63,0x61,0x2E,0x53,0x64,0x6B,0x44,0x65,0x6D,
        0x6F,0x4B,0x65,0x79,0x32,0x30,0x32,0x36,0x2D,0x44,0x65,0x76,0x21,0x21,0x21,0x00
    };
}
