# AfipNet — Contexto para Claude Code

Librería .NET para integración con los Web Services de AFIP/ARCA.
Objetivo: publicarla en NuGet.org como paquete open source.

## Stack

- .NET 8, C# 12
- Sin dependencias externas pesadas (solo abstracciones de Microsoft.Extensions)
- SOAP/XML para comunicación con AFIP (no hay REST en los WS de AFIP)
- xUnit + FluentAssertions + NSubstitute para tests

## Arquitectura

```
src/
├── AfipNet/                    ← librería principal (lo que se publica en NuGet)
│   ├── Authentication/         ← WSAA: autenticación con certificados
│   ├── Invoicing/              ← WSFE: facturación electrónica
│   ├── Models/                 ← DTOs de entrada y salida
│   │   ├── Auth/
│   │   └── Invoicing/
│   ├── Configuration/          ← AfipOptions (se configura desde appsettings)
│   └── Extensions/             ← AddAfipNet() para DI
└── AfipNet.Tests/              ← tests unitarios e integración
```

## Ambientes AFIP

| Ambiente | WSAA | WSFE |
|---|---|---|
| Homologación | wsaahomo.afip.gov.ar | wswhomo.afip.gov.ar |
| Producción | wsaa.afip.gov.ar | servicios1.afip.gov.ar |

Se controla con `AfipOptions.UsarHomologacion`. **Nunca** cambiar a producción sin aprobar con el usuario.

## Reglas de trabajo

- Los certificados (.p12) NUNCA van al repositorio. El .gitignore los excluye.
- Las contraseñas van en variables de entorno (`Afip__CertificadoPassword`), nunca en código.
- El SOAP de AFIP usa namespaces específicos — no simplificar sin verificar que funcione.
- Antes de cambiar la `<Version>` en AfipNet.csproj, confirmar con el usuario.
- Los métodos públicos deben tener `<summary>` en español (excepción a la regla de no comentarios).

## Comandos útiles

```bash
dotnet build                    # compilar
dotnet test                     # correr tests
dotnet pack -c Release          # generar .nupkg
/test-homologacion              # correr tests contra AFIP sandbox
/publish                        # publicar en NuGet.org (pide confirmación)
/review                         # revisar código activo
```

## Estado actual

- [x] WSAA — autenticación con certificados (.p12)
- [x] WSFE — solicitud de CAE
- [x] WSFE — último número de comprobante
- [x] WSFE — verificar disponibilidad (FEDummy)
- [x] DI con AddAfipNet()
- [ ] Caché persistente (IDistributedCache)
- [ ] WSFEX — facturación de exportaciones
- [ ] WSMTXCA — comprobantes con ítems detallados
- [ ] WSFECRED — Factura de Crédito Electrónica MiPyMEs
- [ ] CI/CD para publicación automática en NuGet
