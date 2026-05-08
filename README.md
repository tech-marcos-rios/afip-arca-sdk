# AfipNet

Cliente .NET para los Web Services de AFIP/ARCA.
Cubre autenticación (WSAA) y facturación electrónica (WSFE) con soporte nativo para homologación y producción.

[![NuGet](https://img.shields.io/nuget/v/AfipNet.svg)](https://www.nuget.org/packages/AfipNet)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Instalación

```bash
dotnet add package AfipNet
```

## Configuración

### 1. appsettings.json

```json
{
  "Afip": {
    "UsarHomologacion": true,
    "CertificadoPath": "/ruta/al/certificado.p12",
    "CertificadoPassword": "",
    "Cuit": "20123456789"
  }
}
```

> **Importante**: la contraseña del certificado nunca debe estar en `appsettings.json`.
> Usá variables de entorno: `Afip__CertificadoPassword=tu_contraseña`

### 2. Program.cs

```csharp
builder.Services.AddAfipNet(builder.Configuration);
```

## Uso

### Facturación electrónica

```csharp
public class FacturacionService
{
    private readonly IWsfeService _wsfe;

    public FacturacionService(IWsfeService wsfe) => _wsfe = wsfe;

    public async Task<string?> EmitirFacturaAsync()
    {
        var comprobante = new Comprobante
        {
            PuntoVenta = 1,
            TipoComprobante = 11,       // Factura C
            CuitReceptor = 99999999999, // Consumidor final
            ImporteTotal = 1210.00m,
            ImporteNeto = 1000.00m,
            ImporteIVA = 210.00m,
            Concepto = 2               // Servicios
        };

        var resultado = await _wsfe.SolicitarCAEAsync(comprobante);

        if (resultado.Exitoso)
        {
            Console.WriteLine($"CAE: {resultado.CAE}");
            Console.WriteLine($"Vence: {resultado.FechaVencimientoCAE:dd/MM/yyyy}");
            return resultado.CAE;
        }

        foreach (var error in resultado.Errores)
            Console.WriteLine($"Error: {error}");

        return null;
    }
}
```

### Verificar disponibilidad del servicio

```csharp
var disponible = await wsfe.VerificarDisponibilidadAsync();
```

## Tipos de comprobante comunes

| Código | Tipo |
|--------|------|
| 1 | Factura A |
| 2 | Nota de Débito A |
| 3 | Nota de Crédito A |
| 6 | Factura B |
| 11 | Factura C |
| 51 | Factura M |

## Requisitos previos

1. **Certificado digital**: solicitarlo en el portal de AFIP con clave fiscal nivel 3.
2. **Servicio habilitado**: activar el servicio "Facturación Electrónica" en Administración de Relaciones (AFIP).
3. **Punto de venta**: dado de alta en AFIP como "Web Services".

## Roadmap

- [x] WSAA — Autenticación y Autorización
- [x] WSFE — Facturación Electrónica (CAE)
- [ ] WSFEX — Facturación de Exportaciones
- [ ] WSMTXCA — Comprobantes con detalle de items
- [ ] WSFECRED — Factura de Crédito Electrónica MiPyMEs
- [ ] Caché persistente (Redis / memory cache configurable)

## Contribuciones

Pull requests bienvenidos. Para cambios grandes, abrí un issue primero para discutir qué querés cambiar.

## Licencia

MIT — ver [LICENSE](LICENSE)
