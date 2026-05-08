using AfipNet.Models.Invoicing;

namespace AfipNet.Invoicing;

public interface IWsfeService
{
    /// <summary>
    /// Solicita un CAE (Código de Autorización Electrónico) para el comprobante.
    /// </summary>
    Task<RespuestaCAE> SolicitarCAEAsync(Comprobante comprobante, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna el último número de comprobante autorizado para un punto de venta y tipo.
    /// Útil para calcular el número del próximo comprobante.
    /// </summary>
    Task<long> ObtenerUltimoNumeroAsync(int puntoVenta, int tipoComprobante, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si el servicio WSFE está disponible.
    /// </summary>
    Task<bool> VerificarDisponibilidadAsync(CancellationToken cancellationToken = default);
}
