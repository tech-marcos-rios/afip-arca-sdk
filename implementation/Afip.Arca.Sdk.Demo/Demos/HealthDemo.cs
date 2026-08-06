using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Demo.Helpers;

namespace Afip.Arca.Sdk.Demo.Demos;

internal static class HealthDemo
{
    public static async Task RunAsync(IAfipClient afip, CancellationToken ct)
    {
        Prompt.Header("Health check — FEDummy");
        Prompt.Info("Consulta a AFIP qué subsistemas están operativos (App / Db / Auth).");

        try
        {
            var (app, db, auth) = await afip.Invoicing.HealthCheckAsync(ct);
            Console.WriteLine($"  AppServer:  {app}");
            Console.WriteLine($"  DbServer:   {db}");
            Console.WriteLine($"  AuthServer: {auth}");
            if (app == "OK" && db == "OK" && auth == "OK")
            {
                Prompt.Success("AFIP responde — todos los subsistemas OK.");
            }
            else
            {
                Prompt.Warning("Alguno de los subsistemas no está OK; revisar antes de operar.");
            }
        }
        catch (AfipException ex)
        {
            Prompt.Error("Error: " + ex.Message);
        }
    }
}
