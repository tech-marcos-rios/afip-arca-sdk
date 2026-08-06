using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Authentication;

namespace Afip.Arca.Sdk.Configuration;

/// <summary>
/// Root configuration object for the SDK. Bound via <c>IServiceCollection.AddAfipSdk(...)</c>
/// using the Microsoft <c>Options</c> pattern.
/// </summary>
public sealed class AfipOptions
{
    /// <summary>Target environment.</summary>
    public AfipEnvironment Environment { get; set; } = AfipEnvironment.Homologation;

    /// <summary>
    /// CUIT (11-digit tax id) of the contributor that is making the calls.
    /// Required.
    /// </summary>
    public string Cuit { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint overrides. When <see langword="null"/>, <see cref="AfipEndpoints.DefaultsFor"/>
    /// is used.
    /// </summary>
    public AfipEndpoints? Endpoints { get; set; }

    /// <summary>
    /// Certificate-based local signing settings. When set, the SDK uses
    /// <see cref="WsaaAccessTicketProvider"/>.
    /// </summary>
    public CertificateSigningOptions? CertificateSigning { get; private set; }

    /// <summary>
    /// External ticket factory. When set, the SDK uses
    /// <see cref="ExternalAccessTicketProvider"/> and delegates ticket acquisition to
    /// the caller — useful for HSM-based or remote signing services.
    /// </summary>
    public Func<string, CancellationToken, Task<AccessTicket>>? ExternalTicketProvider { get; private set; }

    /// <summary>How many minutes before <c>expirationTime</c> a cached TA is considered stale.</summary>
    public int TicketRefreshLeewayMinutes { get; set; } = 5;

    /// <summary>How many minutes the TRA <c>expirationTime</c> is set ahead of <c>generationTime</c>.</summary>
    public int TraValidityMinutes { get; set; } = 10;

    /// <summary>Configures the local certificate-based signing strategy.</summary>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public AfipOptions UseLocalCertificateSigning(Action<CertificateSigningOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        CertificateSigning = new CertificateSigningOptions();
        configure(CertificateSigning);
        ExternalTicketProvider = null;
        return this;
    }

    /// <summary>Configures the external ticket provider strategy.</summary>
    /// <param name="provider">Function that produces a TA for the given <c>service</c>.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public AfipOptions UseExternalTicketProvider(Func<string, CancellationToken, Task<AccessTicket>> provider)
    {
        ExternalTicketProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        CertificateSigning = null;
        return this;
    }

    /// <summary>Returns the effective endpoint set, falling back to defaults.</summary>
    public AfipEndpoints ResolveEndpoints() => Endpoints ?? AfipEndpoints.DefaultsFor(Environment);
}

/// <summary>
/// Settings for the local-certificate-based signing strategy.
/// </summary>
public sealed class CertificateSigningOptions
{
    /// <summary>The X.509 certificate (with private key) used to sign the TRA.</summary>
    public X509Certificate2? Certificate { get; private set; }

    /// <summary>Loads the certificate from a <c>.pfx</c>/<c>.p12</c> file.</summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <param name="password">PFX password.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CertificateSigningOptions FromFile(string path, string password)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required.", nameof(path));
#pragma warning disable SYSLIB0057
        Certificate = new X509Certificate2(path, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
        return this;
    }

    /// <summary>Loads the certificate from a raw PFX byte array.</summary>
    /// <param name="pfxBytes">PFX content.</param>
    /// <param name="password">PFX password.</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CertificateSigningOptions FromBytes(byte[] pfxBytes, string password)
    {
        if (pfxBytes is null) throw new ArgumentNullException(nameof(pfxBytes));
#pragma warning disable SYSLIB0057
        Certificate = new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
        return this;
    }

    /// <summary>Uses an already-loaded certificate. Useful when the cert lives in a Key Vault, store, etc.</summary>
    /// <param name="certificate">The certificate (must have a usable private key).</param>
    /// <returns>This instance, for fluent chaining.</returns>
    public CertificateSigningOptions FromCertificate(X509Certificate2 certificate)
    {
        Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        return this;
    }
}
