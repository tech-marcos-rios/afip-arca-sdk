# Afip.Arca.Sdk

> GitHub: [tech-marcos-rios/afip-arca-sdk](https://github.com/tech-marcos-rios/afip-arca-sdk)

SDK .NET para integración con los Web Services oficiales de **AFIP/ARCA** (Argentina).
Cubre autenticación (WSAA), facturación electrónica (WSFEv1) y retenciones del impuesto a las ganancias (cálculo RG 830 + reporte a SIRE).

[![Version](https://img.shields.io/badge/Version-1.0.0-blue)]() [![Targets](https://img.shields.io/badge/Targets-net8.0%20%7C%20netstandard2.0-purple)]() [![Tests](https://img.shields.io/badge/Tests-28%2F28-brightgreen)]() [![AFIP Homologación](https://img.shields.io/badge/AFIP_Homologaci%C3%B3n-validado-success)]() [![License](https://img.shields.io/badge/License-MIT-green)]()

> Validado end-to-end contra AFIP homologación: WSAA → TA → FECAESolicitar → CAE real. Ver [CHANGELOG.md](CHANGELOG.md).

---

## ¿Qué resuelve?

Las integraciones con AFIP/ARCA son frecuentes en cualquier sistema de facturación argentino y todas tienen el mismo conjunto de problemas:

- Firmar TRA con CMS/PKCS#7 y obtener un TA cada 12 horas.
- Cachear el TA correctamente para no chocarse con `coe.alreadyAuthenticated`.
- Armar el SOAP de WSFEv1 con sus reglas aritméticas (los importes tienen que cerrar a la centésima).
- Distinguir errores de transporte, errores de negocio y observaciones.
- Calcular retenciones de ganancias siguiendo la escala vigente (RG 830 / 5423).
- Informar a SIRE.

Este SDK encapsula todo eso detrás de una superficie tipada, asincrónica, testeable e integrable con `IServiceCollection`/`IHttpClientFactory`/`ILogger`.

---

## Instalación

```bash
dotnet add package Afip.Arca.Sdk
```

Targets soportados:

- `net8.0`
- `netstandard2.0` (compatible con .NET Framework 4.7.2+ y .NET Core 3.1+)

---

## Inicio rápido

### 1. Registrar el SDK

```csharp
using Afip.Arca.Sdk.Configuration;

builder.Services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";

    opts.UseLocalCertificateSigning(c =>
        c.FromFile(@"C:\certs\contribuyente.pfx", password: "secret"));
});
```

> ¿Tu app maneja **más de un CUIT** (ej. un SaaS donde cada cliente tiene su propio certificado)?
> Existe un modo multi-tenant (`AddAfipClientFactory<TProvider>`) con un contenedor de DI
> aislado por CUIT — ver [docs/01-usage-guide.md](docs/01-usage-guide.md#4-camino-b--multi-tenant).

### 2. Emitir una factura B

```csharp
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;

public sealed class BillingService(IAfipClient afip)
{
    public async Task<string?> EmitInvoiceAsync(CancellationToken ct)
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(net: 10_000m, rate: VatRate.TwentyOne)
            .Build();

        var result = await afip.Invoicing.AuthorizeAsync(invoice, ct: ct);

        return result.IsSuccess
            ? result.Cae
            : throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Message)));
    }
}
```

### 3. Anular vía Nota de Crédito

```csharp
var nc = await afip.Invoicing.CancelAsync(
    original: new InvoiceReference(InvoiceType.FacturaB, PointOfSale: 1, Number: 42),
    totalToCancel: 12_100m,
    cancellationToken: ct);
```

### 4. Calcular retención de Ganancias (RG 830)

```csharp
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

var calc = afip.IncomeTaxCalculator.Calculate(new IncomeTaxWithholdingRequest(
    Regime: (int)IncomeTaxRegime.ProfessionalsAndTrades,
    PaymentDate: DateOnly.FromDateTime(DateTime.Today),
    CurrentPaymentAmount: 250_000m,
    AccumulatedMonthlyPayments: 0m,
    PreviouslyWithheld: 0m,
    IsRegistered: true));

Console.WriteLine($"Retener: ${calc.WithholdingAmount} (aplica: {calc.Applies})");
```

### 5. Informar la retención a SIRE

```csharp
using Afip.Arca.Sdk.IncomeTax.Reporting.Models;

var sireResult = await afip.Sire.IssueAsync(new WithholdingCertificateRequest(
    TaxCode: TaxCode.IncomeTax,
    Regime: (int)IncomeTaxRegime.ProfessionalsAndTrades,
    WithholdingDate: DateOnly.FromDateTime(DateTime.Today),
    WithheldCuit: "20987654321",
    TaxableBase: 250_000m,
    WithheldAmount: calc.WithholdingAmount,
    SourceComprobanteType: (int)InvoiceType.FacturaB,
    SourceComprobanteNumber: "00001-00000042",
    Condition: SubjectCondition.Registered), ct);
```

---

## Git Flow

GitHub Flow: `master` protegida (PR + CI en verde obligatorios, sin push directo), ramas `feature/*` / `fix/*` / `docs/*`, sin rama `develop` — apropiado para una librería versionada con SemVer donde `master` siempre debe ser publicable. Detalle completo en [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentación

| Documento | Contenido |
|---|---|
| [docs/01-usage-guide.md](docs/01-usage-guide.md) | **Guía de consumo del paquete** — instalación, configuración single/multi-tenant, los 3 servicios, manejo de errores. Empezar por acá si vas a integrar el SDK en tu app. |
| [docs/02-certificate-setup.md](docs/02-certificate-setup.md) | **Cómo obtener y configurar el certificado en ARCA** — paso a paso, end-to-end. Empezar por acá si nunca conectaste con AFIP. |
| [docs/03-afip-api-technical-summary.md](docs/03-afip-api-technical-summary.md) | Resumen técnico de los WS de AFIP. |
| [docs/04-architecture.md](docs/04-architecture.md) | Arquitectura, capas, ADRs. |
| [docs/06-release-process.md](docs/06-release-process.md) | Cómo cortar y publicar una versión nueva (tag → CI/CD → nuget.org). |
| [CHANGELOG.md](CHANGELOG.md) | Historial de versiones (Keep a Changelog + SemVer). |
| [implementation/README.md](implementation/README.md) | Demo interactiva de consumo del NuGet. |
| [scripts/README.md](scripts/README.md) | Script PowerShell para generar CSR + ensamblar PFX. |
| [.claude/CLAUDE.md](.claude/CLAUDE.md) | Lineamientos obligatorios para contribuir. |

---

## Demo interactiva

La carpeta [`implementation/`](implementation/) contiene una solución de consola que **consume el NuGet** (no usa `ProjectReference`) y ofrece un wizard interactivo con todas las operaciones del SDK. Sirve como verificación end-to-end del paquete y como referencia de uso para nuevos consumidores.

```powershell
cd implementation
dotnet run --project Afip.Arca.Sdk.Demo
```

---

## Artefactos

`dotnet pack` deja los `.nupkg`/`.snupkg` en **`D:\Code\projects\artifacts`** (configurado en [`Directory.Build.props`](Directory.Build.props)). La demo en `implementation/` consume desde ese mismo path vía [`NuGet.config`](implementation/NuGet.config) con `packageSourceMapping`.

---

## Estrategia de autenticación

Dos modos soportados, elegibles en tiempo de configuración:

| Modo | Cuándo usar | Cómo configurar |
|---|---|---|
| **Firma local con certificado X.509** | Caso por defecto. El SDK carga `.pfx`/`.p12` y firma el TRA con CMS PKCS#7. | `opts.UseLocalCertificateSigning(c => c.FromFile(...))` |
| **Provider externo** | Cuando la firma vive en un HSM, Key Vault o servicio remoto. | `opts.UseExternalTicketProvider(async (svc, ct) => myProvider.GetTaAsync(svc, ct))` |

El TA se cachea automáticamente en memoria por la dupla `(CUIT, service)` durante toda su validez (12 hs).

---

## Estado del proyecto

Release inicial (`1.0.0`). Validado end-to-end contra AFIP homologación:

- ✅ WSAA (`loginCms`) — autenticación y caché de TA.
- ✅ WSFEv1 — `FEDummy`, `FECompUltimoAutorizado`, `FECAESolicitar` (Factura B y Nota de Crédito B con `CbtesAsoc`).
- ⚠️ SIRE (retenciones) — implementado desde la especificación oficial, **todavía no probado contra AFIP real**. Tratalo como beta.

Pendiente:

- Validar SIRE contra AFIP real.
- `FECompConsultar` y `FEParamGet*` (con caché).
- Soporte multi-comprobante en una sola llamada a `FECAESolicitar`.

Detalle completo en [CHANGELOG.md](CHANGELOG.md).

---

## Licencia

MIT. Ver `LICENSE`.
