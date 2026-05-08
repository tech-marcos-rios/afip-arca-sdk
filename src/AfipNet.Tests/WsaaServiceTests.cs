using AfipNet.Models.Auth;
using FluentAssertions;

namespace AfipNet.Tests;

public class WsaaServiceTests
{
    [Fact]
    public void TicketAutorizacion_EsValido_ReturnsFalse_WhenExpired()
    {
        var ticket = new TicketAutorizacion
        {
            Token = "token",
            Sign = "sign",
            Generado = DateTime.UtcNow.AddHours(-13),
            Expiracion = DateTime.UtcNow.AddHours(-1),
            Servicio = "wsfe"
        };

        ticket.EsValido().Should().BeFalse();
    }

    [Fact]
    public void TicketAutorizacion_EsValido_ReturnsTrue_WhenStillActive()
    {
        var ticket = new TicketAutorizacion
        {
            Token = "token",
            Sign = "sign",
            Generado = DateTime.UtcNow,
            Expiracion = DateTime.UtcNow.AddHours(11),
            Servicio = "wsfe"
        };

        ticket.EsValido().Should().BeTrue();
    }

    [Fact]
    public void TicketAutorizacion_EsValido_WithMargen_ReturnsFalse_WhenExpiresWithin10Minutes()
    {
        var ticket = new TicketAutorizacion
        {
            Token = "token",
            Sign = "sign",
            Generado = DateTime.UtcNow,
            Expiracion = DateTime.UtcNow.AddMinutes(5),
            Servicio = "wsfe"
        };

        ticket.EsValido(margenMinutos: 10).Should().BeFalse();
    }
}
