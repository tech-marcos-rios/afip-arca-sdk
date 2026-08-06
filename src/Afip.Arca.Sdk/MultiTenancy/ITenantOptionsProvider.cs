using System.Threading;
using System.Threading.Tasks;

namespace Afip.Arca.Sdk.MultiTenancy;

/// <summary>
/// Loads per-tenant AFIP configuration from whatever storage backend the consumer
/// application uses (database, disk, Key Vault, etc.).
/// Register an implementation with
/// <c>services.AddAfipClientFactory&lt;TProvider&gt;()</c>.
/// </summary>
public interface ITenantOptionsProvider
{
    /// <summary>
    /// Returns the AFIP options for the given tenant, or <see langword="null"/> if the
    /// tenant is not found or not active.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TenantAfipOptions?> GetAsync(string tenantId, CancellationToken cancellationToken);
}
