using System.Threading;
using System.Threading.Tasks;

namespace Afip.Arca.Sdk.MultiTenancy;

/// <summary>
/// Factory that resolves a per-tenant <see cref="IAfipClient"/>. Clients are created
/// lazily on first access and cached in memory. The backing
/// <see cref="ITenantOptionsProvider"/> is called only on first access or after an
/// explicit <see cref="InvalidateClient"/> call — so new tenants can be added to the
/// database at runtime without restarting the application.
/// </summary>
public interface IAfipClientFactory
{
    /// <summary>
    /// Returns (or lazily creates) the <see cref="IAfipClient"/> for the given tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TenantNotFoundException">
    /// Thrown when <see cref="ITenantOptionsProvider"/> returns <see langword="null"/>
    /// for the given tenant.
    /// </exception>
    Task<IAfipClient> GetClientAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cached client for the given tenant. The next call to
    /// <see cref="GetClientAsync"/> will reload the configuration from
    /// <see cref="ITenantOptionsProvider"/> and build a new client.
    /// Call this after updating a tenant's certificate or changing its CUIT.
    /// </summary>
    void InvalidateClient(string tenantId);
}
