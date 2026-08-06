# Roadmap y guía para retomar el desarrollo

> Documento de **handoff**: pensado para que cualquier desarrollador (humano o IA) pueda retomar el trabajo donde quedó la última sesión sin necesidad de excavar el repo.
> Actualizado: **2026-05-15** — versión actual del SDK: **`1.0.2`**.

---

## 1. Snapshot del estado actual

### Validado en producción contra AFIP homologación

```
CUIT de prueba: 20261234921
Certificado:    afipsdkpoc.pfx (generado vía WSASS, autorizado a wsfe + sire-ws)
```

| Operación | Endpoint | Estado |
|---|---|---|
| WSAA `loginCms` | wsaahomo.afip.gov.ar | ✅ validado |
| WSFEv1 `FEDummy` | wswhomo.afip.gov.ar | ✅ validado |
| WSFEv1 `FECompUltimoAutorizado` | wswhomo.afip.gov.ar | ✅ validado |
| WSFEv1 `FECAESolicitar` (FacturaB → CF) | wswhomo.afip.gov.ar | ✅ validado — CAE 86200173262441 |
| WSFEv1 `FECAESolicitar` (NotaCreditoB con CbtesAsoc) | wswhomo.afip.gov.ar | ✅ validado — CAE 86200173263879 |

### Implementado pero **no validado** contra AFIP

| Operación | Riesgo |
|---|---|
| WSFEv1 `FECAESolicitar` (Factura A a CUIT inscripto) | Bajo — solo cambia el `CbteTipo` y el `ReceiverVatCondition`; mismo camino de código. |
| WSFEv1 `FECAESolicitar` (Factura C, Monotributo) | Bajo — idem. |
| WSFEv1 `FECAESolicitar` (servicio con período + fecha vto pago) | Bajo — agrega 3 elementos XML opcionales. |
| WSFEv1 `FECAESolicitar` (multi-moneda con `MonCotiz`) | Medio — habría que probar contra `FEParamGetCotizacion`. |
| SIRE `emitir` / `consultar` / `anular` | **Alto** — el wire format se codificó desde spec pero nunca se vio una respuesta real; muy probable que haya ajustes. |
| WSFEv1 `FECompConsultar` | N/A — no implementado todavía. |
| WSFEv1 `FEParamGet*` | N/A — no implementados todavía. |

### Tests automatizados

```
25/25 pasando (xUnit + FluentAssertions + NSubstitute)
Cobertura: cálculo RG 830, validator, builder, caché de TA, TRA builder.
```

Los tests **no tocan red** — los SOAP clients están testeados a mano contra AFIP en homologación.

---

## 2. Pendientes priorizados

### 🥇 P0 — Multi-tenancy (v1.1.0) — ✅ Implementado

**Caso de uso:** sistema multi-usuario donde N contribuyentes con CUITs distintos emiten cada uno sus comprobantes.

