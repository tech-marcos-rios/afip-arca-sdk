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

## 5. Más allá de este documento

| Necesitás | Mirá |
|---|---|
| Qué cambió en cada versión | [CHANGELOG.md](../CHANGELOG.md) |
| Cómo consumir el paquete publicado | [01-usage-guide.md](01-usage-guide.md) |
| Estado actual (validado/beta/pendiente) | [README.md](../README.md#estado-del-proyecto) |
