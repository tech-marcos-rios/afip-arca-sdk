#Requires -Version 7.0
<#
.SYNOPSIS
    Genera el material criptográfico necesario para autenticarse contra los Web
    Services de AFIP/ARCA: en la primera fase emite la clave privada y el CSR
    para subir a WSASS; en la segunda fase ensambla el .pfx que consume el SDK.

.DESCRIPTION
    Flujo completo:
      1. (modo Csr)  → produce <CN>.key  + <CN>.csr
      2. (manual)    → subir <CN>.csr a https://wsass-homo.afip.gob.ar/wsass/
                       y descargar el certificado firmado (<CN>.crt)
      3. (modo Pfx)  → produce <CN>.pfx  combinando <CN>.key + <CN>.crt
      4. configurar el SDK con  opts.UseLocalCertificateSigning(c =>
                                  c.FromFile("<CN>.pfx", "<password>"));

    El script usa únicamente APIs nativas de .NET — no requiere OpenSSL.

.PARAMETER Mode
    Csr  : genera clave privada + CSR.
    Pfx  : combina la clave privada con el certificado firmado en un .pfx.

.PARAMETER CommonName
    CN del certificado (alias lógico de la aplicación). Sugerencia: usar guiones
    bajos o minúsculas, sin acentos. Ej.: afip-sdk-poc

.PARAMETER Cuit
    CUIT de 11 dígitos del contribuyente que va a operar con los WS de AFIP.

.PARAMETER Organization
    Nombre de la organización (campo O del subject del certificado). Default:
    "Test".

.PARAMETER OutputDirectory
    Carpeta donde se guardan los archivos. Default: ./certs (relativo al cwd).

.PARAMETER CrtPath
    Solo modo Pfx: ruta al .crt descargado de WSASS.

.PARAMETER KeyPath
    Solo modo Pfx: ruta al .key generado en la fase 1. Default: <OutputDirectory>/<CommonName>.key

.EXAMPLE
    # Paso 1 — generar CSR
    .\New-AfipCertificate.ps1 -Mode Csr -CommonName afip-sdk-poc -Cuit 20123456789

.EXAMPLE
    # Paso 3 — ensamblar PFX (luego de bajar el .crt firmado de WSASS)
    .\New-AfipCertificate.ps1 -Mode Pfx -CommonName afip-sdk-poc -CrtPath .\certs\afip-sdk-poc.crt

.NOTES
    Los archivos resultantes (.key y .pfx) son secretos: el .gitignore del repo
    ya excluye *.key, *.pfx y *.p12. No commitearlos jamás.
#>
[CmdletBinding(DefaultParameterSetName = 'Csr')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Csr')]
    [Parameter(Mandatory, ParameterSetName = 'Pfx')]
    [ValidateSet('Csr', 'Pfx')]
    [string]$Mode,

    [Parameter(Mandatory, ParameterSetName = 'Csr')]
    [Parameter(Mandatory, ParameterSetName = 'Pfx')]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{1,62}$')]
    [string]$CommonName,

    [Parameter(Mandatory, ParameterSetName = 'Csr')]
    [ValidatePattern('^\d{11}$')]
    [string]$Cuit,

    [Parameter(ParameterSetName = 'Csr')]
    [string]$Organization = 'Test',

    [Parameter(ParameterSetName = 'Csr')]
    [Parameter(ParameterSetName = 'Pfx')]
    [string]$OutputDirectory = (Join-Path (Get-Location) 'certs'),

    [Parameter(Mandatory, ParameterSetName = 'Pfx')]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string]$CrtPath,

    [Parameter(ParameterSetName = 'Pfx')]
    [string]$KeyPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host '▶ ' -ForegroundColor Cyan -NoNewline
    Write-Host $Message -ForegroundColor White
}

