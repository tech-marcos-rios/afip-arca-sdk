# Cómo obtener y configurar el certificado para AFIP/ARCA

> Guía paso a paso para conseguir el certificado X.509 que el SDK necesita para autenticarse contra los Web Services de **AFIP/ARCA** (Argentina).
> Esta es la parte que **no se puede automatizar del todo**: requiere acciones manuales en el portal de AFIP con Clave Fiscal del contribuyente. La guía está validada end-to-end contra homologación en mayo de 2026 — emite una factura electrónica real al terminar.

---

## ¿Por qué es importante?

El certificado X.509 es lo que prueba que sos quien decís ser ante AFIP. Sin él, **ninguno de los Web Services responde**. El proceso tiene varios pasos que no son intuitivos y donde es fácil trabarse:

- Hay que **adherir un servicio** antes de poder usarlo (paso fácil de pasar por alto).
- WSASS **no es accesible por URL directa** — solo aparece después de autenticarse en el portal.
- El alias del DN en WSASS tiene **restricciones más estrictas** que el CN del certificado.
- El certificado por sí solo no alcanza: hay que **autorizarlo explícitamente** a cada Web Service que uno vaya a usar.

Esta guía cubre los tres frentes: lo automatizable (con el script), lo manual del portal AFIP, y los criterios de seguridad para el `.pfx` resultante.

---

## Pre-requisitos

