namespace AfipNet.Models.Invoicing;

public class Comprobante
{
    public int PuntoVenta { get; set; }

    /// <summary>
    /// Tipo de comprobante AFIP.
    /// Valores comunes: 1=Factura A, 6=Factura B, 11=Factura C, 51=Factura M.
    /// </summary>
    public int TipoComprobante { get; set; }

    public long CuitReceptor { get; set; }
    public string? RazonSocialReceptor { get; set; }

    public decimal ImporteTotal { get; set; }
    public decimal ImporteNeto { get; set; }
    public decimal ImporteIVA { get; set; }
    public decimal ImporteNoGravado { get; set; }
    public decimal ImporteExento { get; set; }

    public DateTime FechaComprobante { get; set; } = DateTime.Today;

    /// <summary>Código de moneda AFIP. "PES" para pesos argentinos.</summary>
    public string MonedaId { get; set; } = "PES";
    public decimal CotizacionMoneda { get; set; } = 1;

    /// <summary>Concepto: 1=Productos, 2=Servicios, 3=Productos y Servicios.</summary>
    public int Concepto { get; set; } = 1;
}
