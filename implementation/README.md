# Afip.Arca.Sdk — Demo Interactivo

Solución de consola que muestra **cómo se consume el NuGet `Afip.Arca.Sdk`** desde una aplicación real. Pide la configuración mínima por consola y luego ofrece un menú interactivo con todas las operaciones que el SDK expone.

> ⚠️ Esta carpeta vive deliberadamente **fuera de la solución principal** (`../Afip.Arca.Sdk.sln`). El objetivo es replicar el flujo de un consumidor que descarga el paquete desde un feed NuGet — no que tenga acceso al código fuente.

---

## Cómo consume el NuGet

El proyecto incluye un [`NuGet.config`](NuGet.config) que define dos fuentes:

| Source | Origen |
|---|---|
| `local-artifacts` | `C:\GLB\artifacts` (donde `dotnet pack` deja el `.nupkg`) |
| `nuget.org` | feed público para las dependencias transitivas |

Y un **packageSourceMapping** que rutea `Afip.Arca.Sdk` exclusivamente al feed local — así no hay riesgo de que NuGet baje un homónimo de nuget.org.

La referencia en el `.csproj` es la habitual:

```xml
<PackageReference Include="Afip.Arca.Sdk" Version="1.0.0" />
```

---

## Ejecución

```powershell
# Desde la carpeta implementation/
dotnet restore
dotnet run --project Afip.Arca.Sdk.Demo
```

### Flujo

1. **Wizard inicial:**
   - Ambiente (Homologación / Producción).
   - CUIT del contribuyente.
   - Modo de autenticación WSAA:
     - **Firma local** con certificado X.509 (`.pfx` + contraseña).
     - **Provider externo** (modo demo offline — devuelve un TA simulado; útil para mostrar la API sin tener un cert).

2. **Menú principal:**

   | Opción | Operación | Pega a AFIP |
   |---|---|---|
   | 1 | Health check (FEDummy) | sí |
   | 2 | Emitir comprobante (Factura A/B/C/M, ND, NC) | sí |
   | 3 | Anular comprobante (NC asociada) | sí |
   | 4 | Consultar último número autorizado | sí |
   | 5 | Calcular retención de Ganancias (RG 830) | **no** (cálculo local) |
   | 6 | Emitir certificado SIRE | sí |
   | 7 | Consultar certificado SIRE | sí |
   | 8 | Anular certificado SIRE | sí |

---

## Requisitos para probar contra AFIP real

- Estar en **homologación** (el SDK la usa por defecto).
- Tener un **certificado X.509** generado en [WSASS](https://wsass-homo.afip.gob.ar/wsass/portal/main.aspx) para tu CUIT.
- Haber asociado el certificado a los servicios `wsfe` y `sire-ws` en WSASS.
- Tener un **punto de venta** dado de alta para tu CUIT (cualquiera ≥ 1 sirve en homologación).

Para los demos puramente locales (opción 5 — cálculo de Ganancias), nada de lo anterior hace falta: el algoritmo corre offline con la escala embebida (RG 5423, vigente desde 2024-10).

---

## Estructura

```
implementation/
├── NuGet.config
├── Afip.Arca.Sdk.Demo.sln
├── README.md
└── Afip.Arca.Sdk.Demo/
    ├── Afip.Arca.Sdk.Demo.csproj
    ├── Program.cs                    # Wizard inicial + menú principal
    ├── Configuration/
    │   └── SetupWizard.cs            # Pide ambiente, CUIT y modo de auth
    ├── Helpers/
    │   └── Prompt.cs                 # Lecturas de consola con validación
    └── Demos/
        ├── HealthDemo.cs             # FEDummy
        ├── InvoicingDemo.cs          # Emit / Cancel / LastNumber
        ├── IncomeTaxDemo.cs          # Cálculo RG 830
        └── SireDemo.cs               # Issue / Get / Cancel certificado
```