function Write-Ok {
    param([string]$Message)
    Write-Host '  ✔ ' -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Write-Warn {
    param([string]$Message)
    Write-Host '  ⚠ ' -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Write-Info {
    param([string]$Message)
    Write-Host '  ℹ ' -ForegroundColor DarkCyan -NoNewline
    Write-Host $Message
}

function Format-Pem {
    param(
        [byte[]]$Bytes,
        [string]$Label
    )
    $b64 = [Convert]::ToBase64String($Bytes, [Base64FormattingOptions]::InsertLineBreaks)
    return @(
        "-----BEGIN $Label-----"
        $b64
        "-----END $Label-----"
    ) -join "`n"
}

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

# Normalizar rutas a absolutas: PowerShell honra $PWD pero las APIs nativas
# de .NET usan Environment.CurrentDirectory, que NO se actualiza con `cd`.
# Sin esto, pasar `-CrtPath .\certs\foo.crt` rompe en CreateFromPemFile.
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
if ($PSCmdlet.ParameterSetName -eq 'Pfx') {
    $CrtPath = (Resolve-Path -LiteralPath $CrtPath).Path
    if ($KeyPath) {
        $KeyPath = (Resolve-Path -LiteralPath $KeyPath).Path
    }
}

# ---------------------------------------------------------------------------
# MODE: Csr
# ---------------------------------------------------------------------------
if ($Mode -eq 'Csr') {
    $keyPath = Join-Path $OutputDirectory "$CommonName.key"
    $csrPath = Join-Path $OutputDirectory "$CommonName.csr"

    if (Test-Path -LiteralPath $keyPath) {
        Write-Warn "Ya existe '$keyPath'."
        $confirm = Read-Host "¿Sobrescribir? Esto invalida cualquier CSR/PFX previo (s/N)"
        if ($confirm -notmatch '^[sSyY]') {
            Write-Host 'Cancelado.' -ForegroundColor Yellow
            return
        }
    }

    Write-Step "Generando clave privada RSA-2048 para CN=$CommonName"
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)

    Write-Step 'Armando subject del certificado'
    # Formato esperado por WSASS — el campo serialNumber lleva 'CUIT <cuit>'.
    $subjectText = "CN=$CommonName, serialNumber=CUIT $Cuit, O=$Organization, C=AR"
    $subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($subjectText)
    Write-Info "Subject: $subjectText"

    Write-Step 'Creando CertificateRequest (CSR) firmado con SHA-256'
    $req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $subject,
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    $csrBytes = $req.CreateSigningRequest()
    $csrPem = Format-Pem -Bytes $csrBytes -Label 'CERTIFICATE REQUEST'

    Write-Step 'Exportando clave privada en formato PKCS#8 (PEM no cifrado)'
    Write-Warn 'La clave privada queda EN CLARO en disco. Protegé la carpeta.'
    $keyBytes = $rsa.ExportPkcs8PrivateKey()
    $keyPem = Format-Pem -Bytes $keyBytes -Label 'PRIVATE KEY'

    [System.IO.File]::WriteAllText($keyPath, $keyPem)
    [System.IO.File]::WriteAllText($csrPath, $csrPem)

    Write-Ok "Clave privada → $keyPath"
    Write-Ok "CSR           → $csrPath"

    Write-Host ''
    Write-Host '═══════════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
    Write-Host '  PRÓXIMOS PASOS (manuales en WSASS)' -ForegroundColor White
    Write-Host '═══════════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  1. Entrá a:  https://wsass-homo.afip.gob.ar/wsass/' -ForegroundColor White
    Write-Host '     con tu CUIT + Clave Fiscal.'
    Write-Host ''
    Write-Host '  2. Sección "Nuevo Certificado":' -ForegroundColor White
    Write-Host "       - Alias:  $CommonName"
    Write-Host '       - Pegá el contenido COMPLETO de:'
    Write-Host "             $csrPath" -ForegroundColor Yellow
    Write-Host '         (incluyendo las líneas BEGIN/END CERTIFICATE REQUEST).'
    Write-Host ''
    Write-Host '  3. Descargá el .crt que devuelve y guardalo como:' -ForegroundColor White
    Write-Host "       $OutputDirectory\$CommonName.crt" -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  4. Vinculá ese certificado a los servicios que vas a usar' -ForegroundColor White
    Write-Host '     (mínimo: "wsfe" para facturación; "sire-ws" si vas a informar retenciones).'
    Write-Host ''
    Write-Host '  5. Volvé acá y corré:' -ForegroundColor White
    Write-Host "       .\New-AfipCertificate.ps1 -Mode Pfx -CommonName $CommonName -CrtPath $OutputDirectory\$CommonName.crt" -ForegroundColor Green
    Write-Host ''
    return
}

# ---------------------------------------------------------------------------
# MODE: Pfx
# ---------------------------------------------------------------------------
if ($Mode -eq 'Pfx') {
    if (-not $KeyPath) {
        $KeyPath = Join-Path $OutputDirectory "$CommonName.key"
    }
    if (-not (Test-Path -LiteralPath $KeyPath)) {
        throw "No se encontró la clave privada en '$KeyPath'. Especificá -KeyPath."
    }

    $pfxPath = Join-Path $OutputDirectory "$CommonName.pfx"
    if (Test-Path -LiteralPath $pfxPath) {
        Write-Warn "Ya existe '$pfxPath'."
        $confirm = Read-Host '¿Sobrescribir? (s/N)'
        if ($confirm -notmatch '^[sSyY]') {
            Write-Host 'Cancelado.' -ForegroundColor Yellow
            return
        }
    }

    Write-Step 'Pidiendo password para proteger el .pfx'
    Write-Info 'Tip: usá una password fuerte; el SDK la necesita en runtime.'
    $securePwd = Read-Host -AsSecureString 'Password del .pfx'
    if ($securePwd.Length -eq 0) {
        throw 'El password no puede ser vacío.'
    }
    $confirmPwd = Read-Host -AsSecureString 'Repetí el password'
    $pwd1 = [System.Net.NetworkCredential]::new('', $securePwd).Password
    $pwd2 = [System.Net.NetworkCredential]::new('', $confirmPwd).Password
    if ($pwd1 -ne $pwd2) {
        throw 'Los passwords no coinciden.'
    }

    Write-Step "Cargando clave privada: $KeyPath"
    $rsa = [System.Security.Cryptography.RSA]::Create()
    $rsa.ImportFromPem((Get-Content -LiteralPath $KeyPath -Raw))

    Write-Step "Cargando certificado firmado: $CrtPath"
    # Parseo manual PEM → DER → X509Certificate2.
    # CreateFromPemFile a veces rechaza PEM válidos generados por WSASS por una
    # interacción rara con el detector de PEM dentro de .NET en PowerShell;
    # decodificar el base64 a mano y usar el ctor desde bytes DER es robusto.
    $pemLines = Get-Content -LiteralPath $CrtPath
    $b64 = ($pemLines | Where-Object { $_ -notmatch '^-----' }) -join ''
    $b64 = $b64 -replace '\s', ''
    $der = [Convert]::FromBase64String($b64)
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($der)

    Write-Step 'Combinando certificado + clave privada en un PKCS#12 (.pfx)'
    # En PowerShell la resolución de overloads elige CopyWithPrivateKey(ECDiffieHellman)
    # cuando uno espera el de RSA. Forzamos el extension method correcto.
    $certWithKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($cert, $rsa)
    $pfxBytes = $certWithKey.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $pwd1)

    [System.IO.File]::WriteAllBytes($pfxPath, $pfxBytes)

    Write-Ok "Archivo generado → $pfxPath"
    Write-Info "Subject: $($cert.Subject)"
    Write-Info "Issuer:  $($cert.Issuer)"
    Write-Info "Vigencia: $($cert.NotBefore.ToString('yyyy-MM-dd')) → $($cert.NotAfter.ToString('yyyy-MM-dd'))"

    Write-Host ''
    Write-Host '═══════════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
    Write-Host '  CÓMO USARLO CON EL SDK' -ForegroundColor White
    Write-Host '═══════════════════════════════════════════════════════════════════' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host 'En tu wizard o en código:' -ForegroundColor White
    Write-Host ''
    Write-Host '    services.AddAfipSdk(opts =>' -ForegroundColor Green
    Write-Host '    {' -ForegroundColor Green
    Write-Host '        opts.Environment = AfipEnvironment.Homologation;' -ForegroundColor Green
    Write-Host "        opts.Cuit = `"<tu CUIT>`";" -ForegroundColor Green
    Write-Host '        opts.UseLocalCertificateSigning(c =>' -ForegroundColor Green
    Write-Host "            c.FromFile(@`"$pfxPath`", `"<password>`"));" -ForegroundColor Green
    Write-Host '    });' -ForegroundColor Green
    Write-Host ''
    Write-Host 'O directamente correr la demo y elegir "Firma local" cuando lo pida:' -ForegroundColor White
    Write-Host ''
    Write-Host '    cd D:\Code\projects\03-afip-net\implementation' -ForegroundColor Green
    Write-Host '    dotnet run --project Afip.Arca.Sdk.Demo' -ForegroundColor Green
    Write-Host ''
    return
}
