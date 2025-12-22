# KexEdit

Unity roller coaster editor using Force Vector Design (FVD) with DOTS/ECS.

## Architecture

Nested hexagonal: Pure cores compose into Track Backend, wrapped by domain layers, then Application (Unity ECS) and UI.

```
┌─────────────────────────────────────────────────────────────────────┐
│  UI — editor state, undo/redo, viewport, input                      │
├─────────────────────────────────────────────────────────────────────┤
│  APPLICATION — Legacy (ECS), Rendering (GPU shell)                  │
├─────────────────────────────────────────────────────────────────────┤
│  DOMAIN LAYERS — Trains                                             │
├─────────────────────────────────────────────────────────────────────┤
│  TRACK BACKEND ─────────────────────────────────────────────────    │
│  │ Document, Track, Persistence                                     │
│  │ Sim.Schema, Sim.Nodes.*, Graph.Typed, Spline.Resampling/Rendering│
│  │ Sim (FVD) · Graph (DAG) · Spline (arc-length)  ← Hex Cores       │
└─────────────────────────────────────────────────────────────────────┘
```

**Dependency rule**: Inward only. Cores receive data, return data—no outward calls.

| Layer | Role |
|-------|------|
| **Hex Cores** | Pure physics/math: `Sim/Core`, `Graph/Core`, `Spline/Core` |
| **Hex Layers** | Domain extensions: Schema, Nodes, Typed, Resampling |
| **Track Backend** | Document, Track, Persistence |
| **Domain** | Trains (traversal, car positioning) |
| **Application** | Legacy (ECS), Rendering (GPU) |
| **UI** | Editor state, undo/redo, viewport |

## Stack

- **Runtime**: Unity 6000.3.1f1, Rust (parallel backend)
- **Framework**: Unity DOTS (ECS, Burst, Jobs)
- **UI**: UI Toolkit

## Commands

```bash
./run-tests.sh [TestName]              # Headless tests (Burst backend)
./run-tests.sh --rust-backend [TestName]  # Headless tests (Rust FFI)
cargo test -p kexengine                # Rust tests
./build-rust.sh                        # Build FFI DLL
```

**Test Results**: After running tests, check `test-results.xml` (summary) and `test-log.txt` (details) instead of re-running. Do not re-run tests immediately after running them.

## Entry Points

| Entry | Path |
|-------|------|
| Scene | `Assets/Scenes/Main.unity` |
| Runtime | `Assets/Runtime/Legacy/Core/KexEditManager.cs` |
| UI | `Assets/Scripts/UI/UIManager.cs` |
| Document | `Assets/Runtime/Document/Document.cs` |

## Code Rules

- **No history in code/docs** — Write current state only
- **Simple > clever** — Minimal code for current requirements
- **Single responsibility** — Small files, separated concerns
- **Reuse first** — Check existing code before adding new
- **Self-documenting** — Comments only when logic isn't obvious
- **Single source of truth** — No duplicate state
- **Fail fast** — Expose bugs immediately

## Migration Status

**kexengine (submodule: `kexengine/`)** — Standalone library, ready for external use.

| Component | Status |
|-----------|--------|
| Sim core (FVD physics) | Rust parity |
| Graph core (DAG) | Rust parity |
| All 8 node types | Rust parity |
| Track building | Rust parity |
| Spline generation | Rust parity |

**Pending Migration:**
- Trains (traversal, car positioning)
- Persistence (full .kex format)
- Rendering

**Legacy Code (`Assets/Runtime/Legacy/`):**
Unity ECS wrapper code. Gradually being superseded by kexengine via FFI.

## Security

- Validate external inputs only
- Secrets in env vars only
- Never log sensitive data
