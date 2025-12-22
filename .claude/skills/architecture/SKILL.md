# Architecture

Understand the nested hexagonal architecture of KexEdit.

## When to Use

- When navigating the codebase
- Before making changes to understand impact
- When deciding where new code should live

## Layer Structure

```
UI → Application → Domain → Track Backend → Hex Cores
```

Dependencies flow inward only. Each layer can only call layers below it.

## Key Directories

| Layer | Path | Contents |
|-------|------|----------|
| Hex Cores | `Assets/Runtime/Sim/Core/`, `Graph/Core/`, `Spline/Core/` | Pure math/physics |
| Hex Layers | `Assets/Runtime/Sim/Nodes/`, `Graph/Typed/`, etc. | Domain extensions |
| Track Backend | `Assets/Runtime/Track/`, `Document/`, `Persistence/` | Orchestration |
| Legacy | `Assets/Runtime/Legacy/` | Unity ECS wrappers (being replaced) |
| UI | `Assets/Scripts/UI/` | Editor interface |

## Rust Backend

The `rust-backend/` submodule mirrors the C# hex cores:
- `kexedit-sim` = `Sim/Core`
- `kexedit-graph` = `Graph/Core`
- `kexedit-sim-nodes` = `Sim/Nodes`
- `kexedit-track` = `Track`

## Adding New Code

1. Pure math/physics → Hex Core (Rust preferred)
2. Node types → `Sim/Nodes` or `kexedit-sim-nodes`
3. Track orchestration → `Track/` or `kexedit-track`
4. Unity-specific → `Legacy/` (temporary) or proper domain layer
