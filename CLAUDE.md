# KexEdit

Unity roller coaster editor using Force Vector Design (FVD) with DOTS/ECS.

**Before any task**: Read [layers/structure.md](layers/structure.md)

## Architecture: Functional Core, ECS Shell

Data-oriented layered design. Pure cores transform data; ECS orchestrates effects.

| Layer | Role | Examples |
|-------|------|----------|
| **Hex Cores** | Pure, portable transforms | Sim, Graph, Spline |
| **Hex Layers** | Domain-aware extensions | Schema, Nodes, Typed |
| **Application** | Unity integration | Legacy/ |

**Dependency rule**: Inward only. Cores never call outward—they receive data, return data.

**No interfaces/adapters**: Use data contracts. The shell handles IO; cores stay pure.

## Context Tiers

0. `CLAUDE.md` — Global standards
1. `layers/structure.md` — Project map, entry points
2. `context.md` (per folder) — Local purpose/structure
3. Code files

**Always load context before working.**

## Commands

- **Test**: `./run-tests.sh`
- **Debug**: Python scripts in `tools/`

## Code Rules

- **No history in code/docs** — Write current state only. Never "changed from X" or "previously Y"
- **Simple > clever** — Minimal code for current requirements
- **Single responsibility** — Small files, separated concerns
- **Reuse first** — Check existing code before adding new
- **Self-documenting** — Comments only when logic isn't obvious
- **Single source of truth** — No duplicate state; derive when needed
- **Fail fast** — Expose bugs immediately; avoid defensive complexity

## Security

- Validate external inputs only (user input, APIs)
- Secrets in env vars only
- Never log sensitive data
