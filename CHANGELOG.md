# Changelog

Todos los cambios notables del paquete `Afip.Arca.Sdk` están documentados acá.
Sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [SemVer 2.0.0](https://semver.org/lang/es/).

## [Unreleased]

### Pendiente sin versión asignada

- Validar el módulo SIRE contra AFIP real (hoy implementado desde spec pero no probado end-to-end).
- Cubrir métodos faltantes de WSFEv1: `FECompConsultar`, `FEParamGet*` (con caché de 24 h).
- Soporte multi-comprobante en una sola llamada a `FECAESolicitar`.

---

## [1.0.0] — 2026-08-25

### Added — Release inicial

#### Autenticación (WSAA)

- `IAccessTicketProvider` con dos implementaciones (Strategy pattern):
  - `WsaaAccessTicketProvider`: firma local con certificado X.509, CMS PKCS#7 SHA-256.
  - `ExternalAccessTicketProvider`: delega la firma a un proveedor externo (HSM, Key Vault).
- `IAccessTicketCache` con `InMemoryAccessTicketCache` por defecto; keyed por `(CUIT, service)`. TA cacheado 12 horas con leeway configurable (`TicketRefreshLeewayMinutes`).
- `TraDocumentBuilder` y `Pkcs7TraSigner` aislados (SRP). `SOAPAction: ""` (vacío) soportado explícitamente, tal como lo exige `loginCms`.
- **Reintento automático ante token inválido/vencido:** `IInvalidatableAccessTicketProvider` (interfaz opcional, implementada por los dos providers built-in) permite a `InvoiceService` invalidar el TA cacheado y reintentar **una sola vez** cuando WSFEv1 devuelve el error `1000` — tanto en `FECompUltimoAutorizado` (vía `AfipBusinessException`) como en `FECAESolicitar` (vía `InvoiceAuthorizationResult.Errors`). Fuera de alcance: `SireService` no lo tiene todavía (wire format de SIRE sin validar, ver Unreleased).

#### Facturación electrónica (WSFEv1)

- `IInvoiceService` con `AuthorizeAsync`, `CancelAsync`, `GetLastAuthorizedNumberAsync`, `HealthCheckAsync`.
- `InvoiceBuilder` fluido con validación previa; `InvoiceValidator` para chequeos pre-vuelo (importes que cierran, fechas, breakdown por tipo).
- Modelos: `Invoice`, `InvoiceType`, `DocumentType`, `Concept`, `VatRate`, `VatLine`, `Currency`, `InvoiceReference`, `ReceiverVatCondition` (campo `CondicionIVAReceptorId`, obligatorio desde RG 5616/2024, con defaults inteligentes en el builder según el tipo de receptor).
- `InvoiceAuthorizationResult` con `IsSuccess`, `Cae`, `CaeExpiration`, `AssignedNumber`, `PointOfSale`, `Type`, `Observations`, `Errors`.
- Soporte para Factura A/B/C/M y sus respectivas Notas de Crédito y Débito. Anulación implementada como emisión de NC con `CbtesAsoc` referenciando al original.

#### Retenciones de Ganancias (RG 830)

- `IIncomeTaxCalculator` con cálculo offline (síncrono, sin I/O) siguiendo RG 830/2000.
- `IIncomeTaxScaleProvider` con `BuiltInIncomeTaxScaleProvider` embebiendo RG 5423 (vigente 2024-10).
- Soporta: acumulación mensual, mínimo no imponible, escala progresiva, alícuota fija para no inscriptos, descuento de retenciones previas, mínimo a retener.

#### Reporte a SIRE

- `ISireService` con `IssueAsync`, `CancelAsync`, `GetAsync`.
- Implementado desde la spec oficial; **no validado contra AFIP real todavía** (ver Unreleased).

#### Multi-tenancy

- `IAfipClientFactory` + `ITenantOptionsProvider` (namespace `Afip.Arca.Sdk.MultiTenancy`) para apps que sirven N contribuyentes (CUITs y certificados distintos) desde el mismo proceso.
  - `services.AddAfipClientFactory<TProvider>()` registra la fábrica dinámica en DI.
  - `IAfipClientFactory.GetClientAsync(tenantId)` resuelve (o crea lazily) el cliente para el tenant dado, en un contenedor de DI hijo aislado (`DynamicAfipClientFactory`), sin necesidad de reiniciar la aplicación.
  - `IAfipClientFactory.InvalidateClient(tenantId)` fuerza recarga desde el provider al actualizar un certificado.
  - `TenantNotFoundException` cuando el provider devuelve `null` para un tenant.
  - `TenantAfipOptions` record con las opciones desencriptadas por tenant; `ITenantOptionsProvider` es la interfaz que el consumidor implementa contra su propia BD/disco (la desencriptación es responsabilidad del consumidor, no del SDK).

#### Infraestructura

- Multi-target `net8.0;netstandard2.0` (`PolySharp` + `Portable.System.DateTimeOnly` para NS2.0).
- `HttpSoapInvoker` con `IHttpClientFactory` + Polly (retry exponencial 3× + timeout 30s).
- Jerarquía de excepciones tipadas: `AfipException` → `AfipAuthenticationException`, `AfipTransportException`, `AfipBusinessException`, `AfipValidationException`. Errores de negocio (rechazos AFIP) devueltos en `InvoiceAuthorizationResult`/`WithholdingCertificateResult` — NO como excepciones.
- `IClock` inyectable para tests determinísticos.
- DI vía `services.AddAfipSdk(opts => ...)` (single-tenant) o `services.AddAfipClientFactory<TProvider>()` (multi-tenant), con Options pattern + `IValidateOptions`.

### Validación

Validado end-to-end contra AFIP homologación:

- WSAA `loginCms` → TA real obtenido.
- WSFEv1 `FEDummy` → `AppServer=OK, DbServer=OK, AuthServer=OK`.
- WSFEv1 `FECompUltimoAutorizado` → validado.
- WSFEv1 `FECAESolicitar` (Factura B → Consumidor Final) → CAE real obtenido.
- WSFEv1 `FECAESolicitar` (Nota de Crédito B con `CbtesAsoc`) → CAE real obtenido.

28/28 tests unitarios pasando (xUnit + FluentAssertions + NSubstitute), sin tocar red — los SOAP clients se testean mockeando `IHttpSoapInvoker`.

[Unreleased]: https://github.com/tech-marcos-rios/afip-arca-sdk/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/tech-marcos-rios/afip-arca-sdk/releases/tag/v1.0.0
