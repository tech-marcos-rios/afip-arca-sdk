# Changelog

Todos los cambios notables del paquete `Afip.Arca.Sdk` están documentados acá.
Sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [SemVer 2.0.0](https://semver.org/lang/es/).

## [Unreleased]

### Pendiente sin versión asignada

- Validar el módulo SIRE contra AFIP real (hoy implementado desde spec pero no probado end-to-end).
- Cubrir métodos faltantes de WSFEv1: `FECompConsultar`, `FEParamGet*` (con caché de 24 h).
- Soporte multi-comprobante en una sola llamada a `FECAESolicitar`.

---

## [1.1.0] — 2026-06-03

### Added

- **Multi-tenancy:** `IAfipClientFactory` + `ITenantOptionsProvider` en el namespace
  `Afip.Arca.Sdk.MultiTenancy`.
  - `services.AddAfipClientFactory<TProvider>()` registra la fábrica dinámica en DI.
  - `IAfipClientFactory.GetClientAsync(tenantId)` resuelve (o crea lazily) el cliente
    para el tenant dado, sin necesidad de reiniciar la aplicación.
  - `IAfipClientFactory.InvalidateClient(tenantId)` fuerza recarga desde el provider
    al actualizar un certificado.
  - `TenantNotFoundException` cuando el provider devuelve `null` para un tenant.
  - `TenantAfipOptions` record: opciones desencriptadas por tenant.
  - `ITenantOptionsProvider` interfaz que el consumidor implementa contra su BD/disco.
  - `DynamicAfipClientFactory`: crea un DI child container por tenant; comparte
    `IHttpClientFactory` e `ILoggerFactory` del container raíz.
- **`ServiceCollectionExtensions.AddAfipClientFactory<TProvider>()`** (nueva API pública).
- **`ServiceCollectionExtensions.RegisterAfipSdkServices()`** extraído como `internal`
  para reutilización en child containers sin re-registrar el HttpClient.
- **Demo multi-tenant** en `implementation/`: SQLite + AES-256-GCM para certificados,
  `TenantOnboardingService` (registro/actualización/desactivación dinámica de tenants),
  nuevo menú mode `[2] Multi-tenant`.

### Changed

- `ServiceCollectionExtensions.AddAfipSdk()`: sin breaking changes; internamente
  delega a `RegisterHttpClient()` + `RegisterAfipSdkServices()`.
- Demo (`implementation/`): versión bump a 1.1.0; agrega `Microsoft.EntityFrameworkCore.Sqlite`.

### Migration guide (single-tenant → multi-tenant)

```csharp
// Antes (single-tenant, sin cambios requeridos)
services.AddAfipSdk(opts => { opts.Cuit = "..."; ... });
var client = sp.GetRequiredService<IAfipClient>();

// Ahora (multi-tenant)
services.AddAfipClientFactory<MiTenantOptionsProvider>();
// MiTenantOptionsProvider : ITenantOptionsProvider → lee de tu BD y desencripta cert

var factory = sp.GetRequiredService<IAfipClientFactory>();
var client = await factory.GetClientAsync(userId, ct);
```

---

## [1.0.2] — 2026-05-15

### Added

- **`ReceiverVatCondition`** (enum `CondicionIVAReceptorId`) en el modelo `Invoice`. Obligatorio desde **RG 5616/2024**.
- **`InvoiceBuilder.WithReceiverVatCondition(...)`** para overridear la condición.
- Defaults inteligentes en el builder según el tipo de receptor:
  - `ToConsumerFinal()` → `ConsumerFinal` (5)
  - `ToCuit(...)` → `RegisteredVat` (1)
  - `ToDni(...)` → `ConsumerFinal` (5)
- El XML del request `FECAESolicitar` ahora emite `<CondicionIVAReceptorId>` automáticamente.

### Fixed

- AFIP rechazaba todas las emisiones con observación `10246: "Campo Condicion Frente al IVA del receptor es obligatorio conforme a lo reglamentado por la Resolucion General Nro 5616"`.

### Validación

- Primera emisión real contra AFIP homologación: **FacturaB 0001-00000001 → CAE 86200173262441** (CUIT 20261234921).
- Anulación vía Nota de Crédito: **NotaCreditoB 0001-00000001 → CAE 86200173263879**.

---

## [1.0.1] — 2026-05-15

### Fixed

- **`HttpSoapInvoker`** rechazaba `soapAction` vacío con `ArgumentException("SOAP action required.")`. WSAA `loginCms` requiere `SOAPAction: ""` (vacío entre comillas) por especificación — el validador era más estricto que el estándar SOAP 1.1.
- Ahora `soapAction` acepta `null` o string vacío y se envía como `SOAPAction: ""`.

### Validación

- Primer health-check real contra AFIP: `FEDummy` → `AppServer=OK, DbServer=OK, AuthServer=OK`.
- Primer TA real obtenido de WSAA en homologación.

---

## [1.0.0] — 2026-05-13

### Added — Release inicial

#### Autenticación (WSAA)

- `IAccessTicketProvider` con dos implementaciones (Strategy pattern):
  - `WsaaAccessTicketProvider`: firma local con certificado X.509, CMS PKCS#7 SHA-256.
  - `ExternalAccessTicketProvider`: delega la firma a un proveedor externo (HSM, Key Vault).
- `IAccessTicketCache` con `InMemoryAccessTicketCache` por defecto; keyed por `(CUIT, service)`.
- `TraDocumentBuilder` y `Pkcs7TraSigner` aislados (SRP).
- TA cacheado por 12 horas con leeway configurable.

#### Facturación electrónica (WSFEv1)

- `IInvoiceService` con `AuthorizeAsync`, `CancelAsync`, `GetLastAuthorizedNumberAsync`, `HealthCheckAsync`.
- `InvoiceBuilder` fluido con validación previa.
- `InvoiceValidator` para chequeos pre-vuelo (importes que cierran, fechas, breakdown por tipo).
- Modelos: `Invoice`, `InvoiceType`, `DocumentType`, `Concept`, `VatRate`, `VatLine`, `Currency`, `InvoiceReference`.
- `InvoiceAuthorizationResult` con `IsSuccess`, `Cae`, `CaeExpiration`, `Observations`, `Errors`.
- Soporte para Factura A/B/C/M y sus respectivas Notas de Crédito y Débito.
- Anulación implementada como emisión de NC con `CbtesAsoc` referenciando al original.

#### Retenciones de Ganancias (RG 830)

- `IIncomeTaxCalculator` con cálculo offline siguiendo RG 830/2000.
- `IIncomeTaxScaleProvider` con `BuiltInIncomeTaxScaleProvider` embebiendo RG 5423 (vigente 2024-10).
- Soporta: acumulación mensual, mínimo no imponible, escala progresiva, alícuota fija para no inscriptos, descuento de retenciones previas, mínimo a retener.

#### Reporte a SIRE

- `ISireService` con `IssueAsync`, `CancelAsync`, `GetAsync`.
- Implementado desde la spec oficial; **no validado contra AFIP real todavía**.

#### Infraestructura

- Multi-target `net8.0;netstandard2.0` (`PolySharp` + `Portable.System.DateTimeOnly` para NS2.0).
- `HttpSoapInvoker` con `IHttpClientFactory` + Polly (retry exponencial 3× + timeout 30s).
- Jerarquía de excepciones tipadas: `AfipException` → `AfipAuthenticationException`, `AfipTransportException`, `AfipBusinessException`, `AfipValidationException`.
- Errores de negocio (rechazos AFIP) devueltos en `InvoiceAuthorizationResult` — NO como excepciones.
- `IClock` inyectable para tests determinísticos.
- DI vía `services.AddAfipSdk(opts => ...)` con Options pattern + `IValidateOptions`.

#### Documentación

- README, architecture.md, afip-api-technical-summary.md, claude-configuration.md, portfolio-summary.md.
- Carpeta `implementation/` con demo de consumo del NuGet.
- Carpeta `scripts/` con `New-AfipCertificate.ps1` para generar CSR/PFX.

[Unreleased]: https://github.com/marcosrios/afip-arca-sdk/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/marcosrios/afip-arca-sdk/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/marcosrios/afip-arca-sdk/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/marcosrios/afip-arca-sdk/releases/tag/v1.0.0
