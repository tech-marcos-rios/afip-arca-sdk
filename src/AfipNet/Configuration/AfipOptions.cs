namespace AfipNet.Configuration;

public class AfipOptions
{
    public const string SectionName = "Afip";

    public bool UsarHomologacion { get; set; } = true;

    /// <summary>Ruta al archivo .p12 del certificado digital emitido por AFIP.</summary>
    public string CertificadoPath { get; set; } = string.Empty;

    /// <summary>Contraseña del certificado .p12. Cargar desde variables de entorno, nunca hardcodeada.</summary>
    public string CertificadoPassword { get; set; } = string.Empty;

    /// <summary>CUIT del contribuyente (sin guiones).</summary>
    public string Cuit { get; set; } = string.Empty;

    /// <summary>Minutos antes del vencimiento en que se renueva el ticket de autorización. Por defecto 10.</summary>
    public int RenovarTicketAntesDe { get; set; } = 10;

    internal string WsaaUrl => UsarHomologacion
        ? "https://wsaahomo.afip.gov.ar/ws/services/LoginCms"
        : "https://wsaa.afip.gov.ar/ws/services/LoginCms";

    internal string WsfeUrl => UsarHomologacion
        ? "https://wswhomo.afip.gov.ar/wsfev1/service.asmx"
        : "https://servicios1.afip.gov.ar/wsfev1/service.asmx";
}
