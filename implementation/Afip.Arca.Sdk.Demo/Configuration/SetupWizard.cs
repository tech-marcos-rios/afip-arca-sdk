using System;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.Demo.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Afip.Arca.Sdk.Demo.Configuration;

/// <summary>
/// Pide los datos mínimos para configurar el SDK (ambiente, CUIT, modo de auth) y
/// produce un ServiceProvider listo para usar.
/// </summary>
internal static class SetupWizard
{
    public static IServiceProvider RunSetup()
    {
        Prompt.Header("Configuración inicial");

        var env = Prompt.AskYesNo("¿Usar ambiente de Homologación?", defaultYes: true)
            ? AfipEnvironment.Homologation
            : AfipEnvironment.Production;

        var cuit = Prompt.AskString("CUIT del contribuyente (11 dígitos)");
        while (cuit.Length != 11 || !cuit.All(char.IsDigit))
        {
            Prompt.Error("CUIT inválido — deben ser 11 dígitos.");
            cuit = Prompt.AskString("CUIT del contribuyente (11 dígitos)");
        }

        Prompt.Info("Modo de autenticación con WSAA:");
        Console.WriteLine("  [1] Firma local con certificado X.509 (.pfx/.p12)");
        Console.WriteLine("  [2] Provider externo (TA simulado para demo offline)");
        var mode = Prompt.AskInt("Elegí 1 o 2", defaultValue: 1, min: 1, max: 2);

        // Capturar los datos sensibles AHORA, fuera del callback de Configure.
        // El SDK usa IOptionsMonitor<AfipOptions> y CurrentValue re-ejecuta el
        // Configure callback en cada acceso — si los prompts viven adentro, se
        // re-disparan en cada operación.
        string? pfxPath = null;
        string? pfxPwd = null;
        if (mode == 1)
        {
            pfxPath = Prompt.AskString("Ruta al .pfx del certificado");
            pfxPwd = Prompt.AskString("Contraseña del .pfx", allowEmpty: true);
        }
        else
        {
            Prompt.Warning("Modo demo: el provider externo devuelve un TA simulado.");
            Prompt.Warning("Las llamadas reales a AFIP fallarán; sirve para mostrar la API.");
        }

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.AddSimpleConsole(c =>
            {
                c.SingleLine = true;
                c.TimestampFormat = "[HH:mm:ss] ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });

        services.AddAfipSdk(opts =>
        {
            opts.Environment = env;
            opts.Cuit = cuit;

            if (mode == 1)
            {
                opts.UseLocalCertificateSigning(c => c.FromFile(pfxPath!, pfxPwd!));
            }
            else
            {
                opts.UseExternalTicketProvider(async (service, ct) =>
                {
                    await Task.Yield();
                    return new AccessTicket(
                        Service: service,
                        Cuit: cuit,
                        Token: "DEMO-TOKEN",
                        Sign: "DEMO-SIGN",
                        GenerationTime: DateTimeOffset.UtcNow,
                        ExpirationTime: DateTimeOffset.UtcNow.AddHours(12));
                });
            }
        });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Prompt.Success("SDK configurado correctamente.");
        Prompt.Info($"Ambiente: {env}");
        Prompt.Info($"CUIT:     {cuit}");
        Prompt.Info($"Modo:     {(mode == 1 ? "Firma local" : "Provider externo (demo)")}");
        return provider;
    }
}
