using Afip.Arca.Sdk.Configuration;

namespace Afip.Arca.Sdk.MultiTenancy;

/// <summary>
/// Configuration for a single AFIP tenant, returned by <see cref="ITenantOptionsProvider"/>
/// and consumed by <see cref="DynamicAfipClientFactory"/> to build the per-tenant
/// <see cref="IAfipClient"/>.
/// The certificate bytes must arrive already decrypted — decryption is the responsibility
/// of the provider implementation, not of the factory.
/// </summary>
public sealed record TenantAfipOptions
{
    /// <summary>Opaque identifier that maps to this tenant in the consumer application.</summary>
    public required string TenantId { get; init; }

    /// <summary>CUIT (11-digit tax id) of the contributor.</summary>
    public required string Cuit { get; init; }

    /// <summary>Target AFIP environment for this tenant.</summary>
    public AfipEnvironment Environment { get; init; } = AfipEnvironment.Homologation;

    /// <summary>
    /// Raw PFX bytes of the X.509 certificate (with private key), already decrypted.
    /// The factory loads it via <see cref="CertificateSigningOptions.FromBytes"/>.
    /// </summary>
    public required byte[] CertificateBytes { get; init; }

    /// <summary>Password for the PFX certificate.</summary>
    public required string CertificatePassword { get; init; }
}
