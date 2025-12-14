# Legacy Runtime Context

Monolithic ECS runtime being hollowed out. Systems here call into clean Core/Nodes layer.

## Purpose

- Unity DOTS ECS systems for track processing
- Being migrated to use clean node implementations
- Will eventually become thin adapters

## Layout

```
Legacy/
├── Core/           # Foundation & shared infrastructure
├── Track/          # Track construction, graph, section building
├── Trains/         # Vehicle systems
├── Physics/        # Simulation & dynamics
├── Visualization/  # Rendering & mesh generation
├── Persistence/    # Save/load & import
├── State/          # Application state & singletons
└── Editor/         # Unity editor integration
```

## Migration Status

- BuildForceSectionSystem → uses ForceNode.Build()
- Other section systems → pending migration

## Entrypoints

- `Core/KexEditManager.cs` - Runtime initialization
- `Track/Track.cs` - Core track data structure
- `Persistence/CoasterLoader.cs` - File loading

## Dependencies

- KexEdit.Core, KexEdit.Nodes.*, Unity.Entities, Unity.Mathematics, Unity.Burst
