# Scripts utilitarios

> **Si es la primera vez que conectás con AFIP, leé primero [`docs/02-certificate-setup.md`](../docs/02-certificate-setup.md).** Ese documento explica el flujo completo end-to-end (adherir WSASS, generar CSR, subirlo, autorizar a los WS, ensamblar el `.pfx`). Este README cubre solo la parte automatizada (Fases 1 y 4).

## `New-AfipCertificate.ps1`

Genera el material criptográfico para autenticarse contra los Web Services de AFIP/ARCA. Usa APIs nativas de .NET — **no requiere OpenSSL instalado**.

### Requisitos

- **PowerShell 7+** (el script declara `#Requires -Version 7.0`).
- CUIT con Clave Fiscal nivel 3 (para entrar a WSASS).
- Acceso a [https://wsass-homo.afip.gob.ar/wsass/](https://wsass-homo.afip.gob.ar/wsass/) (homologación).

### Flujo

```
┌─────────────────────────────────┐
│  1.  -Mode Csr                  │  ← genera <CN>.key + <CN>.csr
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  2.  WSASS (manual, navegador)  │  ← subir CSR, descargar <CN>.crt
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  3.  -Mode Pfx                  │  ← ensambla <CN>.pfx
└─────────────────────────────────┘
```

### Paso 1 — Generar la solicitud (CSR)

```powershell
cd scripts
.\New-AfipCertificate.ps1 -Mode Csr -CommonName afip-sdk-poc -Cuit 20123456789
```

Produce dos archivos en `.\certs\`:

- `afip-sdk-poc.key` — clave privada PEM (PKCS#8 sin cifrar).
- `afip-sdk-poc.csr` — pedido de firma para subir a WSASS.

Parámetros opcionales:

| Param | Default | Descripción |
|---|---|---|
| `-Organization` | `Test` | Campo `O` del subject. |
| `-OutputDirectory` | `.\certs` | Carpeta donde guardar los archivos. |

### Paso 2 — Subir el CSR a WSASS (manual)

1. Entrar a [https://wsass-homo.afip.gob.ar/wsass/](https://wsass-homo.afip.gob.ar/wsass/) con CUIT + Clave Fiscal.
2. En **"Nuevo Certificado"**, usar:
   - **Alias:** el `-CommonName` que pasaste al script (ej. `afip-sdk-poc`).
   - **CSR:** pegar el contenido completo del `.csr` (incluidas las líneas `BEGIN/END`).
3. Descargar el certificado firmado y guardarlo como `certs\<CN>.crt`.
4. En la sección **"Administración de Relaciones"** vincular el certificado a los servicios:
   - `wsfe` (facturación electrónica)
   - `sire-ws` (opcional, solo si vas a informar retenciones)

### Paso 3 — Ensamblar el `.pfx`

```powershell
.\New-AfipCertificate.ps1 -Mode Pfx -CommonName afip-sdk-poc -CrtPath .\certs\afip-sdk-poc.crt
```

El script pide la password dos veces, valida que coincidan, y genera `certs\afip-sdk-poc.pfx`. Ese es el archivo que consume el SDK:

```csharp
services.AddAfipSdk(opts =>
{
    opts.Environment = AfipEnvironment.Homologation;
    opts.Cuit = "20123456789";
    opts.UseLocalCertificateSigning(c =>
        c.FromFile(@"C:\certs\afip-sdk-poc.pfx", "<password>")); // ajustá la ruta a donde tengas tu .pfx
});
```

O directamente con la demo:

```powershell
cd ..\implementation
dotnet run --project Afip.Arca.Sdk.Demo
```

…y elegir **"Firma local con certificado X.509"** cuando lo pida.

### Seguridad

- `.gitignore` del repo ya excluye `*.key`, `*.pfx`, `*.p12` y `*.crt`.
- La `.key` queda **sin cifrar** en disco — protegé la carpeta o usá BitLocker/EFS sobre `certs\`.
- El `.pfx`, en cambio, **sí está cifrado** con la password que ingresaste.
- Para producción real: guardar el `.pfx` en Azure Key Vault / AWS Secrets Manager y usar `opts.UseExternalTicketProvider(...)` en lugar de cargarlo de disco.

### Errores comunes

| Mensaje | Causa | Solución |
|---|---|---|
| `Read-Host : Cannot find type [SecureString]` | PowerShell 5.1 | Correr en PowerShell 7+ (`pwsh`). |
| WSASS dice "CN ya existente" | Reutilizaste el alias | Cambiar `-CommonName` o eliminar el existente desde WSASS. |
| WSASS dice "CSR inválido" | Pegaste solo el blob base64 | Incluir las líneas `-----BEGIN/END CERTIFICATE REQUEST-----`. |
