using System;
using System.Net.Http;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Authentication.Cms;
using Afip.Arca.Sdk.Authentication.Soap;
using Afip.Arca.Sdk.Common.Soap;
using Afip.Arca.Sdk.Common.Time;
using Afip.Arca.Sdk.IncomeTax.Calculation;
using Afip.Arca.Sdk.IncomeTax.Reporting;
using Afip.Arca.Sdk.IncomeTax.Reporting.Soap;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Soap;
using Afip.Arca.Sdk.Invoicing.Validation;
using Afip.Arca.Sdk.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Afip.Arca.Sdk.Configuration;

/// <summary>
/// Registration helpers for the SDK. Adds every component as a singleton (stateless
/// services) or scoped (services that hold per-request context).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AFIP SDK with the DI container for a single tenant.
    /// Use <see cref="AddAfipClientFactory{TProvider}"/> instead when the application
    /// serves multiple contributors (CUITs) simultaneously.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAfipSdk(this IServiceCollection services, Action<AfipOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        RegisterHttpClient(services);
        RegisterAfipSdkServices(services, configure);
        return services;
    }

    /// <summary>
    /// Registers the multi-tenant SDK infrastructure. The application must provide
    /// <typeparamref name="TProvider"/> which loads per-tenant options from whatever
    /// storage it uses (database, disk, etc.).
    /// Inject <see cref="IAfipClientFactory"/> and call
    /// <c>GetClientAsync(tenantId)</c> to obtain the per-tenant client.
    /// </summary>
    /// <typeparam name="TProvider">
    /// Concrete <see cref="ITenantOptionsProvider"/> implementation.
    /// </typeparam>
    /// <param name="services">Service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAfipClientFactory<TProvider>(this IServiceCollection services)
        where TProvider : class, ITenantOptionsProvider
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // The named HttpClient is registered once in the root container and shared
        // by every per-tenant child container via DynamicAfipClientFactory.
        RegisterHttpClient(services);

        services.TryAddSingleton<ITenantOptionsProvider, TProvider>();
        services.TryAddSingleton<IAfipClientFactory, DynamicAfipClientFactory>();
        return services;
    }

    /// <summary>
    /// Registers all AFIP SDK services except the named HttpClient.
    /// Called by <see cref="AddAfipSdk"/> and by <see cref="DynamicAfipClientFactory"/>
    /// when building per-tenant child containers (which share the root HttpClient).
    /// </summary>
    internal static void RegisterAfipSdkServices(IServiceCollection services, Action<AfipOptions> configure)
    {
        services.AddOptions<AfipOptions>().Configure(configure);
        services.AddSingleton<IValidateOptions<AfipOptions>, AfipOptionsValidator>();

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IAccessTicketCache, InMemoryAccessTicketCache>();
        services.TryAddSingleton<InvoiceValidator>();
        services.TryAddSingleton<IIncomeTaxScaleProvider, BuiltInIncomeTaxScaleProvider>();
        services.TryAddSingleton<IIncomeTaxCalculator, IncomeTaxCalculator>();

        services.TryAddSingleton<IHttpSoapInvoker, HttpSoapInvoker>();

        services.AddSingleton<WsaaSoapClient>();
        services.AddSingleton<TraDocumentBuilder>(sp =>
        {
            var clock = sp.GetRequiredService<IClock>();
            var opts = sp.GetRequiredService<IOptions<AfipOptions>>().Value;
            return new TraDocumentBuilder(clock, opts.TraValidityMinutes);
        });

        services.AddSingleton<IAccessTicketProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AfipOptions>>().Value;

            if (opts.ExternalTicketProvider is not null)
            {
                return new ExternalAccessTicketProvider(
                    sp.GetRequiredService<IAccessTicketCache>(),
                    sp.GetRequiredService<IOptionsMonitor<AfipOptions>>());
            }

            if (opts.CertificateSigning is null || opts.CertificateSigning.Certificate is null)
            {
                throw new InvalidOperationException(
                    "AfipOptions must configure either local certificate signing or an external ticket provider.");
            }

            var signer = new Pkcs7TraSigner(opts.CertificateSigning.Certificate);
            return new WsaaAccessTicketProvider(
                sp.GetRequiredService<IAccessTicketCache>(),
                sp.GetRequiredService<TraDocumentBuilder>(),
                signer,
                sp.GetRequiredService<WsaaSoapClient>(),
                sp.GetRequiredService<IOptionsMonitor<AfipOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WsaaAccessTicketProvider>>());
        });

        services.AddSingleton<WsfeSoapClient>();
        services.AddSingleton<IInvoiceService, InvoiceService>();

        services.AddSingleton<SireSoapClient>();
        services.AddSingleton<ISireService, SireService>();

        services.AddSingleton<IAfipClient, AfipClient>();
    }

    private static void RegisterHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(HttpSoapInvoker.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            })
            .AddPolicyHandler(BuildRetryPolicy())
            .AddPolicyHandler(BuildTimeoutPolicy());
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> BuildTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
}
