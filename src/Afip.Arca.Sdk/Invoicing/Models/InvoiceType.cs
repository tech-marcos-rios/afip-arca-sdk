namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// Comprobante types accepted by WSFEv1 (subset of the AFIP table — most commonly
/// used values for domestic transactions). Numeric value matches AFIP's <c>CbteTipo</c>.
/// </summary>
public enum InvoiceType
{
    /// <summary>Factura A (responsable inscripto → responsable inscripto).</summary>
    FacturaA = 1,
    /// <summary>Nota de Débito A.</summary>
    NotaDebitoA = 2,
    /// <summary>Nota de Crédito A.</summary>
    NotaCreditoA = 3,
    /// <summary>Factura B (responsable inscripto → consumidor final / exento / monotributista).</summary>
    FacturaB = 6,
    /// <summary>Nota de Débito B.</summary>
    NotaDebitoB = 7,
    /// <summary>Nota de Crédito B.</summary>
    NotaCreditoB = 8,
    /// <summary>Factura C (monotributistas y exentos).</summary>
    FacturaC = 11,
    /// <summary>Nota de Débito C.</summary>
    NotaDebitoC = 12,
    /// <summary>Nota de Crédito C.</summary>
    NotaCreditoC = 13,
    /// <summary>Factura M (operación entre RI con datos del receptor obligatorios).</summary>
    FacturaM = 51,
    /// <summary>Nota de Débito M.</summary>
    NotaDebitoM = 52,
    /// <summary>Nota de Crédito M.</summary>
    NotaCreditoM = 53,
}
