namespace AfipNet.Models.Auth;

public class AfipCredentials
{
    public string Cuit { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string Sign { get; init; } = string.Empty;
}
