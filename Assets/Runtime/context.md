# Runtime Context

Core runtime package with hexagonal architecture for track computation, physics, and rendering.

## Purpose

- High-performance track building and simulation using Unity DOTS
- Hexagonal architecture: Core → Nodes → Adapters → Legacy ECS
- Portable core designed for Rust/WASM migration

## Layout

```
Runtime/
├── Core/       # (KexEdit.Core) Pure physics/math, no dependencies
├── Nodes/      # (KexEdit.Nodes.*) Node type implementations
├── Legacy/     # (KexEdit) Monolithic runtime being hollowed out
├── Shaders/    # Compute and rendering shaders
└── Resources/  # Runtime assets
```

## Assembly Dependencies

```
Legacy (KexEdit) ──► Nodes.* ──► Nodes ──► Core
                     (node types cannot depend on each other)
```

## Entrypoints

- `Legacy/Core/KexEditManager.cs` - Main runtime initialization
- `Legacy/Track/Track.cs` - Core track data structure
- `Legacy/Persistence/CoasterLoader.cs` - Track file loading

## Dependencies

- Unity.Entities, Unity.Physics, Unity.Mathematics, Unity.Burst, Unity.Collections
