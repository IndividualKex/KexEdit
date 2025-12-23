# Runtime Context

Core runtime package with hexagonal-lite architecture for track computation, physics, and rendering.

## Purpose

- High-performance track building and simulation using Unity DOTS
- Hexagonal-lite architecture: Hex Cores → Hex Layers → Application → Infrastructure
- Portable cores designed for Rust/WASM migration

## Layout

```
Runtime/
├── Sim/              # Hex 1: Coaster Simulation
│   ├── Core/         # (KexEdit.Sim) Pure physics/math, no dependencies
│   ├── Schema/       # (KexEdit.Sim.Schema) Node types, ports, properties
│   └── Nodes/        # (KexEdit.Sim.Nodes.*) Node implementations
├── Graph/            # Hex 2: Graph System
│   ├── Core/         # (KexEdit.Graph) Generic graph structure
│   └── Typed/        # (KexEdit.Graph.Typed) Type-safe operations
├── Spline/           # Hex 3: Spline System
│   ├── Core/         # (KexEdit.Spline) Spline math and interpolation
│   └── Resampling/   # (KexEdit.Spline.Resampling) Point → SplinePoint
├── App/              # Application Layer
│   ├── Coaster/      # (KexEdit.App.Coaster) Aggregate root + evaluator
│   └── Persistence/  # (KexEdit.App.Persistence) Save/load
├── Legacy/           # Infrastructure Layer
│   ├── (main)        # (KexEdit.Legacy) ECS systems
│   ├── Debug/        # (KexEdit.Legacy.Debug) Gizmo visualization
│   └── Editor/       # (KexEdit.Legacy.Editor) Editor tools
├── Native/           # FFI bindings (KexEdit.Native.RustCore)
├── Shaders/          # Compute and rendering shaders
└── Resources/        # Runtime assets
```

## Assembly Dependencies

```
Legacy (Infrastructure) ──► App ──► Hex Layers ──► Hex Cores
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   Sim.Schema            Graph.Typed           Spline.Resampling
   Sim.Nodes.*               │                     │
        │             ┌──────┴──────┐              │
        ▼             ▼             ▼              ▼
    Sim (Core)    Graph (Core)  Sim.Schema    Spline (Core)
```

## Entrypoints

- `Legacy/Core/KexEditManager.cs` - Main runtime initialization
- `Legacy/Track/Track.cs` - Core track data structure
- `Legacy/Persistence/CoasterLoader.cs` - Track file loading

## Dependencies

- Unity.Entities, Unity.Physics, Unity.Mathematics, Unity.Burst, Unity.Collections
