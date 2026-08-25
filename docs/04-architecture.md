# Arquitectura — Afip.Arca.Sdk

> Documento de arquitectura de la librería NuGet. Audiencia: desarrolladores que consumirán o extenderán el SDK. Complementa [`03-afip-api-technical-summary.md`](03-afip-api-technical-summary.md) (qué es AFIP) explicando cómo se modela en este código.

---

## 1. Visión

`Afip.Arca.Sdk` encapsula la integración con los Web Services de AFIP/ARCA, ocultando la complejidad SOAP/CMS/X.509 detrás de una superficie idiomática .NET. Las metas explícitas son:

1. **Productividad:** que armar una factura electrónica sea ~10 líneas de código de aplicación.
2. **Confiabilidad:** caching de tickets, retries, validación previa, distinción correcta entre errores de transporte y errores de negocio.
3. **Testeable:** todo lo externo (HTTP, reloj, caché, ticket provider) está detrás de una interfaz.
4. **Multi-target:** una sola DLL `net8.0` (modernidad) + `netstandard2.0` (compatibilidad con .NET Framework 4.7.2+).

---

## 2. Topología de capas (Clean Architecture)

```
┌────────────────────────────────────────────────────────────────┐
│                     CONSUMER (app del usuario)                 │
└──────────────────────────────┬─────────────────────────────────┘
                               │ usa
                               ▼
┌────────────────────────────────────────────────────────────────┐
│  FACADE                                                        │
│    AfipClient — agrupa Authentication / Invoicing / IncomeTax  │
└──────────────────────────────┬─────────────────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
        ┌──────────┐    ┌───────────┐    ┌────────────┐
        │ Services │    │ Calculator│    │   Validators│
        │  (IInvoice│    │   (RG830) │    │   (FluentVal│
        │  Service) │    │           │    │   patterns) │
        └─────┬────┘    └─────┬─────┘    └────────────┘
              │               │
              ▼               ▼
        ┌────────────────────────────┐
        │ DOMAIN                     │
        │  Invoice, InvoiceItem,     │
        │  Tax, AccessTicket,        │
        │  Result/Error types        │
        └──────────────┬─────────────┘
                       │
                       ▼ (depende de)
        ┌────────────────────────────┐
        │ ABSTRACTIONS               │
        │  IAccessTicketProvider,    │
        │  IAccessTicketCache,       │
        │  IClock, IHttpSoapInvoker  │
        └──────────────┬─────────────┘
                       │ implementan
                       ▼
        ┌────────────────────────────┐
        │ INFRASTRUCTURE             │
        │  WsaaAccessTicketProvider, │
        │  HttpSoapInvoker,          │
        │  Cms / Soap envelopes,     │
        │  HttpClient + Polly        │
        └────────────────────────────┘
```

**Dependency Rule:** las flechas de dependencia apuntan **hacia adentro**. `Domain` no conoce SOAP. `Infrastructure` implementa las abstracciones que `Domain` define.

