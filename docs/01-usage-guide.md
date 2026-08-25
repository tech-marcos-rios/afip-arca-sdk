# Guía de consumo — `Afip.Arca.Sdk`

> Este documento explica **cómo usar el paquete NuGet** desde tu aplicación: instalación,
> configuración, los tres servicios disponibles (Facturación, Retenciones, SIRE), manejo
> de errores y comportamiento del caché de autenticación.
>
> Para arquitectura interna, ADRs y decisiones de diseño: [04-architecture.md](04-architecture.md).
> Para conseguir el certificado necesario: [02-certificate-setup.md](02-certificate-setup.md).
> Para el estado del proyecto y pendientes: ver la sección "Estado del proyecto" en el [README](../README.md).

---

## 1. Instalación

```bash
dotnet add package Afip.Arca.Sdk
```

Targets soportados: `net8.0` y `netstandard2.0` (compatible con .NET Framework 4.7.2+ y .NET Core 3.1+).

---

## 2. Elegí tu camino

El SDK soporta dos formas de configurarse, y **son mutuamente excluyentes** — elegís una al
registrar el SDK en `Program.cs` y esa decisión determina cómo obtenés el `IAfipClient` en el
resto de tu aplicación.

| | 🅰️ Camino A — Single-tenant | 🅱️ Camino B — Multi-tenant |
|---|---|---|
| **¿Cuándo?** | Tu app factura para **un solo CUIT** propio. | Tu app factura para **varios CUITs distintos** (ej. un SaaS donde cada cliente tiene su propio CUIT y certificado). |
| **Registro** | `services.AddAfipSdk(opts => ...)` | `services.AddAfipClientFactory<TProvider>()` |
| **Cómo obtenés `IAfipClient`** | Inyectado directo por DI: `IAfipClient afip` en el constructor. | Pedido explícitamente por tenant: `await factory.GetClientAsync(tenantId, ct)`. |
| **Quién guarda CUIT/certificado** | Vos, en config (`appsettings.json`, variables de entorno). | Vos, en tu propia base de datos — le das al SDK una interfaz para leerlos on-demand. |
| **Ir a** | [Camino A](#3-camino-a--single-tenant) | [Camino B](#4-camino-b--multi-tenant) |

Si no estás seguro: si tu app entera opera bajo un único CUIT (tu propia empresa, por ejemplo),
es Camino A. Si distintos usuarios/clientes de tu app tienen CUITs propios que no deben mezclarse
entre sí, es Camino B.

Una vez que termines tu camino vas a tener, en la mano, un `IAfipClient afip` — a partir de ahí
**todo el resto de esta guía (§5 en adelante) es idéntico para los dos caminos.**

---

## 3. Camino A — Single-tenant

Un solo CUIT por proceso.

```csharp
using Afip.Arca.Sdk.Configuration;

builder.Services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation; // o .Production
    opts.Cuit = "20123456789";

    opts.UseLocalCertificateSigning(c =>
        c.FromFile(@"C:\certs\contribuyente.pfx", password: "secret"));
});
```

Esto registra `IAfipClient` (y también `IInvoiceService`, `IIncomeTaxCalculator`, `ISireService`
por separado, por si preferís inyectar solo lo que usás) en el contenedor DI.

```csharp
public sealed class BillingService(IAfipClient afip) // ← lo inyectás directo
{
    // usar afip.Invoicing / afip.IncomeTaxCalculator / afip.Sire — ver §5
}
```

### 3.1 Opciones de `AfipOptions`

| Propiedad | Default | Qué controla |
|---|---|---|
| `Environment` | `Homologation` | Selecciona el set de endpoints (homologación vs producción). **Nunca cambiar a `Production` sin certificado real y aprobación explícita.** |
| `Cuit` | `""` (requerido) | CUIT de 11 dígitos del contribuyente que hace las llamadas. |
| `Endpoints` | `null` → usa `AfipEndpoints.DefaultsFor(Environment)` | Override manual de URLs (Wsaa/Wsfev1/Sire), por si AFIP relocaliza un servicio. |
| `TicketRefreshLeewayMinutes` | `5` | Minutos antes del vencimiento real del **TA** (Ticket de Acceso — la credencial que emite WSAA tras autenticarse, dura 12 hs) en que se lo considera "stale" y se pide uno nuevo. |
| `TraValidityMinutes` | `10` | Ventana de validez que el SDK le pide a WSAA para el **TRA** (Ticket de Requerimiento de Acceso — el pedido de autenticación que el SDK firma y envía, dura minutos). No confundir con el TA: el TRA es la *solicitud*, el TA es la *credencial* que WSAA devuelve como respuesta. |

### 3.2 Estrategia de autenticación — elegí una

| Modo | Cuándo usarlo | Cómo |
|---|---|---|
| **Firma local con certificado X.509** (default) | El `.pfx`/`.p12` vive en el mismo proceso o en disco accesible. | `opts.UseLocalCertificateSigning(c => c.FromFile(path, password))`, o `c.FromBytes(bytes, password)`, o `c.FromCertificate(x509Cert2)` si ya tenés el certificado cargado (ej. desde Key Vault). |
| **Provider externo** | La firma vive en un HSM, Key Vault o un servicio remoto — el SDK nunca ve la clave privada. | `opts.UseExternalTicketProvider(async (service, ct) => await miServicioDeFirma.ObtenerTaAsync(service, ct))`. Tu callback debe devolver un `AccessTicket` ya emitido; el SDK solo lo cachea y lo reusa. |

Si no configurás ninguno de los dos, `AfipOptionsValidator` (registrado automáticamente vía
`IValidateOptions<AfipOptions>`) falla en el primer `IOptions<AfipOptions>.Value` con un mensaje
claro en vez de fallar silenciosamente al primer llamado a AFIP.

**Ya tenés tu `IAfipClient afip`.** Continuá en [§5 — Usar el SDK](#5-usar-el-sdk-aplica-a-los-dos-caminos).

---

## 4. Camino B — Multi-tenant

Cuando tu aplicación sirve **N contribuyentes** (CUITs y certificados distintos) desde el mismo
proceso, y **no se pueden mezclar entre sí** — cada tenant tiene su propio `AfipOptions`, su
propio caché de TA y su propio certificado cargado en memoria, en un contenedor de DI aislado por
tenant. El detalle de cómo se garantiza ese aislamiento está en
[04-architecture.md](04-architecture.md) (ADR de multi-tenancy) y en
[05-sdk-implementation-overview.md](05-sdk-implementation-overview.md#6-implementación-multi-tenant-en-detalle).

Registrás la fábrica en vez del SDK fijo:

```csharp
builder.Services.AddAfipClientFactory<MyTenantOptionsProvider>();
```

Vos implementás `ITenantOptionsProvider`, que el SDK llama para resolver la config de cada tenant
(la primera vez, o después de invalidarlo manualmente — nunca la persiste):

```csharp
public sealed class MyTenantOptionsProvider : ITenantOptionsProvider
{
    public async Task<TenantAfipOptions?> GetAsync(string tenantId, CancellationToken ct)
    {
        var row = await _db.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (row is null || !row.IsActive) return null; // null = tenant no encontrado/inactivo

        return new TenantAfipOptions
        {
            TenantId = tenantId,
            Cuit = row.Cuit,
            Environment = AfipEnvironment.Homologation,
            CertificateBytes = _crypto.Decrypt(row.EncryptedCertificate), // desencriptar es tu responsabilidad
            CertificatePassword = _crypto.Decrypt(row.EncryptedPassword),
        };
    }
}
```

Y en vez de inyectar `IAfipClient` directo, inyectás `IAfipClientFactory` y pedís el cliente del
tenant que corresponda a cada operación:

```csharp
public sealed class BillingController(IAfipClientFactory factory) // ← inyectás la FÁBRICA, no el cliente
{
    public async Task<string?> EmitAsync(string tenantId, CancellationToken ct)
    {
        var afip = await factory.GetClientAsync(tenantId, ct); // ← acá conseguís el IAfipClient de ESE tenant
        // usar afip.Invoicing / afip.IncomeTaxCalculator / afip.Sire — ver §5, es igual que en Camino A
        var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);
        return result.Cae;
    }

    public void OnCertificateRotated(string tenantId) =>
        factory.InvalidateClient(tenantId); // fuerza recarga desde ITenantOptionsProvider en el próximo GetClientAsync
}
```

`GetClientAsync` puede lanzar `TenantNotFoundException` si tu `ITenantOptionsProvider` devolvió
`null` para ese `tenantId`.

El cliente por tenant se crea **lazy** (solo al primer `GetClientAsync`) y se cachea en memoria —
agregar tenants nuevos no requiere reiniciar el proceso. Cada tenant tiene su propio caché de TA;
uno no puede ver el TA ni el certificado de otro.

**Ya tenés tu `IAfipClient afip` (para el tenant que pediste).** Continuá en
[§5 — Usar el SDK](#5-usar-el-sdk-aplica-a-los-dos-caminos) — la única diferencia con Camino A fue
cómo llegaste hasta acá.

---

## 5. Usar el SDK (aplica a los dos caminos)

> De acá en adelante, en todos los ejemplos, `afip` es tu `IAfipClient` — no importa si lo
> conseguiste con el Camino A (inyectado directo) o el Camino B
> (`await factory.GetClientAsync(tenantId, ct)`). La superficie es idéntica.

### 5.1 `IAfipClient` — el facade

```csharp
public interface IAfipClient
{
    IInvoiceService Invoicing { get; }             // WSFEv1
    IIncomeTaxCalculator IncomeTaxCalculator { get; } // RG 830, cálculo offline
    ISireService Sire { get; }                      // SIRE
}
```

En Camino A también podés inyectar `IInvoiceService`, `IIncomeTaxCalculator` o `ISireService`
directamente si solo necesitás uno — están registrados igual. En Camino B no, porque esos
servicios son por-tenant: siempre pasás por `factory.GetClientAsync(tenantId)` primero.

### 5.2 Facturación electrónica (WSFEv1)

#### Armar un comprobante con `InvoiceBuilder`

El builder fuerza una secuencia válida; la validación semántica completa la hace
`InvoiceValidator` automáticamente dentro de `AuthorizeAsync` (podés interceptar fallos con
`AfipValidationException`, ver §6).

**Factura B a consumidor final:**

```csharp
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;

var invoice = InvoiceBuilder
    .ForType(InvoiceType.FacturaB)
    .AtPointOfSale(1)
    .ToConsumerFinal()
    .WithDate(DateOnly.FromDateTime(DateTime.Today))
    .WithVatBase(net: 10_000m, rate: VatRate.TwentyOne)
    .Build();
```

**Factura A a un responsable inscripto (requiere condición IVA explícita si no es el default):**

```csharp
var invoice = InvoiceBuilder
    .ForType(InvoiceType.FacturaA)
    .AtPointOfSale(1)
    .ToCuit(20987654321) // default: ReceiverVatCondition.RegisteredVat
    .WithDate(DateOnly.FromDateTime(DateTime.Today))
    .WithVatBase(net: 50_000m, rate: VatRate.TwentyOne)
    .Build();
```

**Servicio (requiere período + fecha de vencimiento de pago):**

```csharp
var invoice = InvoiceBuilder
    .ForType(InvoiceType.FacturaB)
    .AtPointOfSale(1)
    .WithConcept(Concept.Services)
    .ToConsumerFinal()
    .WithDate(today)
    .WithServicePeriod(from: inicioMes, to: finMes, paymentDue: today.AddDays(10))
    .WithVatBase(net: 15_000m, rate: VatRate.TwentyOne)
    .Build();
```

**Moneda extranjera:**

```csharp
.WithCurrency(Currency.UsDollar, quotation: 1_050.50m)
```

Otros métodos del builder: `ToDni(long)`, `ToDocument(DocumentType, long)`,
`WithReceiverVatCondition(...)` (override explícito, obligatorio para exento/monotributo/etc.),
`WithNonTaxableAmount`, `WithExemptAmount`, `WithOtherTaxes`, `WithVatBase(...)` (llamable varias
veces para múltiples alícuotas), `AssociatedTo(InvoiceReference)` (obligatorio en notas de
crédito/débito).

#### Autorizar (`FECAESolicitar`)

```csharp
var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);

if (result.IsSuccess)
{
    Console.WriteLine($"CAE: {result.Cae}, vence {result.CaeExpiration}, número {result.AssignedNumber}");
    // Guardá Cae, CaeExpiration y AssignedNumber en tu propia base de datos ACÁ.
    // AFIP no te los vuelve a dar fácil después — perder el CAE es uno de los dolores de
    // cabeza más comunes al integrar (lo necesitás para imprimir el comprobante, reclamos, etc.).
}
else
{
    // errores bloqueantes — NO es una excepción, ver §6
    foreach (var e in result.Errors) Console.WriteLine($"[{e.Code}] {e.Message}");
}

foreach (var o in result.Observations) Console.WriteLine($"Obs [{o.Code}] {o.Message}"); // no bloqueantes
```

Si no pasás `explicitNumber`, el SDK consulta `FECompUltimoAutorizado` y usa el siguiente número
automáticamente — no necesitás llevar tu propio contador.

#### Anular (vía Nota de Crédito)

AFIP no soporta anulación directa; la forma convencional es una Nota de Crédito por el total:

```csharp
var nc = await afip.Invoicing.CancelAsync(
    original: new InvoiceReference(InvoiceType.FacturaB, PointOfSale: 1, Number: 42),
    totalToCancel: 12_100m,
    cancellationToken: ct);
```

> **Limitación conocida:** la NC generada pone todo el importe en `NonTaxableAmount`. Para
> Factura B suele funcionar; para Factura A puede fallar porque AFIP exige espejar el desglose
> de IVA original. Si necesitás eso, armá la NC vos mismo con `InvoiceBuilder` y llamá
> `AuthorizeAsync` directamente.

#### Consultar el último número autorizado

```csharp
var last = await afip.Invoicing.GetLastAuthorizedNumberAsync(InvoiceType.FacturaB, pointOfSale: 1, ct);
```

#### Health check

```csharp
var (app, db, auth) = await afip.Invoicing.HealthCheckAsync(ct); // FEDummy — cada segmento es "OK" si está sano
```

### 5.3 Retenciones de Ganancias (RG 830) — cálculo offline

`IIncomeTaxCalculator.Calculate` es **puro, síncrono, sin I/O** — no llama a AFIP. Podés
ejecutarlo en cualquier contexto (batch, preview antes de pagar, etc.).

```csharp
using Afip.Arca.Sdk.IncomeTax.Calculation.Models;

var calc = afip.IncomeTaxCalculator.Calculate(new IncomeTaxWithholdingRequest(
    Regime: (int)IncomeTaxRegime.ProfessionalsAndTrades, // o un código crudo no listado en el enum
    PaymentDate: DateOnly.FromDateTime(DateTime.Today),
    CurrentPaymentAmount: 250_000m,
    AccumulatedMonthlyPayments: 0m,   // pagos previos al mismo sujeto/régimen en el mes
    PreviouslyWithheld: 0m,           // retenciones ya practicadas este mes
    IsRegistered: true));

if (calc.Applies)
{
    Console.WriteLine($"Retener: ${calc.WithholdingAmount}");
}
else
{
    Console.WriteLine($"No aplica: {calc.NotAppliedReason}"); // ej. por debajo del mínimo no imponible
}
```

> La tabla de escalas (`BuiltInIncomeTaxScaleProvider`) tiene los valores vigentes a la fecha de
> este documento (RG 5423). Cuando AFIP actualice la escala, hay que regenerarla — o registrar tu
> propio `IIncomeTaxScaleProvider` que lea de una fuente propia (DB, config remota).

### 5.4 SIRE — informar la retención

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

// también disponibles:
await afip.Sire.GetAsync(sireResult.CertificateNumber!, ct);
await afip.Sire.CancelAsync(sireResult.CertificateNumber!, ct);
```

> **⚠️ Importante:** a la fecha de este documento, el wire format de SIRE está implementado desde
> la especificación publicada por AFIP pero **todavía no fue ejercitado contra AFIP real** (ver
> [CHANGELOG.md](../CHANGELOG.md), sección "Unreleased"). Es muy probable que haya que ajustar namespaces o formato
> de campos una vez que se pruebe end-to-end. Tratalo como beta hasta que el roadmap marque este
> punto como validado.

---

## 6. Manejo de errores

El SDK distingue dos categorías, siguiendo el mismo criterio que usa AFIP. Aplica igual en los
dos caminos.

### 6.1 Errores de negocio → NO son excepciones

AFIP los devuelve **dentro** de una respuesta HTTP 200. El SDK los expone en el resultado, no
como excepción:

- `InvoiceAuthorizationResult.Errors` / `.Observations` (facturación)
- `WithholdingCertificateResult.Errors` (SIRE)

Revisá `result.IsSuccess` siempre antes de asumir éxito.

### 6.2 Errores estructurales → excepciones tipadas

| Excepción | Cuándo se lanza |
|---|---|
| `AfipException` | Clase base — capturala si querés manejar cualquier falla del SDK de forma genérica. |
| `AfipValidationException` | El SDK detectó un problema **antes** de llamar a AFIP (ej. total no cierra a la centésima). Trae `Failures` con la lista de motivos. Ahorra un roundtrip. |
| `AfipAuthenticationException` | Falla la autenticación contra WSAA (certificado inválido, TRA vencido, `Cuit` no configurado). Trae `FaultCode` opcional. |
| `AfipTransportException` | No se pudo llegar al servicio, timeout, o WSAA/WSFE devolvió un SOAP Fault (no un error de negocio). Trae `HttpStatusCode` y `SoapFaultCode` opcionales. |
| `AfipBusinessException` | Reservada para paths sin resultado que inspeccionar (ej. `GetLastAuthorizedNumberAsync` cuando AFIP rechaza la consulta). Trae `Errors` con pares `(Code, Message)`. |
| `TenantNotFoundException` | Solo Camino B — `factory.GetClientAsync(tenantId)` cuando tu `ITenantOptionsProvider` devolvió `null`. |

```csharp
try
{
    var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);
}
catch (AfipValidationException ex)
{
    // ex.Failures — corregí el Invoice antes de reintentar
}
catch (AfipAuthenticationException ex)
{
    // problema de certificado/TA — no reintentar automáticamente sin intervención
}
catch (AfipTransportException ex)
{
    // red/timeout/fault SOAP — candidato a retry con backoff en tu capa
}
```

---

## 7. Caché y renovación automática del Ticket de Acceso (TA)

No necesitás manejar el TA vos mismo — es transparente, en los dos caminos (en Camino B, cada
tenant tiene su propio caché aislado, ver §4):

- El TA se cachea en memoria, keyed por `(CUIT, service)`, durante toda su validez (12 hs por
  defecto en AFIP).
- `TicketRefreshLeewayMinutes` (default 5) determina con cuánta anticipación al vencimiento real
  se lo considera stale y se pide uno nuevo.
- **Reintento automático ante token inválido (WSFEv1):** si AFIP rechaza una llamada con el error
  `1000` ("Token inválido o vencido"), `InvoiceService` invalida el TA cacheado y reintenta la
  operación **una sola vez** con un ticket recién emitido, sin que tu código tenga que hacer nada.
  Esto cubre tanto `FECompUltimoAutorizado` como `FECAESolicitar`.
  - Esta capacidad depende de que el `IAccessTicketProvider` activo la soporte — los dos
    providers built-in (`WsaaAccessTicketProvider`, `ExternalAccessTicketProvider`) la tienen. Si
    implementaste tu propio `IAccessTicketProvider` custom, podés sumarte implementando también
    `IInvalidatableAccessTicketProvider`; si no lo hacés, el SDK simplemente no reintenta y
    propaga el error como antes.
  - **SIRE no tiene este comportamiento todavía** — ver §5.4, es beta.

Para escenarios multi-proceso (varias instancias compartiendo el mismo caché), la caché en
memoria por defecto no alcanza — implementá tu propio `IAccessTicketCache` (Redis, etc.) y
registralo antes de llamar `AddAfipSdk` / `AddAfipClientFactory`.

---

## 8. Logging

El SDK usa `ILogger<T>` (Microsoft.Extensions.Logging). Niveles:

| Nivel | Qué vas a ver |
|---|---|
| `Trace` | Cuerpos SOAP completos (solo si tu configuración de logging lo habilita — ruidoso). |
| `Debug` | Operación + parámetros (sin datos sensibles). |
| `Information` | Eventos relevantes: TA renovado, comprobante autorizado. |
| `Warning` | Reintentos (incluido el de §7), observaciones de AFIP. |
| `Error` | Excepciones. |

`Token`, `Sign`, `Cuit` y contenido de certificados **nunca** se loguean en texto plano.

---

## 9. Ambientes

| Ambiente | `AfipEnvironment` | Uso |
|---|---|---|
| Homologación | `Homologation` (default) | Sandbox de pruebas. Gratis, requiere CN vinculado en WSASS. |
| Producción | `Production` | Requiere certificado emitido por el Administrador de Certificados Digitales de AFIP. |

En Camino B, el ambiente se elige **por tenant** (`TenantAfipOptions.Environment`) — nada impide
tener algunos tenants en homologación y otros en producción simultáneamente.

Ver [02-certificate-setup.md](02-certificate-setup.md) para el paso a paso de cómo conseguir el
certificado en cualquiera de los dos ambientes.

---

## 10. Ejemplos completos end-to-end

### 10.1 Camino A — Single-tenant

```csharp
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;
using Afip.Arca.Sdk.Common.Exceptions;

builder.Services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(c => c.FromFile(certPath, certPassword));
});

// ... en tu servicio de negocio:
public sealed class BillingService(IAfipClient afip, ILogger<BillingService> logger)
{
    public async Task<string?> EmitAsync(decimal montoNeto, CancellationToken ct)
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(montoNeto, VatRate.TwentyOne)
            .Build();

        try
        {
            var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);
            if (!result.IsSuccess)
            {
                logger.LogWarning("AFIP rechazó el comprobante: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Message)));
                return null;
            }
            return result.Cae;
        }
        catch (AfipValidationException ex)
        {
            logger.LogError("Comprobante inválido antes de enviarlo: {Failures}", string.Join(", ", ex.Failures));
            return null;
        }
    }
}
```

### 10.2 Camino B — Multi-tenant

```csharp
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk;
using Afip.Arca.Sdk.MultiTenancy;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;
using Afip.Arca.Sdk.Common.Exceptions;

