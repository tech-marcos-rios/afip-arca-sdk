# Arquitectura — Afip.Arca.Sdk

> Documento de arquitectura de la librería NuGet. Audiencia: desarrolladores que consumirán o extenderán el SDK. Complementa [`afip-api-technical-summary.md`](afip-api-technical-summary.md) (qué es AFIP) explicando cómo se modela en este código.

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

---

## 3. Estructura de carpetas

> Nota: el repositorio incluye además una carpeta [`implementation/`](../implementation/) con una **solución de consola separada** que consume el NuGet desde `D:\Code\projects\artifacts` vía `NuGet.config` (no usa `ProjectReference`). Sirve como prueba end-to-end del paquete y como referencia de uso. Toda la arquitectura descrita abajo refiere exclusivamente a la librería `src/Afip.Arca.Sdk/`.

```
src/Afip.Arca.Sdk/
├── Afip.Arca.Sdk.csproj
├── AfipClient.cs                       // Facade público
│
├── Configuration/
│   ├── AfipEnvironment.cs              // Enum: Homologation, Production
│   ├── AfipOptions.cs                  // Options pattern root
│   ├── AfipOptionsValidator.cs
│   └── ServiceCollectionExtensions.cs  // AddAfipSdk(...)
│
├── Authentication/                     // WSAA
│   ├── IAccessTicketProvider.cs
│   ├── WsaaAccessTicketProvider.cs     // Strategy: firma local
│   ├── ExternalAccessTicketProvider.cs // Strategy: TA preexistente
│   ├── AccessTicket.cs                 // Inmutable record
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
│   │   ├── VatRate.cs                  // Enum (TwentyOne=5, TenFive=4, ...)
│   │   ├── VatLine.cs                  // Rate + base + amount
│   │   ├── ReceiverVatCondition.cs     // Enum (RG 5616/2024 — CondicionIVAReceptorId)
│   │   ├── Currency.cs                 // Pes, Usd, Eur, ...
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
│   │   ├── Models/
│   │   │   ├── IncomeTaxRegime.cs
│   │   │   ├── IncomeTaxScale.cs
│   │   │   ├── IncomeTaxScaleBracket.cs
│   │   │   ├── IncomeTaxWithholdingRequest.cs
│   │   │   └── IncomeTaxWithholdingResult.cs
│   │   └── PaymentAccumulator.cs       // Acumula pagos del mes
│   ├── Reporting/                      // SIRE
│   │   ├── ISireService.cs
│   │   ├── SireService.cs
│   │   ├── Models/
│   │   │   ├── WithholdingCertificate.cs
│   │   │   ├── WithholdingCertificateRequest.cs
│   │   │   └── WithholdingCertificateResult.cs
│   │   └── Soap/
│   │       └── SireSoapClient.cs
│   └── Validation/
│       └── WithholdingValidator.cs
│
├── Common/
│   ├── Exceptions/
│   │   ├── AfipException.cs                  // Base
│   │   ├── AfipAuthenticationException.cs    // WSAA
│   │   ├── AfipBusinessException.cs          // Errores de negocio AFIP
│   │   ├── AfipTransportException.cs         // SOAP fault / HTTP
│   │   └── AfipValidationException.cs        // Pre-llamada
│   ├── Time/
│   │   ├── IClock.cs
│   │   └── SystemClock.cs
│   ├── Logging/
│   │   └── LogMessages.cs                    // High-perf logging (LoggerMessage)
│   └── Soap/
│       ├── IHttpSoapInvoker.cs
│       ├── HttpSoapInvoker.cs
│       ├── SoapEnvelope.cs
│       └── SoapFault.cs
│
└── Internal/                                  // Helpers no públicos
    └── XmlExtensions.cs
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

### ADR-003 — Caché de TA: `IAccessTicketCache`

**Contexto:** un TA dura 12 hs; pedir uno nuevo por cada llamada es lento, caro, y termina chocando con `coe.alreadyAuthenticated`.

**Decisión:** abstracción `IAccessTicketCache` con `TryGet(cuit, service)` / `Set(ticket)`. Implementación por defecto en memoria (`InMemoryAccessTicketCache` con `ConcurrentDictionary` + reloj inyectado). Implementaciones alternativas (Redis, disco) quedan como ejercicio del consumidor — la abstracción está lista.

**Consecuencias:** ahorro masivo de llamadas WSAA en cargas reales. Test trivial mockeando la caché.

### ADR-004 — Errores de negocio NO son excepciones

**Contexto:** AFIP devuelve errores y observaciones **en el cuerpo** de la respuesta SOAP exitosa. Tratarlos como excepciones obliga a try/catch por flujos normales y oscurece el control de flujo.

**Decisión:** las operaciones de negocio devuelven un `Result`-style con:

```csharp
public sealed record InvoiceAuthorizationResult(
    bool IsSuccess,
    string? Cae,
    DateTime? CaeExpiration,
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

Sin DI: hay un constructor manual `AfipClient.Create(AfipOptions)` para escenarios poco frecuentes.

### ADR-006 — Sin code-gen de WSDL (clientes SOAP a mano)

**Contexto:** `dotnet-svcutil` genera clientes SOAP, pero el output es voluminoso, opaco, y se rompe ante cambios menores del WSDL de AFIP.

**Decisión:** construir los envelopes SOAP a mano con `XDocument`. El total de operaciones de AFIP que tocamos es ~15; el código manual es legible, depurable y nos da control total sobre la serialización (incluido el orden de elementos, que algunos servicios validan).

**Consecuencias:**
- ✅ Diff-friendly cuando AFIP cambia algo.
- ✅ Sin dependencia adicional a `System.ServiceModel.*`.
- ⚠️ Más superficie de tests (cubierta con fixtures XML).

### ADR-007 — Validación previa con builder + validator

**Contexto:** AFIP rechaza por arrays de razones (importes que no cierran, CbteFch fuera de rango, CUIT mal formado). Pegarle a la API solo para enterarnos es lento y consume cuota.

**Decisión:** `InvoiceBuilder` garantiza estados sintácticamente válidos; `InvoiceValidator` corre antes del request y falla con `AfipValidationException` si hay problemas semánticos. La validación es cero costo de red.

### ADR-008 — `IClock` para tiempo

**Contexto:** TRAs tienen `generationTime`/`expirationTime`. Tests no deben depender del reloj real.

**Decisión:** `IClock` con `SystemClock` por defecto. En tests, `FakeClock` (provisto por el proyecto de tests).

### ADR-009 — Logging vía `LoggerMessage` source generator

**Contexto:** logging clásico con strings interpolados es alocador y lento en hot path.

**Decisión:** todos los logs pasan por métodos generados con `[LoggerMessage]` (atributo del source generator de `Microsoft.Extensions.Logging`). Beneficios: zero allocations, mensajes consistentes, formato estructurado.

### ADR-010 — `ReceiverVatCondition` como enum modelado, no opcional (RG 5616/2024)

**Contexto:** AFIP introdujo el campo `CondicionIVAReceptorId` como **obligatorio** desde RG 5616/2024. Sin él, AFIP rechaza el request con observación 10246.

**Decisión:** modelar el campo como un enum `ReceiverVatCondition` (12 valores per spec AFIP) con un default razonable en el modelo `Invoice` (`ConsumerFinal = 5`). El `InvoiceBuilder` infiere el valor a partir del tipo de receptor (`ToCuit()` → `RegisteredVat`, `ToDni()`/`ToConsumerFinal()` → `ConsumerFinal`); `WithReceiverVatCondition(...)` permite override.

**Consecuencias:**
- ✅ El consumer no tiene que conocer la tabla de códigos AFIP para casos simples.
- ✅ Para casos no triviales (Monotributo, Exento, etc.) la API es explícita.
- ⚠️ Si AFIP suma valores nuevos, hay que extender el enum (bump minor).

**Descubierto en producción:** este campo no estaba en v1.0.0; se agregó en v1.0.2 después de que AFIP rechazara la primera factura emitida con observación 10246.

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
            .WithVatBase(amount: 1000m, rate: VatRate.TwentyOne)
            .Build();

        var result = await _afip.Invoicing.AuthorizeAsync(invoice, ct);

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
| Unit | `tests/Afip.Arca.Sdk.Tests/` | Calculator, builders, validators, parsers SOAP, caché, signers (con cert dummy). Sin red. |
| Integration | `tests/Afip.Arca.Sdk.IntegrationTests/` | Contra homologación real. Excluidos del CI por defecto (`[Trait("Category","Integration")]`). |
| Fixtures | `tests/Afip.Arca.Sdk.Tests/Fixtures/*.xml` | Respuestas SOAP capturadas para tests de parsing. |

---

## 7. Performance budget

| Operación | Latencia objetivo (p95) | Notas |
|---|---|---|
| `Invoice` → builder + validate | < 1 ms | Sin red. |
| WSAA `loginCms` | < 800 ms | Solo cuando hay miss de caché. |
| WSFEv1 `FECAESolicitar` | < 1.5 s | Determinado por AFIP, no por nosotros. |
| Cálculo RG 830 | < 0.1 ms | Aritmética pura. |

Nada en el SDK aloca colecciones en hot path innecesariamente; los DTOs son `record struct` cuando son ≤ 16 bytes.

---

## 8. Roadmap (fuera del alcance v1, pero el diseño lo contempla)

- WSFEXv1 (exportación) — encaja como nuevo módulo `Exporting/` con el mismo patrón.
- WS Aduana / SIM — nuevo módulo, mismo facade.
- Caché Redis del TA — implementación alternativa de `IAccessTicketCache`.
- ARCA REST APIs (cuando AFIP las publique con cobertura) — nuevo módulo paralelo a `*.Soap`.

El **patrón** que cualquier extensión debe seguir es: nuevo módulo en `src/Afip.Arca.Sdk/<Area>/` con su propio `IXxxService` + implementación + modelos + carpeta `Soap/` (o `Rest/`) interna.
