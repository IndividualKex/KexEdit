# Runtime Scripts Context

Core ECS-based track computation and simulation systems, organized by feature responsibility

## Purpose

- Implements Unity DOTS architecture for high-performance track processing
- Organized into 8 feature areas for better context and maintainability
- Manages track construction, physics, rendering, and persistence

## Layout

```
Scripts/
├── context.md  # This file, folder context (Tier 2)
├── Core/  # Foundation & shared infrastructure
│   ├── Components/  # Core components
│   ├── Systems/  # Initialization & cleanup
│   ├── KexEditManager.cs  # Main runtime manager
│   └── context.md
├── Track/  # Track construction & graph
│   ├── Components/  # Nodes, connections, ports
│   ├── Systems/  # Graph & building systems
│   ├── Authoring/  # Section aspects
│   ├── Track.cs  # Core track structure
│   └── context.md
├── Trains/  # Vehicle systems
│   ├── Components/  # Train, cars, wheels
│   ├── Systems/  # Train lifecycle & updates
│   └── context.md
├── Physics/  # Simulation & dynamics
│   ├── Components/  # Movement & forces
│   ├── Systems/  # Physics computation
│   └── context.md
├── Visualization/  # Rendering & visuals
│   ├── Components/  # Meshes & styles
│   ├── Systems/  # Mesh generation & rendering
│   ├── Utils/  # Mesh converters
│   └── context.md
├── Persistence/  # Save/load & import
│   ├── Components/  # Coaster management
│   ├── Systems/  # Serialization
│   ├── Serialization/  # File I/O
│   ├── Import/  # Importers
│   └── context.md
├── State/  # Application state
│   ├── Components/  # Singletons & preferences
│   └── context.md
└── Editor/  # Unity editor integration

```

## Feature Areas

1. **Core** - Shared foundation, utilities, initialization
2. **Track** - Track construction, nodes, connections, graph logic  
3. **Trains** - Vehicles, cars, wheel assemblies
4. **Physics** - Simulation, dynamics, path following, collisions
5. **Visualization** - Rendering, mesh generation, styling
6. **Persistence** - Save/load, serialization, import/export
7. **State** - Application state, singletons, preferences
8. **Editor** - Unity editor integration

## Entrypoints

- `Core/KexEditManager.cs` - Runtime initialization
- `Track/Track.cs` - Core track data structure
- `Persistence/CoasterLoader.cs` - File loading

## Dependencies

- Unity.Entities - ECS framework
- Unity.Mathematics - Math operations
- Unity.Burst - Compilation optimization
- Unity.Physics - Physics simulation
- Unity.Collections - Native containers