| Item | Cómo conseguirlo |
|---|---|
| **Clave Fiscal nivel 3** del contribuyente | Si todavía no la tenés en nivel 3: la subís en cualquier sucursal AFIP o vía videoconferencia. Las claves nivel 2 NO sirven para servicios interactivos. |
| **PowerShell 7+** instalado | `winget install Microsoft.PowerShell` o desde [https://github.com/PowerShell/PowerShell/releases](https://github.com/PowerShell/PowerShell/releases). El script declara `#Requires -Version 7.0`. |
| **El SDK clonado** | Este repo (`Afip.Arca.Sdk`). El script vive en [`scripts/New-AfipCertificate.ps1`](../scripts/New-AfipCertificate.ps1). |
| **Browser moderno** | Chrome / Edge / Firefox. El portal de AFIP es flaky en navegadores muy nuevos a veces — si una pantalla se ve mal, probá en otra ventana incógnito. |

---

## Diferencias homologación / producción

| Aspecto | Homologación | Producción |
|---|---|---|
| Servicio para generar certificados | **WSASS** (Autoservicio de Acceso a APIs de Homologación) | **Administrador de Certificados Digitales** |
| Endpoints de los WS | `wswhomo.afip.gov.ar`, `wsaahomo.afip.gov.ar` | `servicios1.afip.gov.ar`, `wsaa.afip.gov.ar` |
| Costo | Gratis | Gratis |
| Validez del certificado | 2 años | 2 años |
| Datos reales | NO — los CAE de homologación son ficticios | SÍ — emitís comprobantes con efectos fiscales reales |
| Cuándo usar cuál | Toda la vida del desarrollo y QA | Solamente cuando el sistema está listo para producción real |

> ⚠️ **Cero excepciones:** nunca debuggear ni "probar rápido" contra producción. AFIP no permite anular comprobantes — si emitís una factura por error con datos de prueba, vas a tener que emitirle una NC y conservarla por 10 años.

Esta guía describe el flujo de **homologación** en detalle. Para producción, el flujo es estructuralmente idéntico — solo cambian los nombres de los servicios y los endpoints.

---

## Fase 0 — Adherir el servicio WSASS

WSASS solo aparece en tu escritorio de AFIP si lo **adheriste** previamente desde el Administrador de Relaciones.

1. Entrar a **[https://www.afip.gob.ar](https://www.afip.gob.ar)**.
   - No intentes ir directo a `wsass-homo.afip.gob.ar/wsass/`: te va a aparecer en blanco. El portal requiere venir con sesión federada desde AFIP.
2. Click en **"Acceso con Clave Fiscal"** → ingresar CUIT + clave fiscal.
3. En el escritorio buscar **"Administrador de Relaciones de Clave Fiscal"** y entrar.
4. En el panel "Servicio Habilitado", click en **"Adherir Servicio"**.
5. Navegar por la estructura: **ARCA → Servicios Interactivos → WSASS - Autogestión Certificados Homologación**.
6. Confirmar.
7. **Cerrar sesión y volver a entrar** a [https://www.afip.gob.ar](https://www.afip.gob.ar) — los servicios recién adheridos solo aparecen después de un re-login.
8. En el escritorio, ahora debería aparecer **"WSASS - Autogestión Certificados Homologación"**. Click ahí.
9. El portal te redirige a `wsass-homo.afip.gob.ar/wsass/` y esta vez carga bien porque ya estás autenticado.

---

## Fase 1 — Generar la clave privada y el CSR

Esto se hace **localmente**, no en el portal AFIP. El CSR (Certificate Signing Request) es lo que vas a subir a WSASS para que ellos te firmen el certificado.

### Comando

```powershell
cd scripts
.\New-AfipCertificate.ps1 -Mode Csr -CommonName afipsdkpoc -Cuit <tu-cuit-11-digitos>
```

### Restricciones importantes del `-CommonName`

| Restricción | Por qué |
|---|---|
| **Solo letras y números** (sin guiones, puntos ni guiones bajos) | WSASS rechaza el alias con un mensaje "El Nombre simbólico del DN sólo puede contener números y/o letras". El estándar X.509 permite más caracteres, pero WSASS es más estricto que el estándar. |
| **Sin espacios, sin acentos** | Mismo motivo. |
| **Hasta ~30 caracteres** | Más es feo en logs y no aporta. |

Por convención usamos lo mismo de `-CommonName` como alias en WSASS — así evitás confusiones más adelante.

### Qué produce el script

Dos archivos en `scripts\certs\`:

| Archivo | Contenido | Sensibilidad |
|---|---|---|
| `<CN>.key` | Clave privada PEM (PKCS#8 sin cifrar) | **MÁXIMA** — nunca commitearla ni compartirla |
| `<CN>.csr` | Solicitud de firma | Pública — esto es lo que subís a WSASS |

El `.gitignore` del repo ya excluye `*.key`, `*.csr`, `*.crt` y `*.pfx`, además de la carpeta `scripts/certs/` entera.

---

## Fase 2 — Subir el CSR a WSASS

Esto es manual y se hace desde el navegador.

1. Estando en WSASS, click en **"Nuevo Certificado"** (menú izquierdo).
2. Te aparece el formulario **"Crear DN y certificado"** con tres campos:

| Campo | Qué poner |
|---|---|
| **1. Nombre simbólico del DN** | El mismo CN que pasaste al script. Ej.: `afipsdkpoc`. **Solo alfanuméricos.** |
| **2. CUIT del contribuyente** | Ya viene prellenado con tu CUIT. No tocar. |
| **3. Solicitud de certificado en formato PKCS#10** | Pegar **TODO el contenido del `.csr`**, incluidas las líneas `-----BEGIN CERTIFICATE REQUEST-----` y `-----END CERTIFICATE REQUEST-----`. |

3. Click en **"Crear DN y obtener certificado"**.

### Cómo abrir y copiar el CSR

```powershell
notepad scripts\certs\<CN>.csr
```

Ctrl+A → Ctrl+C → pegar en el formulario.

### Qué ves al terminar

El campo **"Resultado"** del formulario se llena con un bloque entre `-----BEGIN CERTIFICATE-----` y `-----END CERTIFICATE-----`. Eso es tu certificado firmado.

**Guardalo en disco** como `scripts\certs\<CN>.crt` (dentro del repo — ya está en `.gitignore`). Una forma fácil, parado en la raíz del repo:

```powershell
# Copiar el contenido del campo Resultado al portapapeles, después:
Get-Clipboard | Set-Content -Path 'scripts\certs\afipsdkpoc.crt' -Encoding utf8
```

---

## Fase 3 — Autorizar el certificado a los Web Services

**Crítico — sin este paso, WSAA va a rechazar tu TA con `cms.cert.notFound` o equivalentes.**

Crear el DN no autoriza automáticamente a usar los WS de negocio. Cada WS al que vayas a acceder requiere una autorización explícita.

1. En WSASS, menú izquierdo: **"Crear autorización a servicio"**.
2. Completar el formulario:

| Campo | Valor |
|---|---|
| 1. Nombre simbólico del DN a autorizar | `SERIALNUMBER=CUIT <tu cuit>, CN=<tu CN>` (dropdown — elegí el DN recién creado) |
| 2. CUIT del DN a autorizar | tu CUIT (prellenado) |
| 3. CUIT representado | tu CUIT |
| 4. CUIT de quien genera la autorización | tu CUIT (prellenado) |
| 5. **Servicio al que desea acceder** | dropdown — buscar y elegir el WS |

3. Para esta librería, autorizar los siguientes servicios (uno a la vez):
   - **`wsfe`** — Factura Electrónica (obligatorio si vas a emitir comprobantes)
   - **`sire-ws`** — SIRE / retenciones (opcional, solo si vas a informar retenciones)

4. Click en **"Crear autorización de acceso"**.

5. El cuadro "Resultado" debe mostrar un XML con `<resultado>OK</resultado>` (o similar). Si lo ves, la autorización quedó grabada del lado de AFIP — **no hace falta guardar el XML de respuesta**.

---

## Fase 4 — Ensamblar el `.pfx`

El SDK no consume `.crt` + `.key` por separado: necesita un `.pfx` (PKCS#12) que combine ambos y esté protegido por contraseña.

```powershell
cd scripts
.\New-AfipCertificate.ps1 -Mode Pfx -CommonName afipsdkpoc -CrtPath "$PWD\certs\afipsdkpoc.crt"
```

> 💡 Pasale **ruta absoluta** al `-CrtPath` — por eso el ejemplo usa `$PWD` en vez de una ruta relativa. PowerShell y .NET interpretan rutas relativas distinto (`$PWD` ≠ `Environment.CurrentDirectory`); el script ya normaliza esto pero es buena costumbre.

El script te pide la contraseña dos veces (anotala — el SDK la necesita).

Resultado: `scripts\certs\<CN>.pfx`.

---

## Fase 5 — Configurar el SDK con el `.pfx`

Una vez tenés el `.pfx`, la configuración en código es directa:

```csharp
using Afip.Arca.Sdk.Configuration;

services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(c =>
        c.FromFile(@"scripts\certs\afipsdkpoc.pfx", // ruta relativa a la raíz del repo — ajustala si tu app vive en otro lado
                   password: "<tu password>"));
});
```

O directamente vía la demo interactiva del repo, parado en la raíz:

```powershell
cd implementation
dotnet run --project Afip.Arca.Sdk.Demo
```

…y elegir **"Firma local con certificado X.509"** cuando lo pida.

---

## Buenas prácticas de seguridad

- **El `.key` queda sin cifrar en disco.** El estándar PKCS#8 que produce el script no lleva password. Protegé la carpeta con permisos NTFS o BitLocker/EFS. Si la perdés, regenerás el certificado entero (no es el fin del mundo en homologación; sí lo es en producción).
- **El `.pfx` SÍ está cifrado** con la contraseña que ingresaste. Es razonable distribuirlo entre máquinas si la password se maneja como secreto.
- **Para producción:** guardá el `.pfx` en Azure Key Vault / AWS Secrets Manager / HSM. No lo despliegues como archivo plano con la app.
- **Si tu cert está comprometido:** generá uno nuevo (con un CN distinto), autorizalo, y revocá el anterior desde WSASS (sección "Certificados").
- **Nunca commitees `.key`, `.crt`, `.csr` o `.pfx`** — el `.gitignore` del repo los excluye, pero si modificás la estructura, validá que la exclusión siga aplicando.
- **Logueá con cuidado:** el SDK ya enmascara token/sign/CUIT en logs (`[REDACTED]`), pero si extendés la librería, no rompas esto.

---

## Renovación

Los certificados de AFIP vencen a los **2 años**. Cuando se acerque el vencimiento:

1. **No hace falta esperar al vencimiento real** — podés generar un certificado nuevo y autorizarlo en paralelo, y switchear cuando estés listo.
2. **Pre-30 días:** generar nuevo CSR con el mismo script (mismo `-CommonName` o uno nuevo, da lo mismo).
3. Subir a WSASS, autorizar a `wsfe`/`sire-ws`, ensamblar `.pfx`.
4. Actualizar la configuración del SDK con la nueva ruta.
5. Una vez en producción con el nuevo, **revocar el viejo** desde WSASS para reducir superficie de ataque.

---

## Troubleshooting

| Síntoma | Causa probable | Solución |
|---|---|---|
| Pantalla en blanco al ir a `wsass-homo.afip.gob.ar/wsass/` | No estás autenticado con Clave Fiscal | Entrá desde `afip.gob.ar` con clave fiscal y navegá al servicio WSASS desde el escritorio. |
| No veo WSASS en el escritorio aún después del login | Falta adherir el servicio | Adherir desde "Administrador de Relaciones" (Fase 0). |
| WSASS dice "El Nombre simbólico del DN sólo puede contener números y/o letras" | Pusiste guiones, puntos o caracteres especiales en el alias | Usar solo alfanuméricos. Ej.: `afipsdkpoc` (no `afip-sdk-poc`). |
| WSASS dice "CSR inválido" | Pegaste solo el blob base64, sin las líneas `BEGIN/END` | Pegar TODO el contenido del `.csr`, incluyendo las líneas de header y footer. |
| WSAA responde `cms.sign.invalid` | Encoding del XML del TRA con BOM | El SDK ya escribe sin BOM — esto suele indicar que estás usando una versión vieja. Actualizá a la última. |
| WSAA responde `cms.cert.notFound` | El cert no está autorizado al servicio | Volvé a WSASS → "Crear autorización a servicio" para `wsfe` (Fase 3). |
| WSAA responde `coe.alreadyAuthenticated` | Re-uso del `uniqueId` en el TRA | Esperá 10 minutos o reiniciá el proceso. El SDK usa `unix_seconds + counter`, lo cual normalmente alcanza para no colisionar. |
| WSFEv1 responde `1000` (Token inválido) | Estás usando producción con un cert de homologación o viceversa | Verificá que `AfipEnvironment` matchee con el ambiente para el que se emitió el certificado. |
| WSFEv1 responde `10246` ("Campo Condición Frente al IVA del receptor es obligatorio") | RG 5616/2024 — el campo `CondicionIVAReceptorId` es ahora obligatorio | El SDK ya emite el campo automáticamente. Si tu receptor es CUIT o DNI, usá `.WithReceiverVatCondition(...)` en el builder. |
| Polly retry exponencial saltando | Homologación caída (frecuente lunes/martes a la mañana) | Probá `FEDummy` aparte para confirmar. Si todos los subsistemas están OK pero igual hay errores, abrí un caso a AFIP. |

---

## Resumen del flujo en una imagen mental

```
┌──────────────────────────────────────────────────────────────┐
│ FASE 0: Adherir WSASS (una vez por CUIT)                     │
│   afip.gob.ar → Admin de Relaciones → Adherir WSASS          │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ FASE 1: Generar .key + .csr (local, automatizado)            │
│   .\New-AfipCertificate.ps1 -Mode Csr -CommonName X -Cuit Y  │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ FASE 2: Subir CSR a WSASS (manual, navegador)                │
│   WSASS → Nuevo Certificado → pegar .csr → bajar .crt        │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ FASE 3: Autorizar cert a wsfe (y sire-ws si aplica)          │
│   WSASS → Crear autorización a servicio → resultado=OK       │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ FASE 4: Ensamblar .pfx (local, automatizado)                 │
│   .\New-AfipCertificate.ps1 -Mode Pfx -CommonName X -CrtPath │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│ FASE 5: Configurar el SDK con el .pfx                        │
│   opts.UseLocalCertificateSigning(c => c.FromFile(...))      │
└──────────────────────────────────────────────────────────────┘
```

Si llegaste hasta acá y la opción **"Health check (FEDummy)"** de la demo te responde tres `OK`, ya tenés todo listo para emitir comprobantes electrónicos reales contra homologación.

---

## Referencias oficiales

- AFIP — [Documentación de Web Services](https://www.afip.gob.ar/ws/documentacion/)
- AFIP — [Manual de WSASS — Cómo adherirse al servicio](https://www.afip.gob.ar/ws/WSASS/WSASS_como_adherirse.pdf)
- AFIP — [Manual de WSASS — Operación](https://www.afip.gob.ar/ws/WSASS/WSASS_manual.pdf)
- AFIP — [Obtener Certificado para Producción](https://www.afip.gob.ar/ws/WSAA/wsaa_obtener_certificado_produccion.pdf)
- AFIP — [Generación de Certificados — guía oficial](https://www.afip.gob.ar/ws/wsaa/wsaa.obtenercertificado.pdf)
- AfipSDK (tercero) — [Habilitar administrador de certificados de testing](https://docs.afipsdk.com/recursos/tutoriales-pagina-de-arca/habilitar-administrador-de-certificados-de-testing)
