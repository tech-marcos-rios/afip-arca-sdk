# Afip.Arca.Sdk — Documento de Implementación

> **Propósito de este documento:** descripción técnica completa del paquete NuGet
> `Afip.Arca.Sdk` (v1.1.0), incluyendo arquitectura, decisiones de diseño, API pública,
> implementación multi-tenant y guía de integración. Preparado para evaluación externa.

---

## 1. Qué es el paquete

`Afip.Arca.Sdk` es una librería .NET que encapsula la integración con los Web Services
fiscales de **AFIP/ARCA** (Argentina). Convierte operaciones que requieren SOAP artesanal,
certificados X.509, firma criptográfica PKCS#7 y XML de formato exacto en una API
idiomática, tipada y compatible con el ecosistema Microsoft.Extensions.

### Problemas que resuelve

| Área | Qué hace el SDK |
|---|---|
| **Autenticación (WSAA)** | Firma el TRA con el certificado X.509 del contribuyente, obtiene el Ticket de Acceso, lo cachea 12 hs y lo renueva automáticamente 5 min antes del vencimiento |
| **Facturación (WSFEv1)** | Emite Facturas A/B/C/M, Notas de Crédito/Débito; numera automáticamente; valida antes de llamar a AFIP |
| **Retenciones (RG 830)** | Calcula offline la retención de Ganancias con escala progresiva (RG 5423 vigente) |
| **Reporte (SIRE)** | Emite, consulta y anula certificados de retención vía SOAP |
| **Multi-tenancy** | Una sola aplicación puede manejar N contribuyentes con CUITs y certificados distintos, sin reiniciar |

---

## 2. Stack técnico

