using System;

namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// Represents an AFIP/ARCA WSAA <c>Ticket de Acceso (TA)</c>: the credential pair
/// (Token, Sign) plus the validity window assigned by WSAA.
/// </summary>
/// <param name="Service">Service name the ticket is valid for (e.g. <c>wsfe</c>).</param>
/// <param name="Cuit">CUIT of the contributor the ticket was issued for.</param>
/// <param name="Token">Opaque token to be sent in every business call.</param>
/// <param name="Sign">Opaque signature accompanying the token.</param>
/// <param name="GenerationTime">When the ticket was issued.</param>
/// <param name="ExpirationTime">When the ticket expires (typically 12 hours after generation).</param>
public sealed record AccessTicket(
    string Service,
    string Cuit,
    string Token,
    string Sign,
    DateTimeOffset GenerationTime,
    DateTimeOffset ExpirationTime)
{
    /// <summary>True when <paramref name="now"/> is past <see cref="ExpirationTime"/> minus the given leeway.</summary>
    /// <param name="now">Reference instant to compare against.</param>
    /// <param name="leeway">How much before the actual expiration the ticket should be considered stale.</param>
    public bool IsExpired(DateTimeOffset now, TimeSpan leeway) => now + leeway >= ExpirationTime;
}
