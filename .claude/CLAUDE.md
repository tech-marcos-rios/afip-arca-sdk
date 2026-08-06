# Lineamientos del Proyecto — Afip.Arca.Sdk

> **OBLIGATORIO.** Este archivo define los principios, patrones y prácticas que **deben respetarse en todo el código, documentación y commit** del repositorio.
> El documento de fundamentación se encuentra en [`docs/claude-configuration.md`](../docs/claude-configuration.md) y la arquitectura detallada en [`docs/architecture.md`](../docs/architecture.md).

---

## 1. Identidad del proyecto

- **Producto:** librería .NET (NuGet) que encapsula la integración con los Web Services de **AFIP/ARCA** (Argentina).
- **Alcance funcional inicial:**
  1. Facturación electrónica (WSFEv1) — emisión, consulta, anulación vía Nota de Crédito.
  2. Retenciones del Impuesto a las Ganancias — cálculo local (RG 830/2000) + informe a AFIP (SIRE).
- **Target frameworks:** multi-target `net8.0` + `netstandard2.0`.
- **Idioma:** identificadores, comentarios XMLDoc y excepciones en inglés; documentación funcional en español rioplatense.

---

## 2. Principios no negociables

Estos principios tienen precedencia sobre cualquier otra consideración estilística.

1. **Clean Architecture / Hexagonal.** El dominio (modelos de factura, retención, ticket) **no depende** de SOAP, HTTP ni infraestructura. La infraestructura depende del dominio (Dependency Rule).
2. **SOLID.** En particular:
   - **SRP:** cada clase resuelve una sola responsabilidad. Los services no parsean XML, los parsers no llaman HTTP, los HTTP clients no firman certificados.
   - **DIP:** todo lo que es reemplazable se expone como interfaz (`IAccessTicketProvider`, `IInvoiceService`, `IIncomeTaxCalculator`, `ISireService`, `IClock`).
3. **Inmutabilidad por defecto.** Los DTOs públicos son `record` o tipos con `init`-only setters. Si un objeto es mutable, debe justificarse en XMLDoc.
4. **Nullable Reference Types ON.** `<Nullable>enable</Nullable>` en todos los proyectos. Nada de `!` (null-forgiving) sin justificación documentada.
5. **Async hasta el final.** Toda llamada I/O retorna `Task` / `ValueTask` y acepta `CancellationToken` como **último parámetro**. Prohibido `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
6. **Excepciones tipadas.** Jerarquía `AfipException` → `AfipAuthenticationException`, `AfipBusinessException`, `AfipTransportException`, `AfipValidationException`. Nada de `throw new Exception(...)`.
7. **Sin estado global.** Nada de `static` mutable. La caché de TA vive en una instancia inyectada (`IAccessTicketCache`).
8. **Determinismo y testabilidad.** `DateTime.Now`/`DateTime.UtcNow` no se usa directamente: se inyecta `IClock`. Lo mismo aplica a `Guid.NewGuid()` cuando el valor importa.

---

## 3. Patrones de diseño aplicados (obligatorios donde corresponda)

| Patrón | Dónde se aplica | Por qué |
|---|---|---|
| **Strategy** | `IAccessTicketProvider` (WSAA local vs. TA externo) | Permite intercambiar el origen del Ticket de Acceso sin tocar consumidores. |
| **Adapter** | Wrappers SOAP sobre los servicios AFIP | Aísla la fea superficie SOAP del dominio limpio. |
| **Facade** | `AfipClient` | Punto de entrada único que agrupa los servicios para uso simple. |
| **Options pattern** | `AfipOptions`, `AfipOptionsValidator` | Configuración tipada e idiomática para `IServiceCollection`. |
| **Result-style returns** | `InvoiceAuthorizationResult` con `IsSuccess`, `Observations`, `Errors` | AFIP devuelve errores **dentro** del payload, no solo por status HTTP. |
| **Repository / Cache** | `IAccessTicketCache` (`InMemoryAccessTicketCache`, extensible a Redis/Disk) | Un TA dura 12 hs; cachearlo es mandatorio. |
| **Builder** | `InvoiceBuilder` | La factura tiene >20 campos; el builder fuerza estados válidos. |
| **Specification / Validator** | `IInvoiceValidator` con `FluentValidation` o validación interna | Falla *antes* de pegarle a AFIP. |

---

## 4. Estilo de código

- **Naming:** `PascalCase` para tipos y métodos públicos, `camelCase` para locales y parámetros, `_camelCase` para campos privados, `IPascalCase` para interfaces.
- **Archivos:** una clase / interfaz / enum / record por archivo, nombre del archivo = nombre del tipo.
- **Carpetas == namespaces.** `Afip.Arca.Sdk.Invoicing.Models` vive en `src/Afip.Arca.Sdk/Invoicing/Models/`.
- **XML documentation obligatoria** en todos los tipos `public`. Falla la compilación si falta (`<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `1591` como warning-as-error).
- **`var` solo cuando el tipo es obvio del RHS.** Para `record`, factory methods, LINQ → `var`. Para primitivos y retornos opacos → tipo explícito.
- **`using` declarations** (sin llaves) cuando aplique.
- **No `#region`.** Si un archivo necesita regiones, está haciendo demasiado.
- **No comentarios obvios.** Comentar el *por qué*, no el *qué*. El nombre cuenta el qué.

---

## 5. Manejo de errores y resiliencia

