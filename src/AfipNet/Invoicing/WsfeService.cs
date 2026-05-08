using System.Text;
using System.Xml;
using AfipNet.Authentication;
using AfipNet.Configuration;
using AfipNet.Models.Invoicing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfipNet.Invoicing;

public class WsfeService : IWsfeService
{
    private readonly AfipOptions _options;
    private readonly HttpClient _http;
    private readonly IWsaaService _wsaa;
    private readonly ILogger<WsfeService> _logger;

    public WsfeService(IOptions<AfipOptions> options, HttpClient http, IWsaaService wsaa, ILogger<WsfeService> logger)
    {
        _options = options.Value;
        _http = http;
        _wsaa = wsaa;
        _logger = logger;
    }

    public async Task<RespuestaCAE> SolicitarCAEAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        var ticket = await _wsaa.ObtenerTicketAsync("wsfe", cancellationToken);
        var proximoNumero = await ObtenerUltimoNumeroAsync(comprobante.PuntoVenta, comprobante.TipoComprobante, cancellationToken) + 1;

        _logger.LogInformation("Solicitando CAE para comprobante tipo {Tipo} PV {PV} N° {Numero}",
            comprobante.TipoComprobante, comprobante.PuntoVenta, proximoNumero);

        var soap = BuildSolicitarCAESoap(comprobante, proximoNumero, ticket.Token, ticket.Sign);
        var response = await EnviarSoapAsync(soap, "FECAESolicitar", cancellationToken);

        return ParsearRespuestaCAE(response, proximoNumero);
    }

    public async Task<long> ObtenerUltimoNumeroAsync(int puntoVenta, int tipoComprobante, CancellationToken cancellationToken = default)
    {
        var ticket = await _wsaa.ObtenerTicketAsync("wsfe", cancellationToken);

        var soap = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
              <soapenv:Header/>
              <soapenv:Body>
                <ar:FECompUltimoAutorizado>
                  <ar:Auth>
                    <ar:Token>{ticket.Token}</ar:Token>
                    <ar:Sign>{ticket.Sign}</ar:Sign>
                    <ar:Cuit>{_options.Cuit}</ar:Cuit>
                  </ar:Auth>
                  <ar:PtoVta>{puntoVenta}</ar:PtoVta>
                  <ar:CbteTipo>{tipoComprobante}</ar:CbteTipo>
                </ar:FECompUltimoAutorizado>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        var response = await EnviarSoapAsync(soap, "FECompUltimoAutorizado", cancellationToken);

        var doc = new XmlDocument();
        doc.LoadXml(response);
        var nroNode = doc.SelectSingleNode("//*[local-name()='CbteNro']");
        return long.TryParse(nroNode?.InnerText, out var nro) ? nro : 0;
    }

    public async Task<bool> VerificarDisponibilidadAsync(CancellationToken cancellationToken = default)
    {
        var soap = """
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
              <soapenv:Header/>
              <soapenv:Body><ar:FEDummy/></soapenv:Body>
            </soapenv:Envelope>
            """;

        try
        {
            var response = await EnviarSoapAsync(soap, "FEDummy", cancellationToken);
            return response.Contains("OK");
        }
        catch
        {
            return false;
        }
    }

    private string BuildSolicitarCAESoap(Comprobante c, long numero, string token, string sign)
    {
        var fecha = c.FechaComprobante.ToString("yyyyMMdd");
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
              <soapenv:Header/>
              <soapenv:Body>
                <ar:FECAESolicitar>
                  <ar:Auth>
                    <ar:Token>{token}</ar:Token>
                    <ar:Sign>{sign}</ar:Sign>
                    <ar:Cuit>{_options.Cuit}</ar:Cuit>
                  </ar:Auth>
                  <ar:FeCAEReq>
                    <ar:FeCabReq>
                      <ar:CantReg>1</ar:CantReg>
                      <ar:PtoVta>{c.PuntoVenta}</ar:PtoVta>
                      <ar:CbteTipo>{c.TipoComprobante}</ar:CbteTipo>
                    </ar:FeCabReq>
                    <ar:FeDetReq>
                      <ar:FECAEDetRequest>
                        <ar:Concepto>{c.Concepto}</ar:Concepto>
                        <ar:DocTipo>80</ar:DocTipo>
                        <ar:DocNro>{c.CuitReceptor}</ar:DocNro>
                        <ar:CbteDesde>{numero}</ar:CbteDesde>
                        <ar:CbteHasta>{numero}</ar:CbteHasta>
                        <ar:CbteFch>{fecha}</ar:CbteFch>
                        <ar:ImpTotal>{c.ImporteTotal:F2}</ar:ImpTotal>
                        <ar:ImpTotConc>{c.ImporteNoGravado:F2}</ar:ImpTotConc>
                        <ar:ImpNeto>{c.ImporteNeto:F2}</ar:ImpNeto>
                        <ar:ImpOpEx>{c.ImporteExento:F2}</ar:ImpOpEx>
                        <ar:ImpIVA>{c.ImporteIVA:F2}</ar:ImpIVA>
                        <ar:ImpTrib>0.00</ar:ImpTrib>
                        <ar:MonId>{c.MonedaId}</ar:MonId>
                        <ar:MonCotiz>{c.CotizacionMoneda:F2}</ar:MonCotiz>
                      </ar:FECAEDetRequest>
                    </ar:FeDetReq>
                  </ar:FeCAEReq>
                </ar:FECAESolicitar>
              </soapenv:Body>
            </soapenv:Envelope>
            """;
    }

    private async Task<string> EnviarSoapAsync(string soap, string action, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.WsfeUrl)
        {
            Content = new StringContent(soap, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", action);

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static RespuestaCAE ParsearRespuestaCAE(string soapXml, long numero)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapXml);

        var resultado = doc.SelectSingleNode("//*[local-name()='Resultado']")?.InnerText;
        if (resultado != "A")
        {
            var errores = doc.SelectNodes("//*[local-name()='Err']")?
                .Cast<XmlNode>()
                .Select(n => $"[{n.SelectSingleNode("*[local-name()='Code']")?.InnerText}] {n.SelectSingleNode("*[local-name()='Msg']")?.InnerText}")
                .ToList() ?? [];

            return new RespuestaCAE { Exitoso = false, Errores = errores, NumeroComprobante = numero };
        }

        var cae = doc.SelectSingleNode("//*[local-name()='CAE']")?.InnerText ?? string.Empty;
        var vtoStr = doc.SelectSingleNode("//*[local-name()='CAEFchVto']")?.InnerText ?? string.Empty;

        var observaciones = doc.SelectNodes("//*[local-name()='Obs']")?
            .Cast<XmlNode>()
            .Select(n => $"[{n.SelectSingleNode("*[local-name()='Code']")?.InnerText}] {n.SelectSingleNode("*[local-name()='Msg']")?.InnerText}")
            .ToList() ?? [];

        return new RespuestaCAE
        {
            Exitoso = true,
            CAE = cae,
            FechaVencimientoCAE = DateTime.TryParseExact(vtoStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var vto) ? vto : null,
            NumeroComprobante = numero,
            Observaciones = observaciones
        };
    }
}
