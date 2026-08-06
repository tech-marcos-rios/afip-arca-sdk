# Afip.Arca.Sdk — Resumen Profesional

> **Una librería .NET de calidad de producto que encapsula la integración con los servicios fiscales de AFIP/ARCA (Argentina), publicable como NuGet, con autenticación criptográfica, facturación electrónica y retenciones de ganancias.**
>
> **Versión actual: 1.0.2** — validado end-to-end contra AFIP homologación (mayo 2026).

---

## Resumen ejecutivo

Toda empresa argentina que factura sufre el mismo dolor: integrarse con AFIP es complejo, los Web Services son SOAP/CMS-PKCS#7, las reglas cambian con cada Resolución General, y los errores que devuelve la API son crípticos. Las implementaciones que se encuentran en la comunidad son scripts dispersos, frameworks en Python o PHP, o capas finitas sobre WSDLs auto-generados que se rompen al primer cambio.

`Afip.Arca.Sdk` aborda el problema desde el otro extremo: una **librería .NET de calidad de producto**, multi-target, con arquitectura limpia, testeable, integrable con `IServiceCollection`/`IHttpClientFactory`/`ILogger`, distribuida como NuGet. El consumidor deja de pensar en SOAP envelopes, CMS signatures y reglas aritméticas: piensa en *facturas* y *retenciones*.

---

## Qué resuelve, en concreto

| Dominio | Funcionalidad |
|---|---|
| **Autenticación (WSAA)** | Firma local PKCS#7 SHA-256 del TRA, llamada a `loginCms`, parseo del TA, caching transparente de 12 hs por `(CUIT, service)`. Modo alternativo: TA inyectado desde proveedor externo (HSM / Key Vault). |
| **Facturación electrónica (WSFEv1)** | Emisión de Factura A/B/C/M y sus Notas de Crédito y Débito. Builder fluido con validación pre-vuelo. Numeración automática. "Anulación" implementada como emisión de NC vinculada al original (única vía técnica que AFIP permite). Consulta de comprobantes y health-check. |
| **Retenciones de Ganancias (RG 830)** | Cálculo local determinístico con escala progresiva, acumulación mensual, mínimo no imponible, alícuota fija para no inscriptos, descuento de retenciones previas, y mínimo de aplicación. Datos de RG 5423 (vigentes 2024-10) embebidos; reemplazables vía `IIncomeTaxScaleProvider`. |
| **Reporte a SIRE** | Emisión, anulación y consulta de certificados de retención vía SOAP, integrado con la misma autenticación WSAA. |

---

## Stack y decisiones de ingeniería

