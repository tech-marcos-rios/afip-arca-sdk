# Contribuir a Afip.Arca.Sdk

## Flujo de ramas (GitHub Flow)

`master` está protegida — nadie pushea directo ahí, todo entra vía Pull Request.

1. Crear una rama desde `master`:
   - `feature/<algo>` para funcionalidad nueva.
   - `fix/<algo>` para bugs.
   - `docs/<algo>` para documentación.
2. Commitear en formato [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`,
   `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, `ci:`), mensaje en inglés, imperativo.
3. Abrir un Pull Request contra `master`.
4. El CI (`.github/workflows/ci.yml`) corre build + tests automáticamente — tiene que estar en
   verde para poder mergear.
5. Mergear el PR (squash o merge commit, lo que prefieras). No hace falta aprobación de otra
   persona para mergear tu propio PR.

## Antes de abrir un PR

- Leé [`.claude/CLAUDE.md`](.claude/CLAUDE.md) — son los lineamientos obligatorios del repo
  (Clean Architecture, SOLID, nullable, async, excepciones tipadas, testing).
- Toda clase pública nueva necesita XMLDoc + tests + entrada en `CHANGELOG.md` bajo `[Unreleased]`.
- `dotnet test Afip.Arca.Sdk.sln` tiene que pasar localmente antes de pushear.

## Cortar un release

Eso es un proceso aparte, documentado en [`docs/06-release-process.md`](docs/06-release-process.md)
— normalmente lo hace quien mantiene el paquete, no parte del flujo de un PR común.
