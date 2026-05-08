using AfipNet.Models.Auth;

namespace AfipNet.Authentication;

public interface IWsaaService
{
    /// <summary>
    /// Obtiene un ticket de autorización para el servicio indicado.
    /// El ticket se cachea automáticamente hasta 10 minutos antes de su vencimiento.
    /// </summary>
    /// <param name="servicio">Nombre del servicio AFIP (ej: "wsfe").</param>
    Task<TicketAutorizacion> ObtenerTicketAsync(string servicio, CancellationToken cancellationToken = default);
}