| Aspecto | Decisión | Por qué |
|---|---|---|
| **Lenguaje** | C# 12+ (con polyfills para netstandard2.0) | Modern records, init-only, nullable reference types, pattern matching. |
| **Targets** | `net8.0` + `netstandard2.0` | Cubre desde apps .NET Framework 4.7.2+ hasta el último .NET. Una sola DLL, distribución limpia. |
| **Arquitectura** | Clean Architecture / Hexagonal | El dominio no depende de SOAP. La infraestructura implementa las abstracciones del dominio. |
| **Async** | 100% `Task`/`ValueTask` con `CancellationToken` | Sin `.Result`, sin `.Wait()`. |
| **Inmutabilidad** | DTOs públicos como `record` con `init` setters | Thread-safety gratis, sharing seguro entre capas. |
| **Configuración** | Options Pattern + `IValidateOptions` | Falla rápido al startup si la config está mal. |
| **HTTP** | `IHttpClientFactory` con Polly (retry exponencial + timeout) | Sin sockets fugados, sin retries ingenuos en respuestas 200-con-error-de-negocio. |
| **Caché TA** | `IAccessTicketCache` con implementación in-memory por defecto | Abstracción permite migrar a Redis sin tocar consumers. |
| **Errores** | Excepciones tipadas (`AfipAuthenticationException`, `AfipTransportException`, `AfipValidationException`) + Result-style para fallos de negocio | Los errores de AFIP vienen en el cuerpo de respuestas 200 — no son excepciones. |
| **Logging** | `Microsoft.Extensions.Logging.Abstractions` con redacción de secretos | Sin tokens, sin sign, sin CUIT crudo en logs. |
| **Time** | `IClock` inyectable, sin `DateTime.Now` directo | Tests determinísticos. |
| **Tests** | xUnit + FluentAssertions + NSubstitute, fixtures XML | Coverage sobre cálculo, validación y caché. Integration tests aislados con trait. |
| **Documentación** | XMLDoc obligatoria (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`), warnings-as-errors | El IntelliSense del consumidor es completo desde día 0. |

---

## Patrones de diseño aplicados

| Patrón | Aplicación |
|---|---|
| **Strategy** | `IAccessTicketProvider` permite intercambiar firma local con `X509Certificate2` por un proveedor externo basado en HSM/Key Vault. |
| **Adapter** | `WsfeSoapClient`, `WsaaSoapClient`, `SireSoapClient` aíslan al dominio del wire SOAP. |
| **Facade** | `AfipClient` agrupa los tres servicios para uso simple. |
| **Builder** | `InvoiceBuilder` para componer comprobantes complejos garantizando estados válidos. |
| **Specification / Validator** | `InvoiceValidator` ejecuta validación pre-vuelo, ahorrando roundtrips a AFIP por errores triviales. |
| **Repository / Cache** | `IAccessTicketCache` abstrae el almacenamiento de TA. |
| **Options Pattern** | `AfipOptions` + `AfipOptionsValidator` para configuración tipada. |
| **Result Object** | `InvoiceAuthorizationResult`, `WithholdingCertificateResult` para errores de negocio. |

---

## Estructura del repositorio

```
IA-AFIP/
├── .claude/                       # Configuración Claude Code (lineamientos obligatorios)
│   ├── CLAUDE.md                  # Constitución del repo
│   └── settings.local.json        # Permisos + hooks
├── docs/                          # Documentación
│   ├── certificate-setup.md       # Guía paso a paso para obtener/configurar el certificado en ARCA
│   ├── architecture.md            # Arquitectura del SDK
│   ├── afip-api-technical-summary.md  # Resumen técnico de los WS de AFIP
│   ├── claude-configuration.md    # Por qué Claude está configurado así
│   └── portfolio-summary.md       # Este documento
├── src/Afip.Arca.Sdk/             # Librería NuGet
│   ├── Authentication/            # WSAA + firma CMS + caché TA
│   ├── Invoicing/                 # WSFEv1 + builder + validator
│   ├── IncomeTax/                 # Cálculo RG 830 + reporte SIRE
│   ├── Common/                    # Excepciones, time, soap base
│   ├── Configuration/             # Options + DI extensions
│   ├── AfipClient.cs              # Facade
│   └── Afip.Arca.Sdk.csproj
├── tests/Afip.Arca.Sdk.Tests/     # Suite xUnit (25 tests)
├── implementation/                # Demo interactiva de consumo del NuGet
│   ├── NuGet.config               # Feed local D:\Code\projects\artifacts + packageSourceMapping
│   └── Afip.Arca.Sdk.Demo/        # Consola con wizard de setup + menú de 8 operaciones
├── Directory.Build.props          # Versionado, reglas y PackageOutputPath centralizados
├── Afip.Arca.Sdk.sln
└── README.md
```

**Artefactos:** los `.nupkg`/`.snupkg` se publican en `D:\Code\projects\artifacts` (definido vía `<PackageOutputPath>`). La carpeta `implementation/` consume el paquete desde ese mismo path, simulando el ciclo real de un consumidor del NuGet.

---

## Lineamientos de calidad (obligatorios por configuración)

Definidos en `.claude/CLAUDE.md`, reforzados con hook `UserPromptSubmit` en `settings.local.json`:

- **Clean Architecture / SOLID** estrictos. La dependency rule no se viola.
- **Nullable Reference Types ON** + **TreatWarningsAsErrors**.
- **Async hasta el final** — sin sincronización forzada.
- **Excepciones tipadas** — sin `throw new Exception("…")`.
- **Sin estado global** — toda dependencia se inyecta.
- **XMLDoc obligatoria** en tipos públicos, `GenerateDocumentationFile=true`.
- **Conventional Commits** y SemVer 2.0.0.
- **PII y secrets nunca en logs.**

Cuando Claude recibe un prompt nuevo, el hook re-inyecta estos puntos como recordatorio explícito. Cuando una solicitud rompe una regla, Claude debe plantearlo antes de implementar, no hacer la excepción silenciosamente.

---

## Cobertura del Web Service de AFIP

| WS | Implementación | Validado en AFIP real |
|---|---|---|
| WSAA `loginCms` | ✅ Completa | ✅ TA real obtenido en homologación (mayo 2026) |
| WSFEv1 `FEDummy` | ✅ Completa | ✅ `AppServer/DbServer/AuthServer = OK` |
| WSFEv1 `FECompUltimoAutorizado` | ✅ Completa | ✅ Probado contra punto de venta real |
| WSFEv1 `FECAESolicitar` | ✅ Completa | ✅ **CAE 86200173262441** emitido para FacturaB |
| WSFEv1 `FECAESolicitar` (Notas de Crédito/Débito con `CbtesAsoc`) | ✅ Completa | ✅ **CAE 86200173263879** emitido para NotaCreditoB |
| RG 5616/2024 `CondicionIVAReceptorId` | ✅ Completa (v1.0.2) | ✅ Aceptado por AFIP |
| WSFEv1 `FECompConsultar` | 🔜 Roadmap | — |
| WSFEv1 `FEParamGet*` | 🔜 Roadmap | — |
| SIRE `emitir` / `anular` / `consultar` | ✅ Implementada | ⚠️ **No validado end-to-end** — el wire format se codificó desde spec |
| WSFEXv1 (exportación) | 🔜 Roadmap | — |

> 💡 Detalle completo del estado y próximos pasos en [`docs/roadmap.md`](roadmap.md).

## Bugs descubiertos durante la validación contra AFIP real

La cadena de testing real reveló y corrigió tres bugs no triviales que el testing unitario no podía detectar:

| Bug | Causa | Versión |
|---|---|---|
| `HttpSoapInvoker` rechazaba `soapAction` vacío | WSAA `loginCms` requiere `SOAPAction: ""` (vacío entre comillas); el validador era más estricto que el estándar SOAP 1.1 | 1.0.1 |
| Falta de `CondicionIVAReceptorId` en el request | AFIP introdujo el campo como obligatorio en RG 5616/2024 — la spec original que se usó como base no lo incluía | 1.0.2 |
| `RSACertificateExtensions.CopyWithPrivateKey` ambiguidad de overload en PowerShell | Llamada explícita al extension method en lugar del método de instancia (`$cert.CopyWithPrivateKey(...)` resolvía mal) | script |

Esto demuestra el valor de **probar contra el ambiente real** además de tener cobertura unitaria — los bugs estaban en las "costuras" entre el dominio del SDK y la API externa.

---

## Roadmap

Detalle completo en [`docs/roadmap.md`](roadmap.md). Resumen de prioridades:

- 🥇 **P0 — Multi-tenancy (v1.1.0)**: `IAfipClientFactory` + Named Options para servir N CUITs en un mismo proceso.
- 🥈 **P1 — Validar SIRE contra AFIP real**: end-to-end no probado todavía.
- 🥉 **P2 — Completar superficie WSFEv1**: `FECompConsultar`, `FEParamGet*` con caché 24 h.
- **P3**: multi-comprobante por request, tributos personalizados, WSFEXv1 (exportación), Redis cache, CI/CD a NuGet.org.

---

## Cierre

Este proyecto demuestra:

1. **Capacidad para diseñar arquitectura limpia** en un dominio con superficie técnica compleja (criptografía, SOAP, reglas fiscales).
2. **Disciplina en buenas prácticas** — testabilidad, multi-target, options pattern, async, manejo de errores tipado.
3. **Conocimiento de dominio** — entendimiento real de WSAA, WSFEv1 y el régimen RG 830, no solo de la mecánica de un cliente SOAP genérico.
4. **Pensamiento en producto** — el SDK no es un script, es un paquete pensado para que otro equipo lo consuma sin documentación adicional.
5. **Uso responsable de IA asistida** — Claude Code configurado con guías versionadas, hooks que refuerzan los principios, documentación de las decisiones de configuración como parte del entregable.

---

> **Tech stack:** C# 12 · .NET 8 + .NET Standard 2.0 · `Microsoft.Extensions.*` · Polly · System.Security.Cryptography.Pkcs · xUnit · FluentAssertions · NSubstitute
> **Dominio:** Argentina · Fiscal · AFIP/ARCA · RG 4291 · RG 830/5423 · SIRE
> **Estado:** v1.0.0 — listo para publicación NuGet preview
