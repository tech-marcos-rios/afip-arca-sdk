using AfipNet.Authentication;
using AfipNet.Configuration;
using AfipNet.Invoicing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfipNet.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de AfipNet en el contenedor de DI.
    /// Configura la sección "Afip" del appsettings.json automáticamente.
    /// </summary>
    /// <example>
    /// // En Program.cs:
    /// builder.Services.AddAfipNet(builder.Configuration);
    ///
    /// // En appsettings.json:
    /// "Afip": {
    ///   "UsarHomologacion": true,
    ///   "CertificadoPath": "/ruta/certificado.p12",
    ///   "CertificadoPassword": "...",  // mejor: variable de entorno
    ///   "Cuit": "20123456789"
    /// }
    /// </example>
    public static IServiceCollection AddAfipNet(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AfipOptions>(configuration.GetSection(AfipOptions.SectionName));
        services.AddHttpClient<IWsaaService, WsaaService>();
        services.AddHttpClient<IWsfeService, WsfeService>();
        return services;
    }

    /// <summary>
    /// Registra los servicios de AfipNet con configuración en código.
    /// </summary>
    public static IServiceCollection AddAfipNet(this IServiceCollection services, Action<AfipOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IWsaaService, WsaaService>();
        services.AddHttpClient<IWsfeService, WsfeService>();
        return services;
    }
}
