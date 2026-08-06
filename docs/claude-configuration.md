# Configuración de Claude — Fundamentación

Este documento explica **por qué** la configuración de Claude en este repositorio (archivos en `.claude/`) está armada como está. Está pensado como pieza de portfolio: muestra criterio de ingeniería, no solo el "qué" sino el "para qué".

---

## 1. Archivos y su rol

| Archivo | Rol | Carga |
|---|---|---|
| [`.claude/CLAUDE.md`](../.claude/CLAUDE.md) | Constitución del repo. Define principios, patrones, estilo, testing, seguridad. | Automática al iniciar sesión en el directorio. |
| [`.claude/settings.local.json`](../.claude/settings.local.json) | Permisos (allowlist de tools) + hook `UserPromptSubmit` que re-inyecta los lineamientos en cada turno. | Automática. |

La separación es intencional:

- **`CLAUDE.md`** es **contenido** versionable, leído por el ser humano y por el modelo. Es el documento de "cómo trabajamos en este código".
- **`settings.local.json`** es **comportamiento** del harness — permisos, hooks, integración con la CLI. No documenta principios; los **fuerza**.

---

## 2. Por qué CLAUDE.md tiene este contenido y no otro

### Decisión: principios no negociables en la cabeza del documento

El modelo lee `CLAUDE.md` al comienzo de la sesión, pero cuando el contexto crece, las primeras secciones son las que mejor se retienen y las que el resumen automático preserva con mayor fidelidad. Por eso lo más crítico (Clean Architecture, SOLID, nullable, async, excepciones tipadas) está en las primeras secciones.

### Decisión: tabla de patrones obligatorios

Un listado en prosa es ignorable. Una tabla `Patrón → Dónde se aplica → Por qué` es:

- **Verificable** en code review: cualquier reviewer puede pedir el patrón donde corresponda.
- **Educativa** para el portfolio: muestra que las decisiones arquitectónicas tienen justificación, no son cargo cult.

### Decisión: "Flujo de trabajo de Claude" como sección final

La sección 11 le dice al modelo **cómo comportarse** ante una solicitud que rompa la guía: plantear el conflicto antes de implementar. Esto convierte a Claude en aliado defensivo de la arquitectura, no en un autómata que la erosiona silenciosamente.

### Decisión: idioma mixto controlado

- **Código y XMLDoc en inglés** → consistente con el ecosistema NuGet, descubrible globalmente.
- **Documentación funcional en español** → audiencia es Argentina (AFIP/ARCA).
- **Excepciones en inglés** → los mensajes terminan en logs/issue trackers; estandar en .NET.

Documentarlo explícitamente evita que el modelo "elija" arbitrariamente y oscile entre idiomas.

---

## 3. Por qué `settings.local.json` incluye un hook `UserPromptSubmit`

El hook ejecuta una línea de PowerShell que imprime un recordatorio compacto de los principios cada vez que el usuario manda un prompt. ¿Por qué?

1. **Re-anclaje contextual.** En sesiones largas, el `CLAUDE.md` inicial puede haber sido resumido. Re-inyectar las 4–5 reglas más importantes en cada turno garantiza que sigan activas sin gastar tokens en re-leer el archivo entero.
2. **Costo nulo de mantenimiento.** El hook vive en un único string; cambiarlo es una línea.
3. **Visibilidad.** El usuario ve la línea ejecutarse — sabe que las reglas siguen activas. Confianza > magia.

**Alternativa descartada:** un hook `PostToolUse` que valide cada Write/Edit contra reglas. Más invasivo, más caro de mantener, y no aporta valor incremental sobre tener un buen `CLAUDE.md` + revisión humana.

---

## 4. Por qué la lista de permisos es la que es

La allowlist (`permissions.allow`) está diseñada con el principio de **mínimo privilegio aplicado al desarrollo**:

- **Permitido sin prompt:** comandos `dotnet` (build, test, restore, pack, new), navegación (`dir`/`ls`), `git status`/`add`. Son reversibles o de solo lectura.
- **Solicitan confirmación:** `git commit`, `git push`, `Remove-Item`, edición de archivos fuera del repo. Son potencialmente destructivos o públicos.
- **WebFetch limitado a dominios oficiales** (`afip.gob.ar`, `github.com`, `sistemasagiles.com.ar`) — evita fugar contexto del repo a sitios arbitrarios.

Resultado: el ciclo de desarrollo normal no necesita aprobar permisos turno a turno, pero las acciones con blast radius (push, delete) siempre piden confirmación.

---

## 5. Decisiones que conscientemente **no** se tomaron

| Decisión rechazada | Por qué |
|---|---|
| Bloquear escritura fuera de `src/` y `docs/` | Demasiado rígido — los hooks de Husky, `.editorconfig`, `Directory.Build.props` viven en la raíz. Sería contraproducente. |
| Pre-commit hook que ejecute `dotnet format` antes de cada Edit | Más latencia por edición. Mejor configurar en el `.csproj` (`<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`) y dejar que el compilador falle. |
| Subagentes especializados (`afip-reviewer`, `nuget-publisher`) | Para este alcance, overhead innecesario. Si el repo crece y aparecen tareas recurrentes (revisar nuevas RG, publicar versión), entonces sí. |
| Settings globales en `~/.claude/settings.json` | Las reglas son del **proyecto**, no del usuario. Vivir en el repo permite que el próximo desarrollador clone y herede el comportamiento. |

---

## 6. Cómo verificar que la configuración funciona

```powershell
# 1. CLAUDE.md está siendo leído
#    Pedile a Claude: "¿Cuál es la regla de logging en este repo?"
#    Debe responder citando la sección 6 sin que se la indiquemos.

# 2. El hook UserPromptSubmit dispara
#    En el primer turno verás la línea "GUIDELINES: ..." impresa.

# 3. Los permisos están en efecto
#    Pedí "ejecutá dotnet build" → no pide permiso.
#    Pedí "ejecutá rm -rf docs" → pide permiso explícito.
```

---

## 7. Relación con otros documentos

- [`docs/architecture.md`](architecture.md) → arquitectura del NuGet en sí.
- [`docs/afip-api-technical-summary.md`](afip-api-technical-summary.md) → resumen técnico de los WS de AFIP.
- [`docs/portfolio-summary.md`](portfolio-summary.md) → resumen ejecutivo del proyecto.
- [`.claude/CLAUDE.md`](../.claude/CLAUDE.md) → reglas del repo (este documento explica el porqué).

> **Regla práctica:** si alguien pregunta *"¿por qué hicimos X así?"* la respuesta debería estar acá o en uno de los `docs/`. Si no está, falta documentación.