- **HTTP transport:** todos los servicios SOAP se invocan a través de un `HttpClient` configurado por `IHttpClientFactory` con políticas Polly:
  - Retry exponencial (3 intentos) en errores `5xx` y `HttpRequestException`.
  - Circuit breaker en cascada.
  - Timeout total ≤ 30 s.
- **AFIP business errors:** AFIP devuelve `Errors` y `Observations` con código y mensaje. Estos **no** son excepciones — son parte del resultado (`InvoiceAuthorizationResult`).
- **AFIP transport / SOAP fault:** se mapean a `AfipTransportException` con el `FaultCode` original.
- **Validación de entrada:** falla con `AfipValidationException` *antes* de la llamada de red.

---

## 6. Logging y observabilidad

- Uso de `Microsoft.Extensions.Logging.Abstractions` con `ILogger<T>` inyectado.
- **Niveles:**
  - `Trace` → cuerpos SOAP completos (solo cuando habilitado).
  - `Debug` → operación + parámetros (sin datos sensibles).
  - `Information` → eventos relevantes (TA renovado, factura autorizada).
  - `Warning` → reintentos, observaciones de AFIP.
  - `Error` → excepciones.
- **PII / datos sensibles:** `Token`, `Sign`, `CUIT`, contenido de certificados → **nunca** se loguean. Se enmascaran con `[REDACTED]`.
- Cada operación pública emite un `ActivitySource` (OpenTelemetry-compatible) con `afip.service`, `afip.environment`, `afip.cuit_hash` (hash, no CUIT crudo).

---

## 7. Seguridad

- **Certificados X.509:** se aceptan como ruta a `.pfx`/`.p12` con contraseña, o como `byte[]`, o como `X509Certificate2` ya cargado. **Nunca** se serializan, persisten ni loguean.
- **In-memory secrets:** `SecureString` no es opción razonable en .NET moderno; se documenta el riesgo y se recomienda Key Vault / DPAPI.
- **TLS:** mínimo TLS 1.2 forzado en `HttpClient`. No se aceptan certificados auto-firmados en producción.
- **Sin dependencias innecesarias.** Cada paquete NuGet sumado a la librería se revisa por superficie de ataque y licencia.

---

## 8. Testing

- Framework: **xUnit** + **FluentAssertions** + **NSubstitute** (mocks).
- **Coverage objetivo:** ≥ 80% en `Afip.Arca.Sdk` (dominio + servicios). Las clases adapter SOAP se testean con fixtures XML (`Tests/Fixtures/*.xml`).
- **No tocar la red en tests unitarios.** Las llamadas SOAP se mockean a nivel `HttpMessageHandler`.
- **Tests de integración** (carpeta `tests/Afip.Arca.Sdk.IntegrationTests`) corren contra **homologación** únicamente y están marcados con `[Trait("Category", "Integration")]` para excluirlos del pipeline por defecto.
- **Naming de tests:** `Method_Scenario_ExpectedResult` (ej. `SolicitarCae_WhenCuitIsInvalid_ThrowsValidationException`).

---

## 9. Versionado y entrega

- **SemVer 2.0.0** estricto. Cambios breaking → major. Nuevas features compatibles → minor. Fixes → patch.
- `Directory.Build.props` centraliza versión, autores, repo URL, license.
- Cada release **debe** incluir entry en `CHANGELOG.md` (Keep a Changelog).
- Package metadata mínima: `PackageId`, `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, `RepositoryType`, `PackageTags`, `PackageReadmeFile`.

---

## 10. Convenciones de commit

- **Conventional Commits:** `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, `ci:`.
- Scope opcional: `feat(invoicing): add credit note support`.
- Mensaje en imperativo, presente, en inglés. Body opcional en español si la motivación lo amerita.

---

## 11. Flujo de trabajo de Claude en este repo

Cuando se le pida modificar este código:

1. **Leer primero** este archivo y los `docs/` antes de proponer cambios.
2. **Respetar la arquitectura por capas.** No crear dependencias hacia afuera del Dependency Rule.
3. **Toda nueva clase pública** debe traer XMLDoc completa, tests asociados y entrada en el `CHANGELOG.md`.
4. **Si la solicitud rompe un principio** de esta guía, plantear el conflicto al usuario *antes* de implementar — no hacer excepciones silenciosas.
5. **Sin abreviaturas oscuras.** `cae`, `cuit`, `tra`, `ta`, `cms` se permiten porque son dominio; cualquier otra sigla se evita.
6. **PowerShell-first** en comandos de shell (este repo vive en Windows). Los scripts portables van en `.ps1`.

## 12. Convenciones del repo

- **Artefactos** (`.nupkg`, `.snupkg`) se publican en `C:\GLB\artifacts` vía `<PackageOutputPath>` en [`Directory.Build.props`](../Directory.Build.props). Convención compartida entre repos del usuario; no cambiar sin acuerdo explícito.
- **Carpeta [`implementation/`](../implementation/)** contiene una demo de consumo del NuGet que vive **fuera** de la solución principal y consume el paquete desde el feed local — no usa `ProjectReference`. Si cambia la superficie pública del SDK, actualizar también esta demo en el mismo PR (es un smoke-test del paquete).
- **Solución principal:** [`Afip.Arca.Sdk.sln`](../Afip.Arca.Sdk.sln) (librería + tests). La demo tiene su propio sln en `implementation/`.

---

> Este documento es contrato. Si una sección queda desactualizada respecto del código, se actualiza el documento en el mismo PR — no se ignora.
