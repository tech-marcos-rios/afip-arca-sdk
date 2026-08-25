# Resumen técnico — Web Services de AFIP/ARCA

> Documento de referencia para implementadores. Compilado en mayo de 2026 a partir de la documentación oficial de [www.afip.gob.ar](https://www.afip.gob.ar/) / [www.arca.gob.ar](https://www.arca.gob.ar/) y manuales del desarrollador en circulación.

---

## 1. Contexto: AFIP → ARCA

En 2024, la AFIP (Administración Federal de Ingresos Públicos) fue reorganizada como **ARCA** (Agencia de Recaudación y Control Aduanero). Los endpoints históricos (`afip.gov.ar`) **siguen activos**; en paralelo se publicaron los espejos `arca.gov.ar`. La nomenclatura técnica (WSAA, WSFEv1, etc.) se mantuvo.

Esta librería usa por defecto los hosts AFIP heredados (`afip.gov.ar`, ver tablas §2.1 y §3.1), con override configurable vía `AfipOptions.Endpoints` hacia los espejos `arca.gov.ar` cuando haga falta.

---

## 2. Modelo de seguridad: WSAA

Todos los Web Services de Negocio (WSN) de AFIP requieren un **Ticket de Acceso (TA)** emitido por el **Web Service de Autenticación y Autorización (WSAA)**.

### 2.1 Endpoints

| Ambiente | URL del servicio |
|---|---|
| Homologación | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` |
| Producción | `https://wsaa.afip.gov.ar/ws/services/LoginCms` |

(Equivalentes en `arca.gov.ar` disponibles.)

### 2.2 Flujo (alto nivel)

```
┌──────────────┐     1. TRA (XML)     ┌──────────────┐
│  Aplicación  │ ───────────────────▶ │  Aplicación  │
│              │                       │  (firma CMS) │
└──────────────┘                       └──────┬───────┘
                                              │ 2. CMS PKCS#7 (Base64)
                                              ▼
                                       ┌──────────────┐
                                       │     WSAA     │
                                       │  loginCms()  │
                                       └──────┬───────┘
                                              │ 3. TA (XML)
                                              ▼
                                       ┌──────────────┐
                                       │  Cache 12hs  │
                                       └──────────────┘
```

### 2.3 TRA (Ticket Request Access)

Documento XML que el cliente arma localmente:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header>
    <uniqueId>1715600000</uniqueId>
    <generationTime>2026-05-13T10:00:00-03:00</generationTime>
    <expirationTime>2026-05-13T10:10:00-03:00</expirationTime>
  </header>
  <service>wsfe</service>
</loginTicketRequest>
```

**Reglas:**
- `uniqueId` debe ser estrictamente creciente entre solicitudes para el mismo CUIT/servicio (típicamente unix-time).
- Ventana `generationTime` → `expirationTime` recomendada: 10 minutos.
- `service` identifica el WSN destino (`wsfe`, `wsfex`, `sire-ws`, etc.).

### 2.4 Firma CMS (PKCS#7)

El TRA se firma con el certificado X.509 del contribuyente:

1. Cargar certificado `.pfx`/`.p12` (CN configurado en WSASS para homologación, o emitido por el Administrador de Certificados Digitales en producción).
2. Generar un `SignedCms` (clase `System.Security.Cryptography.Pkcs.SignedCms`) con:
   - `Detached = false` (signed envelope).
   - `DigestAlgorithm = SHA-256`.
3. Codificar en Base64 y pasarlo como parámetro `in0` del método `loginCms`.

### 2.5 TA (Ticket de Acceso) — respuesta

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketResponse version="1.0">
  <header>
    <source>CN=wsaahomo,O=AFIP,...</source>
    <destination>SERIALNUMBER=CUIT 20123456789,...</destination>
    <uniqueId>1234567890</uniqueId>
    <generationTime>2026-05-13T10:00:05-03:00</generationTime>
    <expirationTime>2026-05-13T22:00:05-03:00</expirationTime>
  </header>
  <credentials>
    <token>PD94bWwgdmVyc2lvbj0iMS4w...</token>
    <sign>tH+W3vDc6XhRz5...</sign>
  </credentials>
</loginTicketResponse>
```

**Validez:** 12 horas. La librería **cachea** el TA por la dupla `(CUIT, service)` y lo renueva al detectar `expirationTime` próximo (5 minutos de margen).

### 2.6 Quirks operativos descubiertos contra AFIP real

| Quirk | Detalle |
|---|---|
| **`SOAPAction: ""` obligatorio** | WSAA `loginCms` requiere el header `SOAPAction` presente pero **con valor vacío entre comillas**. Implementaciones que validan que `SOAPAction` sea no-vacío van a fallar — el SDK acepta `null`/empty en `IHttpSoapInvoker.InvokeAsync` y serializa como `SOAPAction: ""`. |
| **`uniqueId` estrictamente creciente** | El SDK combina `unix-seconds + counter atómico in-process` (`TraDocumentBuilder`). Esto evita colisiones aunque el caller pida dos TAs en el mismo segundo. Para múltiples procesos contra el mismo `(CUIT, service)`, necesitás coordinar (Redis lock o similar). |
| **Reloj sincronizado** | Si tu reloj se desfasa más de 10 minutos respecto del de AFIP, `cms.expired` o respuestas anómalas. Validar NTP en hosts productivos. |

### 2.7 Errores frecuentes

| Código / fault | Causa | Solución |
|---|---|---|
| `coe.alreadyAuthenticated` | Reuso de `uniqueId` antes del expiry. | Esperar a expirar o cambiar `uniqueId`. |
| `cms.cert.notFound` | Certificado no asociado al servicio en WSASS. | Vincular en WSASS / Administrador de Certificados Digitales. |
| `cms.sign.invalid` | Firma corrupta (suele ser BOM/encoding del XML). | Asegurar UTF-8 sin BOM y serialización canónica. |
| `cms.expired` | `expirationTime` del TRA en el pasado. | Sincronizar reloj y dejar margen razonable (5 min). |

---

## 3. Facturación Electrónica: WSFEv1

Servicio principal de emisión de comprobantes sin detalle de ítems, regulado por RG 4291 y sus modificatorias (incluida RG 5616/2024).

### 3.1 Endpoints

| Ambiente | URL del servicio |
|---|---|
| Homologación | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` |
| Producción | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` |

WSDL disponible agregando `?WSDL` al endpoint.

### 3.2 Métodos relevantes

| Método | Propósito |
|---|---|
| `FECAESolicitar` | **Solicita el CAE** (Código de Autorización Electrónica) para uno o más comprobantes. |
| `FECAEARegInformativo` | Informa CAEA ya usado (modalidad contingencia). |
| `FECAEASolicitar` | Solicita CAEA (autorización por anticipado). |
| `FECompUltimoAutorizado` | Devuelve el último número de comprobante autorizado para un punto de venta y tipo. |
| `FECompConsultar` | Consulta el detalle de un comprobante ya autorizado. |
| `FEParamGetTiposCbte` | Listado de tipos de comprobante (Factura A/B/C/M, ND, NC, etc.). |
| `FEParamGetTiposDoc` | Tipos de documento receptor (CUIT, CUIL, DNI, etc.). |
| `FEParamGetTiposIva` | Alícuotas de IVA (21%, 10.5%, 27%, 0%, exento, no gravado). |
| `FEParamGetTiposMonedas` | Monedas (PES, DOL, EUR…). |
| `FEParamGetCotizacion` | Cotización oficial de una moneda en una fecha. |
| `FEParamGetTiposConcepto` | Concepto (1=Productos, 2=Servicios, 3=Productos y Servicios). |
| `FEParamGetPtosVenta` | Puntos de venta habilitados. |
| `FEDummy` | Health-check de AppServer/DbServer/AuthServer. |

### 3.3 Autenticación en cada request

Todos los métodos (excepto `FEDummy`) reciben un bloque `Auth`:

```xml
<Auth>
  <Token>{token del TA}</Token>
  <Sign>{sign del TA}</Sign>
  <Cuit>{cuit emisor}</Cuit>
</Auth>
```

### 3.4 Estructura `FECAESolicitar`

```xml
<FeCAEReq>
  <FeCabReq>
    <CantReg>1</CantReg>
    <PtoVta>1</PtoVta>
    <CbteTipo>1</CbteTipo>          <!-- 1=Factura A -->
  </FeCabReq>
  <FeDetReq>
    <FECAEDetRequest>
      <Concepto>1</Concepto>         <!-- 1=Productos -->
      <DocTipo>80</DocTipo>          <!-- 80=CUIT -->
      <DocNro>20123456789</DocNro>
      <CbteDesde>1</CbteDesde>
      <CbteHasta>1</CbteHasta>
      <CbteFch>20260513</CbteFch>    <!-- AAAAMMDD -->
      <ImpTotal>12100</ImpTotal>
      <ImpTotConc>0</ImpTotConc>
      <ImpNeto>10000</ImpNeto>
      <ImpOpEx>0</ImpOpEx>
      <ImpIVA>2100</ImpIVA>
      <ImpTrib>0</ImpTrib>
      <MonId>PES</MonId>
      <MonCotiz>1</MonCotiz>
      <Iva>
        <AlicIva>
          <Id>5</Id>                 <!-- 5=21% -->
          <BaseImp>10000</BaseImp>
          <Importe>2100</Importe>
        </AlicIva>
      </Iva>
    </FECAEDetRequest>
  </FeDetReq>
</FeCAEReq>
```

**Reglas críticas de los importes:**

```
ImpTotal = ImpNeto + ImpIVA + ImpTrib + ImpTotConc + ImpOpEx
ImpIVA   = Σ(AlicIva.Importe)
ImpNeto  = Σ(AlicIva.BaseImp)   (cuando todo es gravado)
```

AFIP **rechaza** el comprobante (no observa, rechaza) si las sumas no cuadran a la última centésima.

### 3.5 Respuesta `FECAESolicitar`

```xml
<FECAEResponse>
  <FeCabResp>
    <Cuit>20123456789</Cuit>
    <PtoVta>1</PtoVta>
    <CbteTipo>1</CbteTipo>
    <FchProceso>20260513100000</FchProceso>
    <CantReg>1</CantReg>
    <Resultado>A</Resultado>         <!-- A=Aprobado, R=Rechazado, P=Parcial -->
    <Reproceso>N</Reproceso>
  </FeCabResp>
  <FeDetResp>
    <FECAEDetResponse>
      <Concepto>1</Concepto>
      <DocTipo>80</DocTipo>
      <DocNro>20123456789</DocNro>
      <CbteDesde>1</CbteDesde>
      <CbteHasta>1</CbteHasta>
      <CbteFch>20260513</CbteFch>
      <Resultado>A</Resultado>
      <CAE>74123456789012</CAE>
      <CAEFchVto>20260523</CAEFchVto>
      <Observaciones>...</Observaciones>
    </FECAEDetResponse>
  </FeDetResp>
  <Errors>...</Errors>                <!-- presente si Resultado=R o P -->
</FECAEResponse>
```

### 3.6 Tipos de comprobante (subset relevante)

| `CbteTipo` | Descripción |
|---|---|
| 1 | Factura A |
| 2 | Nota de Débito A |
| 3 | Nota de Crédito A |
| 6 | Factura B |
| 7 | Nota de Débito B |
| 8 | Nota de Crédito B |
| 11 | Factura C |
| 12 | Nota de Débito C |
| 13 | Nota de Crédito C |
| 51 | Factura M |
| 52 | Nota de Débito M |
| 53 | Nota de Crédito M |
| 201–203 | FCE A / ND A / NC A (Factura de Crédito Electrónica) |

### 3.7 "Anulación" de una factura

**AFIP no permite anular un comprobante autorizado.** El mecanismo legal y técnico es:

1. Emitir una **Nota de Crédito** (`CbteTipo` 3, 8 o 13 según la letra) por el mismo importe.
2. Vincularla al comprobante original mediante `CbtesAsoc`:
   ```xml
   <CbtesAsoc>
     <CbteAsoc>
       <Tipo>1</Tipo>
       <PtoVta>1</PtoVta>
       <Nro>1</Nro>
       <Cuit>20123456789</Cuit>
       <CbteFch>20260513</CbteFch>
     </CbteAsoc>
   </CbtesAsoc>
   ```

La librería expone esto como `IInvoiceService.CancelAsync(invoiceRef, ...)` que internamente arma la Nota de Crédito apropiada.

### 3.8 Códigos de error frecuentes

| Código | Descripción |
|---|---|
| 10015 | CbteFch fuera de rango (más de 10 días pasados / 1 día futuro para productos). |
| 10016 | CbteDesde no es el siguiente al último autorizado. |
| 10018 | DocNro inválido para DocTipo. |
| 10019 | Comprobante ya autorizado anteriormente. |
| 10048 | ImpTotal no coincide con la suma de las parciales. |
| 10063 | Punto de venta inexistente o inhabilitado para WSFEv1. |
| 1000  | Token inválido o vencido. **`InvoiceService` lo maneja automáticamente**: invalida el TA cacheado y reintenta una vez con uno nuevo (ver `docs/04-architecture.md` ADR-003). Si aparece dos veces seguidas, el problema no es cacheo — revisar reloj del host o vínculo del certificado en WSASS. |
| 1005  | Cuit del Auth no coincide con el del TA. |

---

## 4. Retenciones del Impuesto a las Ganancias — RG 830/2000

Este es un cálculo **local** que la librería realiza antes de informar a SIRE.

### 4.1 Conceptos básicos

| Término | Significado |
|---|---|
| Sujeto retenido | La persona/empresa a la que se le retiene. |
| Agente de retención | Quien retiene y deposita la retención (el contribuyente que usa la librería). |
| Régimen | Código identificatorio del concepto retenible (ej. 19 = Profesionales, 116 = Locaciones de inmuebles urbanos). |
| Mínimo no imponible | Umbral por debajo del cual no corresponde retener. |
| Escala | Tabla con tramos (monto desde / monto hasta) → monto fijo + alícuota marginal. |
| Inscripto | Estado del sujeto en el padrón de Ganancias. Cambia la base y/o alícuota. |

### 4.2 Algoritmo (caso general, RG 830)

```
1. Determinar el régimen aplicable (depende de la naturaleza del pago).
2. Sumar todos los pagos realizados al mismo sujeto en el mismo mes
   para el mismo régimen (acumulación mensual).
3. Restar al subtotal el mínimo no imponible del régimen.
   Si resultado ≤ 0 → no se retiene.
4. Aplicar la escala del régimen al subtotal neto del MNI:
   retención_acumulada = monto_fijo + (subtotal_neto_MNI - desde) * alicuota
5. Restar a la retención acumulada las retenciones ya practicadas
   al mismo sujeto en el mes para el mismo régimen.
6. Si el sujeto NO está inscripto, en lugar de la escala se aplica
   una alícuota fija (típicamente 28% para profesionales).
7. Si retención_a_practicar < mínimo a retener (RG vigente, $240
   para profesionales en 2024-25, actualizable) → no se retiene.
```

### 4.3 Datos de tabla (vigentes a octubre 2024, RG 5423)

> ⚠️ Estos valores **se actualizan periódicamente**. La librería los carga desde una tabla versionada (`IIncomeTaxScaleProvider`), no hardcodeada.

**Régimen 19 — Profesiones liberales y oficios:**

- Mínimo no imponible: **$160.000** (mensual acumulado).
- Mínimo de retención: **$240**.
- Alícuota para no inscriptos: **28%**.
- Escala (montos en pesos, alícuotas marginales):

| Desde | Hasta | Fijo | Alícuota sobre excedente |
|---:|---:|---:|---:|
| 0 | 7.500 | 0 | 5% |
| 7.500 | 15.000 | 375 | 9% |
| 15.000 | 22.500 | 1.050 | 12% |
| 22.500 | 45.000 | 1.950 | 15% |
| 45.000 | 75.000 | 5.325 | 19% |
| 75.000 | 112.500 | 11.025 | 23% |
| 112.500 | 187.500 | 19.650 | 27% |
| 187.500 | ∞ | 39.900 | 31% |

### 4.4 Salida del cálculo

```csharp
public sealed record IncomeTaxWithholdingResult(
    decimal WithholdableBase,          // Base sobre la que se calcula
    decimal AccumulatedWithholding,    // Antes de descontar previas
    decimal PreviouslyWithheld,        // En el mes
    decimal WithholdingAmount,         // Final a practicar
    bool Applies,                      // false si cae bajo mínimo
    string? NotAppliedReason);
```

---

## 5. SIRE — Sistema Integral de Retenciones Electrónicas

Servicio mediante el cual el agente **informa** las retenciones efectivamente practicadas.

### 5.1 Endpoints

SIRE-WS está disponible vía SOAP con autenticación WSAA (`service=sire-ws`). Hay una superficie API REST alternativa expuesta por intermediarios (no oficial), pero esta librería usa **únicamente** los WS oficiales.

### 5.2 Operaciones principales

| Operación | Propósito |
|---|---|
| `emitir` | Emitir un certificado de retención (genera el formulario F. 2003 / F. 2004 según corresponda). |
| `anular` | Anular un certificado emitido. |
| `consultar` | Consultar el detalle de un certificado por número. |
| `listar` | Listar certificados emitidos en un rango. |
| `dummy` | Health-check. |

### 5.3 Parámetros típicos de `emitir`

| Campo | Tipo | Descripción |
|---|---|---|
| `cuitAgente` | string(11) | CUIT del agente de retención. |
| `impuesto` | int | Código de impuesto (217=Ganancias, 767=IVA, 308=Seguridad Social). |
| `regimen` | int | Código de régimen (depende del impuesto). |
| `fechaRetencion` | date | Fecha en que se practica. |
| `cuitRetenido` | string(11) | CUIT del sujeto. |
| `importeBase` | decimal | Base imponible. |
| `importeRetencion` | decimal | Monto retenido. |
| `tipoComprobante` | int | Comprobante asociado (factura, etc.). |
| `numeroComprobante` | string | Número del comprobante asociado. |
| `condicion` | int | Inscripto / no inscripto / excluido. |

### 5.4 Resultado

```xml
<resultado>
  <numeroCertificado>2024000123456</numeroCertificado>
  <fechaEmision>2026-05-13</fechaEmision>
  <estado>EMITIDO</estado>
</resultado>
```

---

## 6. Buenas prácticas operativas aprendidas

Estas no figuran explícitamente en los manuales pero son críticas en producción:

1. **`PtoVta` aislado por máquina/proceso.** AFIP rechaza una solicitud si el `CbteDesde` no es exactamente el siguiente al último autorizado. Si dos procesos usan el mismo PtoVta concurrentemente, uno fallará. → Asignar puntos de venta distintos por instancia o serializar con un lock distribuido.
2. **Antes de `FECAESolicitar`, llamar a `FECompUltimoAutorizado`** para conocer el último número y armar el `CbteDesde` correcto. La librería lo hace internamente cuando se invoca `IInvoiceService.AuthorizeAsync` sin número explícito.
3. **Cachear los tablas `FEParamGet*`** (tipos de comprobante, IVA, moneda). Cambian rara vez; cachear 24 hs es razonable y reduce latencia drásticamente.
4. **Sincronizar reloj NTP.** El WSAA rechaza TRAs con desfase de más de unos minutos.
5. **No reintentar `FECAESolicitar` ciegamente.** Si AFIP respondió `Resultado=A` y la red murió antes de leer el CAE, **el comprobante está autorizado**. Hay que consultar con `FECompConsultar` antes de reintentar — de lo contrario se duplica.
6. **Homologación es ruidosa.** El App/AuthServer de homologación cae con cierta frecuencia. `FEDummy` permite distinguir falla nuestra de falla AFIP.

---

## 7. Referencias

- AFIP — [Documentación de Web Services](https://www.afip.gob.ar/ws/documentacion/)
- AFIP — [Manual WSAA](https://www.afip.gob.ar/ws/WSAA/WSAAmanualDev.pdf)
- AFIP — [Manuales WSFEv1](https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp)
- AFIP — [SIRE](https://www.afip.gob.ar/sire/)
- AFIP — [Consultas frecuentes RG 830](https://servicioscf.afip.gob.ar/publico/abc/ABCpaso2.aspx?cat=3304)