| Aspecto | Tecnología |
|---|---|
| Lenguaje | C# 12 |
| Target frameworks | `net8.0` + `netstandard2.0` (compatible con .NET Framework 4.7.2+) |
| DI | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| Configuración | `Microsoft.Extensions.Options` + `IValidateOptions<T>` |
| Logging | `Microsoft.Extensions.Logging.Abstractions` |
| HTTP | `IHttpClientFactory` (Microsoft.Extensions.Http) |
| Resiliencia | Polly — retry exponencial 3× + timeout 30 s |
| Criptografía | `System.Security.Cryptography.Pkcs` (SignedCms / PKCS#7 SHA-256, nativo en .NET) |
| SOAP/XML | `System.Xml.Linq.XDocument` (manual, sin code-gen) |
| Tests | xUnit + FluentAssertions + NSubstitute |

---

## 3. Arquitectura

### Capas (Dependency Rule)

```
┌──────────────────────────────────────────────────┐
│              APLICACIÓN CONSUMIDORA              │
│        (inyecta IAfipClient o IAfipClientFactory) │
└───────────────────┬──────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────┐
│                  FACADE                          │
│   IAfipClient → Invoicing, IncomeTax, Sire       │
│   IAfipClientFactory → GetClientAsync(tenantId)  │
└────┬──────────────┬──────────────┬───────────────┘
     │              │              │
     ▼              ▼              ▼
┌─────────┐  ┌──────────┐  ┌──────────────┐
│Invoicing│  │IncomeTax │  │     SIRE     │
│ Service │  │Calculator│  │   Service    │
└────┬────┘  └──────────┘  └──────┬───────┘
     │                            │
     └──────────────┬─────────────┘
                    ▼
        ┌───────────────────────┐
        │     DOMAIN LAYER      │
        │  Invoice, AccessTicket│
        │  IncomeTaxResult…     │
        └──────────┬────────────┘
                   │
     ┌─────────────┴──────────────┐
     ▼                            ▼
┌─────────────────┐   ┌──────────────────────────┐
│  ABSTRACCIONES  │   │      INFRAESTRUCTURA     │
│IAccessTicketPro-│   │WsaaAccessTicketProvider  │
│vider            │   │Pkcs7TraSigner            │
│IAccessTicketCa- │   │HttpSoapInvoker (Polly)   │
│che              │   │WsaaSoapClient            │
│IHttpSoapInvoker │   │WsfeSoapClient            │
│IClock           │   │InMemoryAccessTicketCache │
└─────────────────┘   │SystemClock               │
                      └──────────────────────────┘
```

**Regla fundamental:** el dominio no depende de SOAP, HTTP ni infraestructura. Los
adaptadores SOAP implementan interfaces definidas por el dominio.

### Patrones aplicados

| Patrón | Aplicación concreta |
|---|---|
| **Facade** | `AfipClient` agrupa los 3 servicios. `IAfipClientFactory` es la facade multi-tenant |
| **Strategy** | `IAccessTicketProvider`: firma local (X.509) vs. proveedor externo (HSM/Key Vault) |
| **Adapter** | `WsfeSoapClient`, `WsaaSoapClient`, `SireSoapClient` aíslan SOAP del dominio |
| **Builder** | `InvoiceBuilder` fluent con defaults inteligentes por tipo de receptor |
| **Result Object** | `InvoiceAuthorizationResult` con `IsSuccess`, `Errors`, `Observations` — los errores de negocio de AFIP no son excepciones |
| **Repository/Cache** | `IAccessTicketCache` (default: `InMemoryAccessTicketCache`) |
| **Options Pattern** | `AfipOptions` + `AfipOptionsValidator` (Microsoft.Extensions.Options) |

---

## 4. Estructura de archivos del SDK

```
src/Afip.Arca.Sdk/
│
├── IAfipClient.cs                      ← Facade: Invoicing + IncomeTax + Sire
├── AfipClient.cs
│
├── MultiTenancy/                       ← Módulo multi-tenant (v1.1.0)
│   ├── TenantAfipOptions.cs            ← record: datos de un tenant ya desencriptados
│   ├── ITenantOptionsProvider.cs       ← interfaz que el consumidor implementa
│   ├── IAfipClientFactory.cs           ← GetClientAsync / InvalidateClient
│   ├── TenantNotFoundException.cs      ← excepción tipada
│   └── DynamicAfipClientFactory.cs     ← implementación con child containers y caché
│
├── Configuration/
│   ├── AfipOptions.cs                  ← CUIT, Environment, CertificateSigning
│   ├── AfipEnvironment.cs              ← enum: Homologation | Production
│   ├── AfipEndpoints.cs                ← URLs de WSAA y WSFEv1
│   ├── AfipOptionsValidator.cs
│   └── ServiceCollectionExtensions.cs  ← AddAfipSdk() + AddAfipClientFactory<T>()
│
├── Authentication/
│   ├── IAccessTicketProvider.cs
│   ├── IAccessTicketCache.cs
│   ├── AccessTicket.cs                 ← record: Token + Sign + ExpirationTime
│   ├── WsaaAccessTicketProvider.cs     ← firma local con SemaphoreSlim gate
│   ├── ExternalAccessTicketProvider.cs ← delega a Func<string, CT, Task<AccessTicket>>
│   ├── InMemoryAccessTicketCache.cs    ← keyed por (CUIT, service)
│   └── Cms/
│       ├── ITraSigner.cs
│       ├── Pkcs7TraSigner.cs           ← SignedCms SHA-256 nativo
│       └── TraDocumentBuilder.cs       ← construye el XML del TRA
│
├── Invoicing/
│   ├── IInvoiceService.cs
│   ├── InvoiceService.cs
│   ├── InvoiceBuilder.cs               ← fluent builder con validación implícita
│   ├── Models/
│   │   ├── Invoice.cs                  ← record inmutable
│   │   ├── InvoiceType.cs              ← enum: FacturaA=1, FacturaB=6, NotaCreditoA=3…
│   │   ├── InvoiceAuthorizationResult.cs ← IsSuccess + CAE + Errors + Observations
│   │   ├── VatRate.cs / VatLine.cs
│   │   ├── ReceiverVatCondition.cs     ← RG 5616/2024 obligatorio
│   │   └── …
│   ├── Validation/
│   │   └── InvoiceValidator.cs         ← valida antes de llamar a AFIP
│   └── Soap/
│       └── WsfeSoapClient.cs           ← FECAESolicitar + FECompUltimoAutorizado + FEDummy
│
├── IncomeTax/
│   ├── Calculation/
│   │   ├── IIncomeTaxCalculator.cs
│   │   ├── IncomeTaxCalculator.cs      ← escala progresiva RG 830
│   │   ├── IIncomeTaxScaleProvider.cs
│   │   ├── BuiltInIncomeTaxScaleProvider.cs  ← RG 5423 (2024-10) embebida
│   │   └── Models/ (IncomeTaxWithholdingRequest / Result / Scale…)
│   └── Reporting/
│       ├── ISireService.cs
│       ├── SireService.cs
│       ├── Models/ (WithholdingCertificateRequest / Result…)
│       └── Soap/SireSoapClient.cs
│
└── Common/
    ├── Exceptions/                     ← AfipException → Auth / Business / Transport / Validation
    ├── Soap/HttpSoapInvoker.cs         ← IHttpClientFactory + Polly
    └── Time/IClock.cs + SystemClock.cs ← inyectable para tests determinísticos
```

---

## 5. API pública principal

### 5.1 Registro en DI — modo single-tenant

```csharp
// Program.cs
builder.Services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation; // o Production
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(c =>
        c.FromFile(@"C:\certs\contribuyente.pfx", password: "mi-pass"));
});
```

Variante con provider externo (HSM, Key Vault):

```csharp
opts.UseExternalTicketProvider(async (service, ct) =>
{
    // Llamar al HSM / Key Vault y retornar el TA ya firmado
    return new AccessTicket(Service: service, Cuit: cuit,
        Token: "...", Sign: "...",
        GenerationTime: DateTimeOffset.UtcNow,
        ExpirationTime: DateTimeOffset.UtcNow.AddHours(12));
});
```

### 5.2 Registro en DI — modo multi-tenant

```csharp
// Program.cs
builder.Services.AddAfipClientFactory<MiTenantOptionsProvider>();
// MiTenantOptionsProvider : ITenantOptionsProvider → implementada por el consumidor
```

### 5.3 Uso en código de aplicación

**Single-tenant:**
```csharp
public class FacturacionService(IAfipClient afip)
{
    public async Task<string> EmitirAsync(CancellationToken ct)
    {
        var factura = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(net: 10_000m, rate: VatRate.TwentyOne)
            .Build();

        var result = await afip.Invoicing.AuthorizeAsync(factura, ct: ct);

        return result.IsSuccess
            ? result.Cae!
            : throw new Exception(string.Join("; ", result.Errors));
    }
}
```

**Multi-tenant:**
```csharp
public class FacturacionService(IAfipClientFactory factory)
{
    public async Task<string> EmitirAsync(string tenantId, CancellationToken ct)
    {
        // El cliente se crea lazily en el primer uso de este tenantId.
        // Si el tenant se agrega mientras la app corre, funciona sin restart.
        var afip = await factory.GetClientAsync(tenantId, ct);

        var factura = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(net: 10_000m, rate: VatRate.TwentyOne)
            .Build();

        var result = await afip.Invoicing.AuthorizeAsync(factura, ct: ct);
        return result.IsSuccess ? result.Cae! : throw new Exception("...");
    }
}
```

### 5.4 Health check

```csharp
var (app, db, auth) = await afip.Invoicing.HealthCheckAsync(ct);
// app/db/auth == "OK" cuando el servicio de AFIP está disponible
```

### 5.5 Anulación (Nota de Crédito automática)

```csharp
var nc = await afip.Invoicing.CancelAsync(
    original: new InvoiceReference(InvoiceType.FacturaB, PointOfSale: 1, Number: 42),
    totalToCancel: 12_100m,
    cancellationToken: ct);
```

### 5.6 Cálculo de retención (RG 830)

```csharp
var resultado = afip.IncomeTaxCalculator.Calculate(new IncomeTaxWithholdingRequest(
    Regime: (int)IncomeTaxRegime.ProfessionalsAndTrades,
    PaymentDate: DateOnly.FromDateTime(DateTime.Today),
    CurrentPaymentAmount: 250_000m,
    AccumulatedMonthlyPayments: 0m,
    PreviouslyWithheld: 0m,
    IsRegistered: true));

if (resultado.Applies)
    Console.WriteLine($"Retener: ${resultado.WithholdingAmount:N2}");
```

---

## 6. Implementación multi-tenant en detalle

### 6.1 Contrato que el consumidor debe implementar

```csharp
// Namespace: Afip.Arca.Sdk.MultiTenancy
public interface ITenantOptionsProvider
{
    // Retorna null si el tenant no existe o está inactivo.
    Task<TenantAfipOptions?> GetAsync(string tenantId, CancellationToken cancellationToken);
}

public sealed record TenantAfipOptions
{
    public required string TenantId { get; init; }
    public required string Cuit { get; init; }
    public AfipEnvironment Environment { get; init; } = AfipEnvironment.Homologation;
    public required byte[] CertificateBytes { get; init; }  // PFX ya desencriptado
    public required string CertificatePassword { get; init; }
}
```

El consumidor decide cómo almacena y desencripta el certificado. El SDK no sabe nada
de bases de datos ni de esquemas de cifrado.

### 6.2 API de la fábrica

```csharp
public interface IAfipClientFactory
{
    // Lazy: crea el cliente en el primer acceso y lo cachea.
    // Thread-safe: double-checked locking con SemaphoreSlim.
    Task<IAfipClient> GetClientAsync(string tenantId, CancellationToken cancellationToken = default);

    // Elimina el cliente cacheado. El próximo GetClientAsync recarga desde el provider.
    // Llamar después de actualizar un certificado.
    void InvalidateClient(string tenantId);
}
```

### 6.3 Cómo funciona internamente `DynamicAfipClientFactory`

Para cada tenant, la fábrica crea un **DI child container** aislado:

```
Root container (IHttpClientFactory, ILoggerFactory)
       │
       ├─ Tenant "empresa1" child container
       │    ├─ AfipOptions { Cuit: "20111111111", cert1.pfx }
       │    ├─ IAccessTicketCache (caché de TA propia)
       │    ├─ IAccessTicketProvider (firma con cert1.pfx)
       │    ├─ WsfeSoapClient → usa IHttpClientFactory del root
       │    └─ IAfipClient ← lo que se devuelve al caller
       │
       └─ Tenant "empresa2" child container
            ├─ AfipOptions { Cuit: "20222222222", cert2.pfx }
            ├─ IAccessTicketCache (caché de TA propia, independiente)
            ├─ IAccessTicketProvider (firma con cert2.pfx)
            ├─ WsfeSoapClient → usa IHttpClientFactory del root
            └─ IAfipClient
```

Lo que se **comparte** del root: `IHttpClientFactory` (con Polly configurado una sola vez)
e `ILoggerFactory` (todos los tenants loguean al mismo sink).

Lo que es **por tenant**: `AfipOptions`, `IAccessTicketCache`, `IAccessTicketProvider`
(el signer tiene el certificado cargado), todos los SOAP clients.

El child container se crea una sola vez y queda cacheado en un `ConcurrentDictionary`.
`InvalidateClient` lo elimina y disposa.

### 6.4 Flujo de vida de un nuevo tenant

```
1. Admin carga el .pfx del nuevo contribuyente desde la UI
2. La aplicación cifra el .pfx (ej: AES-256-GCM) y guarda en BD
3. La app sigue corriendo — no hay restart
4. Primera factura del nuevo tenant:
       factory.GetClientAsync("nuevo-tenant")
         → provider.GetAsync("nuevo-tenant")    ← consulta BD, desencripta
         → BuildEntry(opts)                     ← crea child container
         → cache["nuevo-tenant"] = entry
         → retorna IAfipClient
5. Llamadas siguientes del mismo tenant: hit en caché, sin I/O
6. Si se renueva el certificado: InvalidateClient("nuevo-tenant")
       → next GetClientAsync recarga config y crea nuevo child container
```

### 6.5 Implementación de referencia (demo incluida en el repo)

El proyecto `implementation/Afip.Arca.Sdk.Demo` incluye una implementación de
referencia completa con:

- **`DbTenantOptionsProvider`** — lee de SQLite (EF Core), desencripta con AES-256-GCM
- **`AesCertificateEncryption`** — AES-256-GCM, nonce aleatorio por operación
- **`TenantOnboardingService`** — registra, actualiza y desactiva tenants
- **Demo interactivo** — menú modo `[2] Multi-tenant` con listar / registrar / health
  check / emitir factura por tenant

```csharp
// Registro en la aplicación de referencia
services.AddDbContextFactory<AfipDemoDbContext>(opts =>
    opts.UseSqlite("Data Source=afip_tenants.db"));

services.AddSingleton(new AesCertificateEncryption(encryptionKey)); // clave de 32 bytes
services.AddAfipClientFactory<DbTenantOptionsProvider>();           // del SDK
services.AddSingleton<DbTenantOptionsProvider>();
services.AddSingleton<TenantOnboardingService>();
```

**Esquema de la tabla `tenant_afip_configs`:**

| Columna | Tipo | Descripción |
|---|---|---|
| `TenantId` | TEXT (PK) | ID opaco (ej: userId, CUIT, slug) |
| `DisplayName` | TEXT | Nombre para mostrar |
| `Cuit` | TEXT(11) | CUIT del contribuyente |
| `UseHomologation` | BOOL | true = homologación, false = producción |
| `CertificateEncrypted` | BLOB | Bytes del .pfx cifrados con AES-256-GCM |
| `CertificateNonce` | BLOB | Nonce de 12 bytes para el cert |
| `CertificateTag` | BLOB | Tag de 16 bytes para el cert |
| `PasswordEncrypted` | BLOB | Contraseña del .pfx cifrada |
| `PasswordNonce` | BLOB | Nonce para la contraseña |
| `PasswordTag` | BLOB | Tag para la contraseña |
| `CreatedAt` | DATETIME | UTC |
| `UpdatedAt` | DATETIME | UTC — actualizado en cada cambio |
| `IsActive` | BOOL | false = tenant desactivado, factory lanza TenantNotFoundException |

---

## 7. Manejo de errores

### Jerarquía de excepciones

```
AfipException
├── AfipAuthenticationException  → WSAA no pudo obtener el TA
├── AfipTransportException       → SOAP fault o error HTTP (incluye FaultCode original)
├── AfipValidationException      → validación previa falló (antes de llamar a AFIP)
├── AfipBusinessException        → error lógico irrecuperable (raro)
└── TenantNotFoundException      → tenant no configurado o inactivo (multi-tenant)
```

### Errores de negocio de AFIP (no son excepciones)

AFIP devuelve errores dentro del payload XML con HTTP 200. El SDK los mapea a propiedades
del result object:

```csharp
var result = await afip.Invoicing.AuthorizeAsync(invoice, ct: ct);

if (!result.IsSuccess)
{
    // Errores bloqueantes (impidieron la autorización)
    foreach (var error in result.Errors)
        Console.WriteLine($"[{error.Code}] {error.Message}");
}
else
{
    // Observaciones no bloqueantes (la autorización fue exitosa de todas formas)
    foreach (var obs in result.Observations)
        Console.WriteLine($"Obs [{obs.Code}]: {obs.Message}");

    Console.WriteLine($"CAE: {result.Cae}  Vence: {result.CaeExpiration}");
}
```

---

## 8. Seguridad

| Aspecto | Implementación |
|---|---|
| **Certificados** | Se aceptan como `.pfx`/`byte[]`/`X509Certificate2`. Nunca se loguean ni serializan |
| **Contraseñas** | Solo viven en memoria durante la carga del cert. No se almacenan en texto plano |
| **Tokens AFIP** | `Token` y `Sign` se loguean como `[REDACTED]` |
| **TLS** | Mínimo TLS 1.2 forzado en `HttpClient` |
| **Estado global** | Ningún campo `static` mutable. La caché de TA vive en una instancia inyectada |
| **Certificados en BD** | En la implementación de referencia: AES-256-GCM con nonce aleatorio por operación. La clave maestra viene de variable de entorno (`AFIP_DEMO_ENCRYPTION_KEY`) |
| **Multi-tenant isolation** | Cada tenant tiene su propio `IAccessTicketCache` y `Pkcs7TraSigner` — un tenant no puede acceder al TA ni al certificado de otro |

---

## 9. Autenticación WSAA — cómo funciona internamente

1. **`TraDocumentBuilder.Build(service)`** — construye el XML del TRA con `generationTime`,
   `expirationTime` y `uniqueId` aleatorio. El `expirationTime` se fija a `now + TraValidityMinutes`
   (default 10 min).

2. **`Pkcs7TraSigner.Sign(traXml)`** — firma el XML con `System.Security.Cryptography.Pkcs.SignedCms`
   usando SHA-256 y el certificado X.509 del contribuyente. Produce un blob PKCS#7 en Base64.

3. **`WsaaSoapClient.LoginCmsAsync(service, cuit, cms, ct)`** — envía el CMS a
   `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` (homologación) o equivalente
   en producción. Parsea el XML de respuesta y extrae `token`, `sign`, `expirationTime`.

4. **`InMemoryAccessTicketCache.Set(ticket)`** — almacena el TA indexado por `(cuit, service)`.
   `TryGet` devuelve `null` si faltan menos de `TicketRefreshLeewayMinutes` minutos (default 5)
   para la expiración.

5. **`SemaphoreSlim` gate** — si dos threads piden un TA para el mismo `(cuit, service)`
   simultáneamente, solo uno llama a WSAA; el otro espera y reutiliza el resultado (double-check).

---

## 10. Testing

```
tests/Afip.Arca.Sdk.Tests/
├── Authentication/
│   ├── InMemoryAccessTicketCacheTests.cs   ← caché + leeway
│   └── TraDocumentBuilderTests.cs          ← XML del TRA
├── IncomeTax/
│   └── IncomeTaxCalculatorTests.cs         ← escala, mínimos, acumulación
├── Invoicing/
│   ├── InvoiceBuilderTests.cs              ← casos válidos e inválidos
│   └── InvoiceValidatorTests.cs
└── Support/
    └── FakeClock.cs                        ← clock determinístico
```

- **25 tests unitarios — 25/25 pasando** (sin tocar red)
- Los SOAP clients se testean con fixtures XML reales capturados de AFIP homologación
- Tests de integración: `[Trait("Category", "Integration")]`, excluidos del pipeline por default

**Estado validado contra AFIP real (mayo 2026 — homologación):**

| Operación | CAE obtenido | Estado |
|---|---|---|
| `loginCms` (WSAA) | — | ✅ |
| `FEDummy` | — | ✅ |
| `FECompUltimoAutorizado` | — | ✅ |
| `FECAESolicitar` — Factura B Consumidor Final | 86200173262441 | ✅ |
| `FECAESolicitar` — Nota de Crédito B | 86200173263879 | ✅ |

---

## 11. Versionado y publicación

- **SemVer 2.0.0** estricto
- `Directory.Build.props` centraliza versión, autores, licencia (MIT), repo URL
- Artefactos: `D:\Code\projects\artifacts\Afip.Arca.Sdk.{version}.nupkg`
- El demo (`implementation/`) consume el NuGet desde feed local, no `ProjectReference`

### Historial de versiones

| Versión | Fecha | Cambios clave |
|---|---|---|
| **1.0.0** | 2026-04-30 | Versión inicial: WSAA + WSFEv1 + RG 830 + SIRE |
| **1.0.1** | 2026-05-10 | Fix: `SOAPAction: ""` rechazado por `HttpSoapInvoker` |
| **1.0.2** | 2026-05-15 | Fix: `CondicionIVAReceptorId` (RG 5616/2024) obligatorio |
| **1.1.0** | 2026-06-03 | **Multi-tenancy:** `IAfipClientFactory` + `ITenantOptionsProvider` + `DynamicAfipClientFactory` |

---

## 12. Pendientes y roadmap

| Prioridad | Funcionalidad | Notas |
|---|---|---|
| P1 | Validar SIRE contra AFIP real | Código implementado desde spec, no probado end-to-end |
| P2 | `FECompConsultar` | Consultar detalle de comprobante ya autorizado |
| P2 | `FEParamGet*` | Catálogos: tipos de comprobante, IVA, monedas (caché 24 h) |
| P3 | Multi-comprobante en un `FECAESolicitar` | Hasta 250 por request |
| P3 | WSFEXv1 | Facturación de exportaciones |
| P3 | Redis cache para `IAccessTicketCache` | Multi-proceso / multi-instancia |
| P3 | CI/CD | GitHub Actions: test → pack → publish a NuGet.org |

---

## 13. Decisiones de diseño relevantes

### Por qué SOAP manual y no code-gen del WSDL

Los WSDL de AFIP generan clases con nombres inconsistentes, orden de campos no
garantizado y namespaces que cambian entre versiones. El SOAP manual con `XDocument`
da control total sobre el wire format exacto, es legible y debuggeable.

### Por qué `IHttpClientFactory` compartida en multi-tenant

El `HttpClient` con Polly es stateless. Crear uno por tenant sería desperdicio de
recursos (sockets, threads del timer de Polly). Un único named client configurado en
el root container es correcto y eficiente.

### Por qué child containers y no `IOptionsSnapshot`

`IOptionsSnapshot` requiere scopes por request (ASP.NET). En aplicaciones de consola,
workers o servicios de fondo sin ciclo de request, el child container por tenant es
más explícito, más fácil de razonar y permite disposing limpio del certificado.

### Por qué los errores de AFIP no son excepciones

AFIP devuelve HTTP 200 con `Errors` y `Observations` en el XML. Mapearlos a
excepciones obligaría al caller a usar try/catch para flujo normal. El Result Object
(`InvoiceAuthorizationResult`) hace el caso de error explícito en el tipo de retorno.

### Por qué `IClock` inyectable

Permite tests determinísticos sin mock de `DateTime`. `FakeClock` avanza el tiempo
manualmente → los tests de caché de TA son 100% determinísticos.

---

## 14. Cómo integrar en una aplicación real

### Mínimo requerido por tenant

1. Un certificado X.509 `.pfx` generado en WSASS (homologación) o en el Administrador
   de Certificados Digitales de AFIP (producción), con clave privada exportable.
2. El CUIT del contribuyente habilitado para el Web Service que se quiere usar.
3. Al menos un punto de venta dado de alta en AFIP para WSFEv1.

### Checklist de seguridad

- [ ] La clave de cifrado de certificados viene de variable de entorno / Key Vault
- [ ] Los archivos `.pfx` no están en el repositorio (`.gitignore` los excluye)
- [ ] `AfipOptions.Environment` = `Production` solo después de validar en homologación
- [ ] Los logs están configurados para no emitir `Trace` en producción (evita dump de SOAP)
- [ ] `AFIP_DEMO_ENCRYPTION_KEY` es una clave de 32 bytes aleatoria en producción

---

*Documento generado el 2026-06-03 — corresponde al estado del SDK en v1.1.0.*
