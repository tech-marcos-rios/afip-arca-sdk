namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>Receiver document type codes per AFIP (<c>DocTipo</c>).</summary>
public enum DocumentType
{
    /// <summary>CUIT (11 digits, business or registered individual).</summary>
    Cuit = 80,
    /// <summary>CUIL (11 digits, individual under employment).</summary>
    Cuil = 86,
    /// <summary>CDI.</summary>
    Cdi = 87,
    /// <summary>Libreta Cívica.</summary>
    LibretaCivica = 89,
    /// <summary>Libreta de Enrolamiento.</summary>
    LibretaEnrolamiento = 90,
    /// <summary>DNI (Argentine national ID).</summary>
    Dni = 96,
    /// <summary>Consumidor final (no document required).</summary>
    ConsumidorFinal = 99,
}
