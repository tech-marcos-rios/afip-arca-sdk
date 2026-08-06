namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// Thread-safe cache for <see cref="AccessTicket"/> instances, keyed by
/// <c>(cuit, service)</c>. Issuing a new ticket every call is wasteful and triggers
/// AFIP's <c>coe.alreadyAuthenticated</c> error.
/// </summary>
public interface IAccessTicketCache
{
    /// <summary>Attempts to retrieve a non-expired ticket from the cache.</summary>
    /// <param name="cuit">CUIT of the contributor.</param>
    /// <param name="service">AFIP service name.</param>
    /// <param name="ticket">When this method returns <see langword="true"/>, contains the cached ticket.</param>
    /// <returns><see langword="true"/> if a valid ticket was found.</returns>
    bool TryGet(string cuit, string service, out AccessTicket? ticket);

    /// <summary>Stores a ticket, replacing any previous one for the same key.</summary>
    /// <param name="ticket">The ticket to cache.</param>
    void Set(AccessTicket ticket);

    /// <summary>Removes any ticket for the given key. Useful after a 401-equivalent error.</summary>
    /// <param name="cuit">CUIT of the contributor.</param>
    /// <param name="service">AFIP service name.</param>
    void Invalidate(string cuit, string service);
}
