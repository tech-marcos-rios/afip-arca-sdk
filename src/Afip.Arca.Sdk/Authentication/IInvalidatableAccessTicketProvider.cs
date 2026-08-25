namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// Optional capability for <see cref="IAccessTicketProvider"/> implementations that can
/// discard their cached ticket on demand. Implemented by the built-in providers
/// (<see cref="WsaaAccessTicketProvider"/>, <see cref="ExternalAccessTicketProvider"/>)
/// so that service-layer code (<c>InvoiceService</c>, <c>SireService</c>) can recover
/// from an AFIP "invalid/expired token" business error by invalidating the stale ticket
/// and retrying once with a freshly issued one.
/// Deliberately kept separate from <see cref="IAccessTicketProvider"/> — extending that
/// interface directly would be a breaking change for any custom implementation a
/// consumer may have written (e.g. an HSM-backed provider with no local cache to
/// invalidate). Consumers that do not implement this interface simply do not get the
/// automatic retry: the original error propagates as before.
/// </summary>
public interface IInvalidatableAccessTicketProvider
{
    /// <summary>
    /// Discards the cached ticket for <paramref name="service"/>, forcing the next
    /// <see cref="IAccessTicketProvider.GetAsync"/> call to request a fresh one from AFIP.
    /// </summary>
    /// <param name="service">AFIP service name (e.g. <c>wsfe</c>, <c>sire-ws</c>).</param>
    void Invalidate(string service);
}
