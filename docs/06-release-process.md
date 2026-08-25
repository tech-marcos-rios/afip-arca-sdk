# Proceso de release

> Cómo se corta y publica una versión nueva de `Afip.Arca.Sdk` en nuget.org.

---

## 1. Resumen

La publicación es **disparada por un tag `vX.Y.Z`** en GitHub, no manual desde una terminal.
El workflow [`.github/workflows/publish.yml`](../.github/workflows/publish.yml) corre los tests,
empaqueta la librería y publica a nuget.org usando **Trusted Publishing** (OIDC) — no hay ninguna
API key de nuget.org guardada como secret en ningún lado del repo ni de GitHub.

Antes de publicar de verdad, el workflow **se pausa esperando aprobación manual** en la pestaña
*Actions* de GitHub (Environment `release` con "Required reviewers"). Publicar en nuget.org es una
acción pública y no trivialmente reversible (se puede "unlist" una versión, pero no borrarla), así
que ese gate es intencional — no lo saltees.

---

## 2. Pasos para cortar un release

1. **Actualizar `CHANGELOG.md`**: mover lo que corresponda de `[Unreleased]` a una nueva sección
   `## [X.Y.Z] — YYYY-MM-DD`, siguiendo [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).
2. **Bumpear la versión** en [`Directory.Build.props`](../Directory.Build.props)
   (`VersionPrefix`, `FileVersion`, `AssemblyVersion`, `InformationalVersion`) según SemVer:
   breaking → major, feature compatible → minor, fix → patch.
3. **Actualizar el badge de versión** en el [README](../README.md) si corresponde.
4. Commitear esos cambios normalmente (no hace falta nada especial).
5. **Tagear y pushear**:

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

6. Ir a la pestaña **Actions** del repo en GitHub, abrir el run que se disparó, y **aprobarlo**
   cuando llegue al paso que espera el Environment `release`.
7. Una vez aprobado, el workflow publica a nuget.org. Tarda unos minutos en indexar
   (`nuget.org/packages/Afip.Arca.Sdk`).
8. Opcional: crear el Release en GitHub desde ese tag, pegando la entrada correspondiente del
   `CHANGELOG.md` como descripción.

---

## 3. Salvaguarda automática: el tag debe coincidir con la versión

El workflow tiene un paso que compara el tag pusheado (`vX.Y.Z` → `X.Y.Z`) contra
`Directory.Build.props`'s `VersionPrefix`, y **falla antes de compilar nada** si no coinciden. Esto
existe para atrapar el error común de tagear sin haber bumpeado la versión (o al revés).

---

## 4. Configuración de Trusted Publishing (ya hecha, referencia)

Esto es setup de una sola vez, ya completado para este repo — documentado acá por si hace falta
recrearlo (ej. si se migra a otra cuenta/org de nuget.org):

- **nuget.org → Trusted Publishing → Create**, con:
  - Package Owner: la cuenta dueña del paquete.
  - CI/CD Provider: `GitHub Actions`.
  - Repository Owner: `tech-marcos-rios`.
  - Repository: `afip-arca-sdk`.
  - Workflow File: `publish.yml` (nombre del archivo, sin la ruta `.github/workflows/`).
  - Environment: `release`.
  - Scopes: `Push` → "Push new packages and package versions" (necesario para el primer publish de
    un paquete que todavía no existe en nuget.org; se puede restringir a "Push only new package
    versions" en una política separada una vez publicada la primera versión).
  - Glob Patterns and Packages: `Afip.Arca.Sdk` (match exacto, no wildcard — limita la política a
    este paquete puntual).
- **GitHub → Settings → Environments → `release`**, con:
  - Required reviewers: quien deba aprobar publicaciones.
  - "Allow administrators to bypass configured protection rules" **destildado** — si no, el gate
    no frena a los admins del repo.
  - Deployment branches and tags → "Selected branches and tags" → patrón `v*` (defensa en
    profundidad; el trigger del workflow ya filtra por tag, esto es una segunda capa a nivel
    GitHub).

Referencia oficial: [Trusted Publishing on nuget.org — Microsoft Learn](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

---

## 5. Troubleshooting — problemas reales encontrados en el release de 1.0.0

### `dotnet test` falla en CI con errores `CA1873` que no aparecen en local

**Síntoma:** el paso `Test` del workflow falla con varios `error CA1873: Evaluation of this
argument may be expensive and unnecessary if logging is disabled`, apuntando a llamadas
`_logger.LogInformation(...)`/`LogWarning(...)` normales — pero localmente `dotnet build`/
`dotnet test` compilan sin ningún warning.

**Causa:** `Directory.Build.props` tiene `<AnalysisLevel>latest-recommended</AnalysisLevel>`, que
ata el set de reglas de análisis a la versión del SDK de .NET instalada — no es un número fijo.
El runner de GitHub Actions (`actions/setup-dotnet@v4` con `dotnet-version: 8.0.x`) puede resolver
una versión de SDK distinta a la que tenés localmente, y esa versión puede traer reglas nuevas
"recomendadas" que tu SDK local todavía no tiene (o viceversa). No es reproducible 1:1 entre tu
máquina y CI mientras se use `latest-recommended`.

**Solución aplicada:** se agregó `CA1873` al `<NoWarn>` de `Directory.Build.props`, con el mismo
criterio ya documentado para `CA1848` — son logs esporádicos (renovación de TA, autorización de
comprobante, reintentos), no hot-path, así que evitar la evaluación de argumentos no aporta nada.

**Si vuelve a pasar con otra regla nueva:** mismo patrón — evaluar si la regla aplica de verdad al
código del SDK, y si no, sumarla al `NoWarn` con un comentario explicando por qué (no suprimir a
ciegas). Alternativa de fondo si esto se vuelve recurrente: fijar `AnalysisLevel` a un número
concreto (ej. `8.0-recommended`) en vez de `latest-recommended`, para que CI y local usen siempre
el mismo set de reglas sin importar qué SDK patch tengan instalado.

### `NuGet/login@v1` falla con `HTTP 401` / "No matching trust policy owned by user..."

**Síntoma:** el paso de login OIDC falla con:
```
Token exchange failed (HTTP 401)... Make sure you are using the username of the policy creator,
not the policy owner: No matching trust policy owned by user 'X' was found.
```

**Causa:** el input `user:` de `NuGet/login@v1` tiene que ser tu **usuario de nuget.org**
(ej. `marcos.rios`), no el owner/organización del repositorio de GitHub (`tech-marcos-rios`). Son
dos campos distintos de la misma política de Trusted Publishing — es fácil confundirlos porque
"Repository Owner" sí es el de GitHub, pero `user:` en el workflow es el de nuget.org.

**Solución:** usar el nombre de cuenta que aparece en el dropdown **"Package Owner"** del
formulario de la política en nuget.org, no el que pusiste en "Repository Owner".

---

## 6. Más allá de este documento

| Necesitás | Mirá |
|---|---|
| Qué cambió en cada versión | [CHANGELOG.md](../CHANGELOG.md) |
| Cómo consumir el paquete publicado | [01-usage-guide.md](01-usage-guide.md) |
| Estado actual (validado/beta/pendiente) | [README.md](../README.md#estado-del-proyecto) |
