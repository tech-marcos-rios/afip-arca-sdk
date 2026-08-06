using Afip.Arca.Sdk.Common.Exceptions;

namespace Afip.Arca.Sdk.MultiTenancy;

/// <summary>
/// Thrown by <see cref="DynamicAfipClientFactory"/> when
/// <see cref="ITenantOptionsProvider"/> returns <see langword="null"/> for the requested
/// tenant identifier.
/// </summary>
public sealed class TenantNotFoundException : AfipException
{
    /// <summary>The tenant identifier that was not found.</summary>
    public string TenantId { get; }

    /// <summary>Initializes a new instance of the <see cref="TenantNotFoundException"/> class.</summary>
    public TenantNotFoundException(string tenantId)
        : base($"No AFIP configuration found for tenant '{tenantId}'.")
    {
        TenantId = tenantId;
    }
}
