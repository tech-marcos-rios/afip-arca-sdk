namespace AfipNet.Models.Auth;

public class TicketAutorizacion
{
    public string Token { get; init; } = string.Empty;
    public string Sign { get; init; } = string.Empty;
    public DateTime Generado { get; init; }
    public DateTime Expiracion { get; init; }
    public string Servicio { get; init; } = string.Empty;

    public bool EsValido(int margenMinutos = 0) =>
        DateTime.UtcNow.AddMinutes(margenMinutos) < Expiracion;
}