**Multi-tenancy (capa paralela):** cuando la app sirve N CUITs, `IAfipClientFactory` (namespace `MultiTenancy/`) envuelve N instancias completas de este mismo stack — una por tenant, cada una con su propio `AfipOptions`/`IAccessTicketCache`/`IAccessTicketProvider` en un contenedor de DI hijo aislado. Ver ADR-011 y [`05-sdk-implementation-overview.md`](05-sdk-implementation-overview.md#6-implementación-multi-tenant-en-detalle) para el detalle completo.

---

## 3. Estructura de carpetas

> Nota: el repositorio incluye además una carpeta [`implementation/`](../implementation/) con una **solución de consola separada** que consume el NuGet desde `D:\Code\projects\artifacts` vía `NuGet.config` (no usa `ProjectReference`). Sirve como prueba end-to-end del paquete y como referencia de uso. Toda la arquitectura descrita abajo refiere exclusivamente a la librería `src/Afip.Arca.Sdk/`.

```
src/Afip.Arca.Sdk/
├── Afip.Arca.Sdk.csproj
├── AfipClient.cs                       // Facade público
├── IAfipClient.cs
│
├── Configuration/
│   ├── AfipEndpoints.cs                // URLs por ambiente (Homologation/Production)
│   ├── AfipEnvironment.cs              // Enum: Homologation, Production
│   ├── AfipOptions.cs                  // Options pattern root
│   ├── AfipOptionsValidator.cs
│   └── ServiceCollectionExtensions.cs  // AddAfipSdk(...) / AddAfipClientFactory<T>(...)
│
├── Authentication/                     // WSAA
│   ├── AccessTicket.cs                 // Inmutable record
│   ├── IAccessTicketProvider.cs
│   ├── IInvalidatableAccessTicketProvider.cs  // Capability opcional — invalidación de TA
│   ├── WsaaAccessTicketProvider.cs     // Strategy: firma local
│   ├── ExternalAccessTicketProvider.cs // Strategy: TA preexistente
│   ├── IAccessTicketCache.cs
│   ├── InMemoryAccessTicketCache.cs
│   ├── Cms/
│   │   ├── ITraSigner.cs
│   │   ├── Pkcs7TraSigner.cs           // SignedCms (SHA-256)
│   │   └── TraDocumentBuilder.cs       // XML del TRA
│   └── Soap/
│       └── WsaaSoapClient.cs           // loginCms()
│
├── Invoicing/                          // WSFEv1
│   ├── IInvoiceService.cs
│   ├── InvoiceService.cs
│   ├── InvoiceBuilder.cs
│   ├── Models/
│   │   ├── Invoice.cs
│   │   ├── InvoiceType.cs              // Enum (FacturaA, NotaCreditoB, etc.)
│   │   ├── DocumentType.cs             // Enum (Cuit=80, Dni=96, ...)
│   │   ├── Concept.cs                  // Enum (Products=1, Services=2, Mixed=3)
│   │   ├── VatRate.cs                  // Enum (TwentyOne=5, TenAndHalf=4, ...)
│   │   ├── VatLine.cs                  // Rate + base + amount
│   │   ├── ReceiverVatCondition.cs     // Enum (RG 5616/2024 — CondicionIVAReceptorId)
│   │   ├── Currency.cs                 // ArgentinePeso, UsDollar, Euro
│   │   ├── InvoiceReference.cs         // PtoVta + Number + Type
│   │   ├── InvoiceAuthorizationResult.cs
│   │   ├── InvoiceObservation.cs
│   │   └── InvoiceError.cs
│   ├── Validation/
│   │   └── InvoiceValidator.cs
│   └── Soap/
│       └── WsfeSoapClient.cs           // FECAESolicitar, FECompUltimoAutorizado, FEDummy
│
├── IncomeTax/                          // Ganancias
│   ├── Calculation/
│   │   ├── IIncomeTaxCalculator.cs
│   │   ├── IncomeTaxCalculator.cs      // Implementa RG 830
│   │   ├── IIncomeTaxScaleProvider.cs
│   │   ├── BuiltInIncomeTaxScaleProvider.cs   // RG 5423 (oct 2024)
│   │   └── Models/
│   │       ├── IncomeTaxRegime.cs
│   │       ├── IncomeTaxScale.cs
│   │       ├── IncomeTaxScaleBracket.cs
│   │       ├── IncomeTaxWithholdingRequest.cs
│   │       └── IncomeTaxWithholdingResult.cs
│   └── Reporting/                      // SIRE
│       ├── ISireService.cs
│       ├── SireService.cs
│       ├── Models/
│       │   ├── SubjectCondition.cs
│       │   ├── TaxCode.cs
│       │   ├── WithholdingCertificateRequest.cs
│       │   └── WithholdingCertificateResult.cs
│       └── Soap/
│           └── SireSoapClient.cs
│
├── MultiTenancy/                       // Ver ADR-011
│   ├── IAfipClientFactory.cs
│   ├── DynamicAfipClientFactory.cs
│   ├── ITenantOptionsProvider.cs       // Interfaz que el consumidor implementa
│   ├── TenantAfipOptions.cs
│   └── TenantNotFoundException.cs
│
└── Common/
    ├── Exceptions/
    │   ├── AfipException.cs                  // Base
    │   ├── AfipAuthenticationException.cs    // WSAA
    │   ├── AfipBusinessException.cs          // Errores de negocio AFIP
    │   ├── AfipTransportException.cs         // SOAP fault / HTTP
    │   └── AfipValidationException.cs        // Pre-llamada
    ├── Time/
    │   ├── IClock.cs
    │   └── SystemClock.cs
    └── Soap/
        ├── IHttpSoapInvoker.cs
        ├── HttpSoapInvoker.cs
        └── SoapFault.cs
```

---

## 4. Decisiones arquitectónicas (ADR-style)

### ADR-001 — Multi-target (`net8.0` + `netstandard2.0`)

**Contexto:** apps Argentinas todavía corren mucho .NET Framework 4.x para sistemas legados de facturación.

**Decisión:** Multi-target.

**Consecuencias:**
- ✅ Funciona en .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5/6/7/8.
- ⚠️ Hay que evitar APIs `netstandard2.0`-incompatibles (records pre-C# 9 no aplican porque la versión de C# es por proyecto; sí se pueden usar `record` con polyfill `IsExternalInit`).
- ⚠️ `HttpClient` con políticas Polly nativas → `netstandard2.0` requiere `Microsoft.Extensions.Http.Polly` v2.x compatible.

### ADR-002 — Strategy para `IAccessTicketProvider`

**Contexto:** algunos consumidores ya tienen su propio mecanismo de firma (HSM, Key Vault firmador remoto). Otros quieren que la librería se encargue de todo.

**Decisión:** dos implementaciones explícitas:

- `WsaaAccessTicketProvider` — toma un `X509Certificate2` y un CUIT, firma el TRA y llama a `loginCms`.
- `ExternalAccessTicketProvider` — recibe un `Func<service, CancellationToken, Task<AccessTicket>>` y delega al consumidor.

Ambas implementan `IAccessTicketProvider` y se eligen en la registración:

```csharp
services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(cert => cert.FromFile("...", "password"));
    // o:
    opts.UseExternalTicketProvider(myProvider);
});
```

**Consecuencias:** el dominio no sabe quién firma. Cambiar de uno a otro es una línea.

Ambas implementaciones además implementan `IInvalidatableAccessTicketProvider` (interfaz
opcional, ver ADR-003) — un consumidor con un `IAccessTicketProvider` propio (ej. HSM sin
caché local) puede no implementarla sin romper nada; simplemente no participa del retry
automático descripto abajo.

### ADR-003 — Caché de TA: `IAccessTicketCache` e invalidación ante token inválido

**Contexto:** un TA dura 12 hs; pedir uno nuevo por cada llamada es lento, caro, y termina chocando con `coe.alreadyAuthenticated`.

**Decisión:** abstracción `IAccessTicketCache` con `TryGet(cuit, service)` / `Set(ticket)` / `Invalidate(cuit, service)`. Implementación por defecto en memoria (`InMemoryAccessTicketCache` con `ConcurrentDictionary` + reloj inyectado). Implementaciones alternativas (Redis, disco) quedan como ejercicio del consumidor — la abstracción está lista.

`Invalidate` no se llama directamente desde los servicios (`InvoiceService`, `SireService`) — eso acoplaría la orquestación de negocio al detalle de caché. En su lugar, `WsaaAccessTicketProvider` y `ExternalAccessTicketProvider` exponen `IInvalidatableAccessTicketProvider.Invalidate(service)`, que resuelve el CUIT actual y delega en la caché. `InvoiceService` detecta el error WSFEv1 `1000` ("Token inválido o vencido") — devuelto por `FECAESolicitar` dentro del resultado, o lanzado como `AfipBusinessException` por `FECompUltimoAutorizado` — hace un `is IInvalidatableAccessTicketProvider` sobre su `IAccessTicketProvider` inyectado y, si aplica, invalida y reintenta la operación una única vez con un TA fresco.

**Consecuencias:** ahorro masivo de llamadas WSAA en cargas reales. Test trivial mockeando la caché. El retry ante token inválido es opt-in por capability-check (Interface Segregation): no rompe `IAccessTicketProvider` para implementaciones custom que no cachean nada localmente.

### ADR-004 — Errores de negocio NO son excepciones

**Contexto:** AFIP devuelve errores y observaciones **en el cuerpo** de la respuesta SOAP exitosa. Tratarlos como excepciones obliga a try/catch por flujos normales y oscurece el control de flujo.

**Decisión:** las operaciones de negocio devuelven un `Result`-style con:

```csharp
public sealed record InvoiceAuthorizationResult(
    bool IsSuccess,
    string? Cae,
    DateOnly? CaeExpiration,
    long? AssignedNumber,
    int PointOfSale,
    InvoiceType Type,
    IReadOnlyList<InvoiceObservation> Observations,
    IReadOnlyList<InvoiceError> Errors);
```

Las excepciones quedan reservadas para:
- **Authentication failures** → `AfipAuthenticationException`.
- **Transport / SOAP fault** → `AfipTransportException`.
- **Validación pre-llamada** → `AfipValidationException`.
- **Errores irrecuperables del SDK** → `AfipException` directa.

### ADR-005 — `Microsoft.Extensions.*` como integración nativa

**Contexto:** las apps modernas usan `IServiceCollection`, `IConfiguration`, `ILogger`, `IHttpClientFactory`. Una librería NuGet que no se integra ahí es vintage.

**Decisión:** dependencias core:
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Http.Polly`

**Sin DI:** hoy no hay un constructor manual soportado (`AfipClient` solo se construye con `IInvoiceService`/`IIncomeTaxCalculator`/`ISireService` ya resueltos). Armar el stack completo a mano (HttpClient con Polly, ticket provider, SOAP clients) fuera de `IServiceCollection` no está documentado ni es el camino recomendado.

### ADR-006 — Sin code-gen de WSDL (clientes SOAP a mano)

**Contexto:** `dotnet-svcutil` genera clientes SOAP, pero el output es voluminoso, opaco, y se rompe ante cambios menores del WSDL de AFIP.

**Decisión:** construir los envelopes SOAP a mano con `XDocument`. El total de operaciones de AFIP que tocamos es ~15; el código manual es legible, depurable y nos da control total sobre la serialización (incluido el orden de elementos, que algunos servicios validan).

**Consecuencias:**
- ✅ Diff-friendly cuando AFIP cambia algo.
- ✅ Sin dependencia adicional a `System.ServiceModel.*`.
- ⚠️ Más superficie de tests — se cubre mockeando `IHttpSoapInvoker` con NSubstitute y devolviendo `XElement` construidos inline en C# (no hay fixtures XML como archivos separados; ver ADR de testing en la sección 6).

### ADR-007 — Validación previa con builder + validator

**Contexto:** AFIP rechaza por arrays de razones (importes que no cierran, CbteFch fuera de rango, CUIT mal formado). Pegarle a la API solo para enterarnos es lento y consume cuota.

**Decisión:** `InvoiceBuilder` garantiza estados sintácticamente válidos; `InvoiceValidator` corre antes del request y falla con `AfipValidationException` si hay problemas semánticos. La validación es cero costo de red.

### ADR-008 — `IClock` para tiempo

**Contexto:** TRAs tienen `generationTime`/`expirationTime`. Tests no deben depender del reloj real.

**Decisión:** `IClock` con `SystemClock` por defecto. En tests, `FakeClock` (provisto por el proyecto de tests).

### ADR-009 — Logging clásico con `ILogger<T>`

**Contexto:** el SDK loguea eventos poco frecuentes (renovación de TA, autorización de comprobante, reintentos) — no hot-path de alta frecuencia.

**Decisión:** `ILogger<T>.LogInformation(...)`/`LogWarning(...)` clásicos con plantillas de mensaje estructuradas (ej. `"Authorizing comprobante type {Type} pos {Pos} number {Number}"`), sin el source generator `[LoggerMessage]`. Se evaluó y se descartó: el overhead de alocación es irrelevante a la frecuencia real de estos logs, y el atributo agrega ceremonia (clase parcial + método estático) sin beneficio medible acá.

### ADR-010 — `ReceiverVatCondition` como enum modelado, no opcional (RG 5616/2024)

**Contexto:** AFIP introdujo el campo `CondicionIVAReceptorId` como **obligatorio** desde RG 5616/2024. Sin él, AFIP rechaza el request con observación 10246.

**Decisión:** modelar el campo como un enum `ReceiverVatCondition` (12 valores per spec AFIP) con un default razonable en el modelo `Invoice` (`ConsumerFinal = 5`). El `InvoiceBuilder` infiere el valor a partir del tipo de receptor (`ToCuit()` → `RegisteredVat`, `ToDni()`/`ToConsumerFinal()` → `ConsumerFinal`); `WithReceiverVatCondition(...)` permite override.

**Consecuencias:**
- ✅ El consumer no tiene que conocer la tabla de códigos AFIP para casos simples.
- ✅ Para casos no triviales (Monotributo, Exento, etc.) la API es explícita.
- ⚠️ Si AFIP suma valores nuevos, hay que extender el enum (bump minor).

**Descubierto en producción:** durante el desarrollo, AFIP rechazó la primera factura de prueba emitida con observación 10246 hasta que se agregó este campo — quedó incluido desde el release inicial (1.0.0).

### ADR-011 — Multi-tenancy vía contenedores de DI hijos (`MultiTenancy/`)

**Contexto:** algunos consumidores sirven N contribuyentes (CUITs y certificados distintos) desde el mismo proceso — típicamente un SaaS de facturación — y ninguna configuración/caché/certificado puede filtrarse entre tenants.

**Decisión:** `IAfipClientFactory` (implementación: `DynamicAfipClientFactory`) resuelve un `ITenantOptionsProvider` (implementado por el consumidor, lee de su propia DB/secretos) y arma, lazily por tenant, un `ServiceCollection` nuevo con su propio `AfipOptions`/`IAccessTicketCache`/`IAccessTicketProvider`/SOAP clients — un `ServiceProvider` completamente aislado por CUIT. Solo `IHttpClientFactory` e `ILoggerFactory` se comparten desde el contenedor raíz (no tienen secretos). `InvalidateClient(tenantId)` disposea el contenedor de un tenant puntual (ej. tras rotar su certificado).

**Consecuencias:**
- ✅ Aislamiento estructural, no por convención: no existe ningún campo mutable compartido con el CUIT/certificado "actual" — cada tenant es un grafo de objetos separado en memoria.
- ✅ Agregar tenants nuevos no requiere reiniciar el proceso.
- ⚠️ Un `SemaphoreSlim` con double-checked locking evita construir el mismo contenedor dos veces bajo carga concurrente.

Detalle completo (trace paso a paso, diagrama del árbol de contenedores) en [`05-sdk-implementation-overview.md`](05-sdk-implementation-overview.md#6-implementación-multi-tenant-en-detalle).

---

## 5. Flujo de uso típico (consumer-side)

```csharp
// 1. Registración (Program.cs / Startup.cs)
services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(c =>
    {
        c.FromFile(@"C:\certs\contribuyente.pfx", "secret");
    });
});

