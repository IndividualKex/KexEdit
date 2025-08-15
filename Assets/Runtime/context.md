# Runtime Context

Core runtime package for KexEdit containing all track computation, physics, and rendering logic

## Purpose

- Provides high-performance track building and simulation using Unity DOTS
- Contains ECS components, systems, and authoring tools
- Manages track serialization and compute shader operations

## Layout

```
Runtime/
├── context.md  # This file, folder context (Tier 2)
├── package.json  # Unity package definition
├── Scripts/  # Main source code
│   ├── context.md  # Scripts context
│   ├── Authoring/  # Track authoring components
│   ├── Components/  # ECS components
│   ├── Systems/  # ECS systems
│   ├── Serialization/  # Save/load
│   ├── Utils/  # Utilities
│   └── KexEditManager.cs  # Main manager
├── Shaders/  # Compute and rendering shaders
└── Resources/  # Runtime assets
    └── FallbackRail.asset  # Default rail asset
```

## Scope

- In-scope: Track computation, physics simulation, mesh generation, ECS architecture
- Out-of-scope: UI implementation, editor tools, user preferences

## Entrypoints

- `KexEditManager.cs` - Main runtime initialization
- `Track.cs` - Core track data structure
- `CoasterLoader.cs` - Track file loading system

## Dependencies

- Unity.Entities 1.3.14 - ECS framework
- Unity.Physics - Physics simulation
- Unity.Mathematics - Math library
- Unity.Burst - High-performance compiler
- Unity.Collections - Native collections