builder.Services.AddAfipClientFactory<MyTenantOptionsProvider>();

// ... en tu servicio de negocio, el único cambio real frente a 10.1 es
// inyectar IAfipClientFactory en vez de IAfipClient, y resolver el cliente
// del tenant correspondiente antes de usarlo:
public sealed class BillingService(IAfipClientFactory factory, ILogger<BillingService> logger)
{
    public async Task<string?> EmitAsync(string tenantId, decimal montoNeto, CancellationToken ct)
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(montoNeto, VatRate.TwentyOne)
            .Build();

        try
        {
            var afip = await factory.GetClientAsync(tenantId, ct);
            var result = await afip.Invoicing.AuthorizeAsync(invoice, cancellationToken: ct);
            if (!result.IsSuccess)
            {
                logger.LogWarning("AFIP rechazó el comprobante del tenant {TenantId}: {Errors}",
                    tenantId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return null;
            }
            return result.Cae;
        }
        catch (TenantNotFoundException)
        {
            logger.LogError("Tenant {TenantId} no encontrado o inactivo", tenantId);
            return null;
        }
        catch (AfipValidationException ex)
        {
            logger.LogError("Comprobante inválido antes de enviarlo: {Failures}", string.Join(", ", ex.Failures));
            return null;
        }
    }
}
```

---

## 11. Más allá de esta guía

| Necesitás | Mirá |
|---|---|
| Conseguir/instalar el certificado AFIP/ARCA | [02-certificate-setup.md](02-certificate-setup.md) |
| Arquitectura interna, ADRs, por qué se diseñó así | [04-architecture.md](04-architecture.md) |
| Detalle de multi-tenancy (child containers, etc.) | [05-sdk-implementation-overview.md](05-sdk-implementation-overview.md) |
| Referencia técnica de los WS de AFIP (namespaces, códigos de error) | [03-afip-api-technical-summary.md](03-afip-api-technical-summary.md) |