// 2. Uso (en un service de aplicación)
public sealed class BillingService
{
    private readonly IAfipClient _afip;
    public BillingService(IAfipClient afip) => _afip = afip;

    public async Task<string> EmitInvoiceAsync(CancellationToken ct)
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()                    // doc type 99, doc nro 0
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(net: 1000m, rate: VatRate.TwentyOne)
            .Build();

        var result = await _afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);

        return result.IsSuccess
            ? result.Cae!
            : throw new InvalidOperationException($"AFIP rechazó: {string.Join("; ", result.Errors)}");
    }
}
```

---

## 6. Estrategia de testing

| Tipo | Carpeta | Qué se testea |
|---|---|---|
| Unit | `tests/Afip.Arca.Sdk.Tests/` | Calculator, builders, validators, caché de TA, TRA builder, y el retry ante token inválido de `InvoiceService`. Sin red — los SOAP clients se testean mockeando `IHttpSoapInvoker` con NSubstitute y devolviendo `XElement` de respuesta construidos inline en C#. |

No hay actualmente un proyecto de tests de integración separado ni fixtures XML como archivos; las respuestas SOAP de AFIP se validan manualmente contra homologación real durante el desarrollo (ver estado en el [README](../README.md#estado-del-proyecto)).

---

## 7. Performance budget

| Operación | Latencia objetivo (p95) | Notas |
|---|---|---|
| `Invoice` → builder + validate | < 1 ms | Sin red. |
| WSAA `loginCms` | < 800 ms | Solo cuando hay miss de caché. |
| WSFEv1 `FECAESolicitar` | < 1.5 s | Determinado por AFIP, no por nosotros. |
| Cálculo RG 830 | < 0.1 ms | Aritmética pura. |

Nada en el SDK aloca colecciones en hot path innecesariamente. Los DTOs públicos son `record` (tipo referencia, inmutables por `init`); no se usa `record struct` en el código actual.

---

## 8. Roadmap (fuera del alcance v1, pero el diseño lo contempla)

- WSFEXv1 (exportación) — encaja como nuevo módulo `Exporting/` con el mismo patrón.
- WS Aduana / SIM — nuevo módulo, mismo facade.
- Caché Redis del TA — implementación alternativa de `IAccessTicketCache`.
- ARCA REST APIs (cuando AFIP las publique con cobertura) — nuevo módulo paralelo a `*.Soap`.

El **patrón** que cualquier extensión debe seguir es: nuevo módulo en `src/Afip.Arca.Sdk/<Area>/` con su propio `IXxxService` + implementación + modelos + carpeta `Soap/` (o `Rest/`) interna.
