using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using AfipNet.Configuration;
using AfipNet.Models.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfipNet.Authentication;

public class WsaaService : IWsaaService
{
    private readonly AfipOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<WsaaService> _logger;
    private readonly Dictionary<string, TicketAutorizacion> _cache = new();

    public WsaaService(IOptions<AfipOptions> options, HttpClient http, ILogger<WsaaService> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;
    }

    public async Task<TicketAutorizacion> ObtenerTicketAsync(string servicio, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(servicio, out var cached) && cached.EsValido(_options.RenovarTicketAntesDe))
        {
            _logger.LogDebug("Usando ticket cacheado para {Servicio}, expira {Expiracion}", servicio, cached.Expiracion);
            return cached;
        }

        _logger.LogInformation("Solicitando nuevo ticket de autorización para {Servicio}", servicio);

        var cms = GenerarCms(servicio);
        var soapResponse = await EnviarSolicitudAsync(cms, cancellationToken);
        var ticket = ParsearRespuesta(soapResponse, servicio);

        _cache[servicio] = ticket;
        return ticket;
    }

    private string GenerarCms(string servicio)
    {
        var cert = new X509Certificate2(_options.CertificadoPath, _options.CertificadoPassword);
        var loginTicketRequest = GenerarLoginTicketRequest(servicio);
        var bytes = Encoding.UTF8.GetBytes(loginTicketRequest);

        var contenido = new ContentInfo(bytes);
        var firmado = new SignedCms(contenido);
        var firmante = new CmsSigner(cert) { DigestAlgorithm = new Oid("SHA256") };
        firmado.ComputeSignature(firmante);

        return Convert.ToBase64String(firmado.Encode());
    }

    private static string GenerarLoginTicketRequest(string servicio)
    {
        var generationTime = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var expirationTime = DateTime.UtcNow.AddHours(12).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var uniqueId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <loginTicketRequest version="1.0">
              <header>
                <uniqueId>{uniqueId}</uniqueId>
                <generationTime>{generationTime}</generationTime>
                <expirationTime>{expirationTime}</expirationTime>
              </header>
              <service>{servicio}</service>
            </loginTicketRequest>
            """;
    }

    private async Task<string> EnviarSolicitudAsync(string cms, CancellationToken cancellationToken)
    {
        var soap = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov">
              <soapenv:Header/>
              <soapenv:Body>
                <wsaa:loginCms>
                  <wsaa:in0>{cms}</wsaa:in0>
                </wsaa:loginCms>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, _options.WsaaUrl)
        {
            Content = new StringContent(soap, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", "");

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static TicketAutorizacion ParsearRespuesta(string soapXml, string servicio)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapXml);

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsaa", "http://wsaa.view.sua.dvadac.desein.afip.gov");

        var loginReturn = doc.SelectSingleNode("//wsaa:loginCmsReturn", ns)?.InnerText
            ?? throw new InvalidOperationException("Respuesta WSAA sin loginCmsReturn");

        var taDoc = new XmlDocument();
        taDoc.LoadXml(loginReturn);

        var token = taDoc.SelectSingleNode("//token")?.InnerText ?? string.Empty;
        var sign = taDoc.SelectSingleNode("//sign")?.InnerText ?? string.Empty;
        var generationTimeStr = taDoc.SelectSingleNode("//generationTime")?.InnerText ?? string.Empty;
        var expirationTimeStr = taDoc.SelectSingleNode("//expirationTime")?.InnerText ?? string.Empty;

        return new TicketAutorizacion
        {
            Token = token,
            Sign = sign,
            Servicio = servicio,
            Generado = DateTime.Parse(generationTimeStr).ToUniversalTime(),
            Expiracion = DateTime.Parse(expirationTimeStr).ToUniversalTime()
        };
    }
}
