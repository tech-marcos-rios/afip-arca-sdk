using System.Threading;
using System.Threading.Tasks;

namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// Provides an <see cref="AccessTicket"/> for a given AFIP service. Implementations
/// vary based on where the signing happens (locally with an X.509 cert, externally
/// against an HSM, or via a pre-existing ticket).
/// </summary>
public interface IAccessTicketProvider
{
    /// <summary>
    /// Returns a valid (non-expired) <see cref="AccessTicket"/> for <paramref name="service"/>.
    /// Implementations are expected to consult and update the cache transparently.
    /// </summary>
    /// <param name="service">AFIP service name (e.g. <c>wsfe</c>, <c>sire-ws</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AccessTicket> GetAsync(string service, CancellationToken cancellationToken);
}
