# 03 — Afip.Arca.Sdk

> GitHub: [tech-marcos-rios/afip-arca-sdk](https://github.com/tech-marcos-rios/afip-arca-sdk)

SDK .NET para integración con los Web Services oficiales de **AFIP/ARCA** (Argentina).
Cubre autenticación (WSAA), facturación electrónica (WSFEv1) y retenciones del impuesto a las ganancias (cálculo RG 830 + reporte a SIRE).

[![Version](https://img.shields.io/badge/Version-1.0.2-blue)]() [![Targets](https://img.shields.io/badge/Targets-net8.0%20%7C%20netstandard2.0-purple)]() [![Tests](https://img.shields.io/badge/Tests-25%2F25-brightgreen)]() [![AFIP Homologación](https://img.shields.io/badge/AFIP_Homologaci%C3%B3n-validado-success)]() [![License](https://img.shields.io/badge/License-MIT-green)]()

> **v1.0.2** validado end-to-end contra AFIP homologación el 2026-05-15: WSAA → TA → FECAESolicitar → CAE real. Ver [CHANGELOG.md](CHANGELOG.md) y [docs/roadmap.md](docs/roadmap.md).

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

## Documentación

| Documento | Contenido |
|---|---|
| [docs/certificate-setup.md](docs/certificate-setup.md) | **Cómo obtener y configurar el certificado en ARCA** — paso a paso, end-to-end. Empezar por acá si nunca conectaste con AFIP. |
| [docs/roadmap.md](docs/roadmap.md) | **Pendientes priorizados y guía para retomar el desarrollo.** Punto de entrada para cualquier dev/IA que continúe el trabajo. |
| [CHANGELOG.md](CHANGELOG.md) | Historial de versiones (Keep a Changelog + SemVer). |
| [docs/architecture.md](docs/architecture.md) | Arquitectura, capas, ADRs. |
| [docs/afip-api-technical-summary.md](docs/afip-api-technical-summary.md) | Resumen técnico de los WS de AFIP. |
| [docs/claude-configuration.md](docs/claude-configuration.md) | Por qué y cómo está configurado Claude en este repo. |
| [docs/portfolio-summary.md](docs/portfolio-summary.md) | Resumen ejecutivo del proyecto. |
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

`dotnet pack` deja los `.nupkg`/`.snupkg` en **`C:\GLB\artifacts`** (configurado en [`Directory.Build.props`](Directory.Build.props)). La demo en `implementation/` consume desde ese mismo path vía [`NuGet.config`](implementation/NuGet.config) con `packageSourceMapping`.

---

## Estrategia de autenticación

Dos modos soportados, elegibles en tiempo de configuración:

| Modo | Cuándo usar | Cómo configurar |
|---|---|---|
| **Firma local con certificado X.509** | Caso por defecto. El SDK carga `.pfx`/`.p12` y firma el TRA con CMS PKCS#7. | `opts.UseLocalCertificateSigning(c => c.FromFile(...))` |
| **Provider externo** | Cuando la firma vive en un HSM, Key Vault o servicio remoto. | `opts.UseExternalTicketProvider(async (svc, ct) => myProvider.GetTaAsync(svc, ct))` |

El TA se cachea automáticamente en memoria por la dupla `(CUIT, service)` durante toda su validez (12 hs).

---

## Licencia

MIT. Ver `LICENSE`.
