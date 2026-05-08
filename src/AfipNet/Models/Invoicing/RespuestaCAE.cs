namespace AfipNet.Models.Invoicing;

public class RespuestaCAE
{
    public bool Exitoso { get; init; }
    public string? CAE { get; init; }
    public DateTime? FechaVencimientoCAE { get; init; }
    public long NumeroComprobante { get; init; }
    public List<string> Observaciones { get; init; } = [];
    public List<string> Errores { get; init; } = [];

    public static RespuestaCAE Fallida(string error) =>
        new() { Exitoso = false, Errores = [error] };
}