**Estado:** implementado y documentado en [`CHANGELOG.md`](../CHANGELOG.md#110--2026-06-03). La implementación real terminó usando un enfoque distinto al sketch original de abajo (que se deja como referencia histórica de la exploración inicial): en vez de named options estáticas (`AddAfipSdkForTenant(name, configure)`), se resolvió con `ITenantOptionsProvider` (el consumidor lee las opciones del tenant desde su propia DB en runtime) + `DynamicAfipClientFactory` (`IAfipClientFactory.GetClientAsync(tenantId, ct)`, async, con un DI child container por tenant creado lazily). Ver `src/Afip.Arca.Sdk/MultiTenancy/` para la API real.

#### Sketch original (superado por la implementación real de arriba)

##### Approach: Named Options + `IAfipClientFactory`

```csharp
// API objetivo:
services.AddAfipSdkForTenant("tenant-a", opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20111111111";
    opts.UseLocalCertificateSigning(c => c.FromFile("vault/a.pfx", pwdA));
});
services.AddAfipSdkForTenant("tenant-b", opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20222222222";
    opts.UseLocalCertificateSigning(c => c.FromFile("vault/b.pfx", pwdB));
});

// Uso:
public class BillingController(IAfipClientFactory factory)
{
    public async Task<string> Emit(string tenantId, ...)
    {
        var afip = factory.GetClient(tenantId);
        var result = await afip.Invoicing.AuthorizeAsync(invoice);
        return result.Cae!;
    }
}
```

#### Pasos sugeridos de implementación

1. **Agregar named options en `AfipOptions`** — Microsoft.Extensions.Options ya lo soporta (`IOptionsMonitor<T>.Get(name)`). El único cambio es que el ticket provider y los SOAP clients lean opciones por nombre, no las default.
2. **Refactor del SOAP client base** — `WsaaSoapClient`, `WsfeSoapClient`, `SireSoapClient` hoy leen `_options.CurrentValue.Cuit`. Cambiar a recibir un `string tenantName` que use `_options.Get(tenantName).Cuit`.
3. **Refactor del cache** — ya está keyed por `(CUIT, service)`, así que **no necesita cambios**. ✅
4. **Refactor del ticket provider** — `WsaaAccessTicketProvider` hoy es singleton sin tenant. Necesita un `IAccessTicketProvider Get(string tenant)` o similar.
5. **Agregar `IAfipClientFactory`** — fábrica que devuelve un `IAfipClient` configurado para un nombre.
6. **Agregar `AddAfipSdkForTenant(name, configure)`** — extension method que registra named options para ese tenant.
7. **Backward compatibility** — `AddAfipSdk(...)` actual sigue funcionando como `AddAfipSdkForTenant(Options.DefaultName, ...)`.
8. **Tests** — al menos uno con 2 tenants simulados y verificación de que sus caches no se mezclan.
9. **Actualizar `implementation/`** — agregar un demo multi-tenant para mostrar el patrón.
10. **Actualizar docs** — `architecture.md` (nuevo ADR), `README.md`, `CHANGELOG.md` (entrada 1.1.0).

**Esfuerzo estimado:** 4–6 horas para un dev con familiaridad .NET DI. ~150 líneas de cambio neto.

---

### 🥈 P1 — Validar el módulo SIRE contra AFIP real

**Problema:** el `SireSoapClient` se escribió desde la spec oficial pero **nunca se ejecutó contra AFIP**. Casi seguro hay ajustes que descubrir (namespaces, ordenes de campos, formatos de fecha).

**Pasos:**

1. Verificar que el cert `afipsdkpoc.pfx` esté autorizado al servicio `sire-ws` en WSASS (en la última sesión esto se hizo, confirmar).
2. Usar la demo `implementation/Afip.Arca.Sdk.Demo`, opción **6 (Emitir certificado SIRE)** con datos de prueba.
3. Documentar el error que devuelva (probablemente formato XML).
4. Ajustar `SireSoapClient.cs`:
   - `Sire` namespace puede no ser `http://sire.afip.gob.ar/` — verificar.
   - `Endpoint.Sire` apunta a `fwshomo.afip.gov.ar/sire-ws/services/SireSoap` — confirmar.
   - El response parsing es genérico (busca por LocalName); puede romperse si AFIP usa namespaces distintos.
5. Una vez validado: `Operación SIRE` → ✅ en la tabla de [§1](#1-snapshot-del-estado-actual).
6. Bump a `1.0.3`, republicar, actualizar `CHANGELOG.md`.

---

### 🥉 P2 — Completar superficie de WSFEv1

#### `FECompConsultar`

Permite consultar el detalle de un comprobante ya autorizado. Útil para reconciliación o cuando hay dudas si una emisión llegó o no.

```csharp
public interface IInvoiceService
{
    // existente:
    Task<InvoiceAuthorizationResult> AuthorizeAsync(...);
    Task<long> GetLastAuthorizedNumberAsync(...);

    // a agregar:
    Task<InvoiceLookupResult?> GetAsync(InvoiceReference reference, CancellationToken ct);
}
```

`InvoiceLookupResult` debe incluir el CAE, fecha, importes, observaciones y datos del comprobante.

#### `FEParamGet*` con caché

Las tablas paramétricas (tipos de comprobante, alícuotas, monedas, etc.) cambian rara vez. Cachearlas 24h ahorra latencia en cada call.

API objetivo:

```csharp
public interface IInvoiceParameters
{
    Task<IReadOnlyList<InvoiceTypeInfo>> GetInvoiceTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<DocumentTypeInfo>> GetDocumentTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<VatRateInfo>> GetVatRatesAsync(CancellationToken ct);
    Task<IReadOnlyList<CurrencyInfo>> GetCurrenciesAsync(CancellationToken ct);
    Task<decimal> GetCurrencyQuotationAsync(string currencyCode, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<ReceiverVatConditionInfo>> GetReceiverVatConditionsAsync(CancellationToken ct);
    Task<IReadOnlyList<PointOfSaleInfo>> GetPointsOfSaleAsync(CancellationToken ct);
}
```

Caché: `IMemoryCache` con TTL 24h. Permitir override del TTL en `AfipOptions`.

---

### P3 — Otros nice-to-haves

- **Soporte multi-comprobante en `FECAESolicitar`** — la API permite hasta 250 comprobantes por request; hoy mandamos 1.
- **Soporte de tributos personalizados** (`Tributos`) — útil para retenciones provinciales/municipales.
- **WSFEXv1** (facturación de exportación) — nuevo módulo paralelo a `Invoicing`, con su propia carpeta `Exporting/` siguiendo el mismo patrón hexagonal.
- **CHANGELOG automatizado** vía un commit hook que valide que cada PR no-trivial tenga entrada.
- **CI/CD** — pipeline GitHub Actions que corra tests, packee y publique a `nuget.org` en cada tag SemVer.

---

## 3. Limitaciones conocidas a tener en cuenta

| Limitación | Detalle |
|---|---|
| **Single-tenant** | Una sola configuración (`AfipOptions.Cuit`) por proceso. Solución: P0 multi-tenancy. |
| **`InvoiceService.CancelAsync` simple** | La NC que genera pone todo el importe en `NonTaxableAmount`. Para FacturaB suele andar; para FacturaA puede fallar porque AFIP exige mirroring del breakdown de IVA original. Workaround: usar `AuthorizeAsync` con una NC construida a mano. |
| **`BuiltInIncomeTaxScaleProvider` con datos de 2024-10** | Cuando AFIP actualice la RG 830, hay que regenerar la tabla. Mejor: registrar un `IIncomeTaxScaleProvider` propio que lea de DB. |
| **SIRE wire format no validado** | Ver P1. |
| **No hay retry inteligente para `coe.alreadyAuthenticated`** | Si dos procesos comparten cache y piden TA simultáneamente, uno puede recibir este error. Solución actual: el provider tiene un `SemaphoreSlim` interno que serializa el acceso. Para multi-proceso real, usar `IAccessTicketCache` distribuido (Redis). |

---

## 4. Cómo retomar (quick start para el próximo dev/IA)

### Paso 1 — Familiarizate con el estado

Leé en este orden:

1. **[README.md](../README.md)** — overview rápido.
2. **[CHANGELOG.md](../CHANGELOG.md)** — lo que se hizo cuando.
3. **Este documento** — qué está pendiente y por qué.
4. **[docs/certificate-setup.md](certificate-setup.md)** — cómo obtener el cert (la parte que más se traba).
5. **[docs/architecture.md](architecture.md)** — las decisiones de diseño.
6. **[.claude/CLAUDE.md](../.claude/CLAUDE.md)** — los lineamientos obligatorios del repo.

### Paso 2 — Verificá que todo siga andando

```powershell
cd D:\Code\projects\03-afip-net

# Build limpio + tests:
dotnet test Afip.Arca.Sdk.sln

# Repack para confirmar que el nupkg sale:
dotnet pack src/Afip.Arca.Sdk/Afip.Arca.Sdk.csproj -c Release

# Demo interactiva contra AFIP real:
cd implementation
dotnet restore --force-evaluate
dotnet run --project Afip.Arca.Sdk.Demo
```

Si querés correr la demo, necesitás el `.pfx` real. Hay uno generado en la sesión anterior en `scripts/certs/afipsdkpoc.pfx` (password `test-poc`) para CUIT `20261234921`. **Solo sirve para homologación.**

### Paso 3 — Elegí un pendiente y atacalo

Si tu objetivo es feature next: arrancá por **P0 (Multi-tenancy)** — es el cambio más alto-impacto y la API ya está pensada.

Si tu objetivo es completar lo que falta: **P1 (SIRE)** es el pendiente "completar lo prometido".

Si tu objetivo es robustez: **P2 (`FECompConsultar` + `FEParamGet*`)** completa la superficie de WSFEv1 hasta el 95%.

### Paso 4 — Convenciones obligatorias antes de modificar código

- [`.claude/CLAUDE.md`](../.claude/CLAUDE.md) es **contrato**. Cualquier cambio que rompa Clean Architecture / SOLID / async / nullable / excepciones tipadas debe **plantearse antes**, no aplicarse silenciosamente.
- Toda nueva clase pública requiere XMLDoc + tests + entrada en `CHANGELOG.md` bajo `[Unreleased]`.
- Commits en formato Conventional Commits (`feat:`, `fix:`, `docs:`...).
- Si el cambio rompe la API: bump major (2.0.0). Si solo agrega: minor. Si es fix: patch.

### Paso 5 — Cambios sin commitear de la última sesión

A la fecha de este documento, hay cambios sin committear. El árbol contiene:

- Versión bumpeada a 1.0.2 (Directory.Build.props).
- Nuevos archivos: `src/Afip.Arca.Sdk/Invoicing/Models/ReceiverVatCondition.cs`, `CHANGELOG.md`, `docs/roadmap.md`, `docs/certificate-setup.md`.
- Modificaciones: `HttpSoapInvoker.cs`, `Invoice.cs`, `InvoiceBuilder.cs`, `WsfeSoapClient.cs`, `InvoicingDemo.cs`, `SetupWizard.cs`, `New-AfipCertificate.ps1`, varios READMEs.
- Artefactos en `D:\Code\projects\artifacts` (carpeta renombrada en agosto 2026, antes `C:\GLB\artifacts`): `Afip.Arca.Sdk.1.0.1.nupkg`, `1.0.2.nupkg`.

Antes de empezar nuevo trabajo: **decidir si committear estos cambios como tags `v1.0.1` + `v1.0.2`, o squashearlos**. Mi recomendación: commits separados con mensajes Conventional Commits (un commit por cada fix relevante), respetando la trazabilidad de los descubrimientos.

---

## 5. Referencias rápidas

- **Endpoints AFIP** — [docs/afip-api-technical-summary.md §2.1 y §3.1](afip-api-technical-summary.md)
- **Errores AFIP frecuentes** — [docs/afip-api-technical-summary.md §3.8](afip-api-technical-summary.md)
- **Cómo conseguir el cert** — [docs/certificate-setup.md](certificate-setup.md)
- **Arquitectura del SDK** — [docs/architecture.md](architecture.md)
- **Demo interactiva** — [implementation/README.md](../implementation/README.md)
- **Script PowerShell de cert** — [scripts/README.md](../scripts/README.md)
- **Lineamientos del repo** — [.claude/CLAUDE.md](../.claude/CLAUDE.md)
