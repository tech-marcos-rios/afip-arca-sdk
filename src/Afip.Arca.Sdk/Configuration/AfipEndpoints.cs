using System;

namespace Afip.Arca.Sdk.Configuration;

/// <summary>
/// Endpoint URLs of the AFIP/ARCA Web Services per environment.
/// </summary>
/// <remarks>
/// The defaults point to the legacy <c>afip.gov.ar</c> hosts which remain active and
/// match what every reference implementation in the wild expects. The mirror hosts on
/// <c>arca.gov.ar</c> can be configured via <see cref="AfipOptions.Endpoints"/>.
/// </remarks>
public sealed class AfipEndpoints
{
    /// <summary>WSAA (authentication) endpoint.</summary>
    public Uri Wsaa { get; set; } = null!;

    /// <summary>WSFEv1 (electronic invoicing) endpoint.</summary>
    public Uri Wsfev1 { get; set; } = null!;

    /// <summary>SIRE (withholding reporting) endpoint.</summary>
    public Uri Sire { get; set; } = null!;

    /// <summary>Returns the default endpoint set for the given environment.</summary>
    /// <param name="environment">Target environment.</param>
    public static AfipEndpoints DefaultsFor(AfipEnvironment environment) =>
        environment switch
        {
            AfipEnvironment.Production => new AfipEndpoints
            {
                Wsaa = new Uri("https://wsaa.afip.gov.ar/ws/services/LoginCms"),
                Wsfev1 = new Uri("https://servicios1.afip.gov.ar/wsfev1/service.asmx"),
                Sire = new Uri("https://servicios1.afip.gov.ar/sire-ws/services/SireSoap"),
            },
            _ => new AfipEndpoints
            {
                Wsaa = new Uri("https://wsaahomo.afip.gov.ar/ws/services/LoginCms"),
                Wsfev1 = new Uri("https://wswhomo.afip.gov.ar/wsfev1/service.asmx"),
                Sire = new Uri("https://fwshomo.afip.gov.ar/sire-ws/services/SireSoap"),
            },
        };
}
