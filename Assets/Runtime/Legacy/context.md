# KexEdit.Legacy

Unity ECS runtime layer. Systems here call into clean Sim/Schema layer.

## Purpose

- Unity DOTS ECS systems for track processing
- Adapters between modern Sim layer and Unity ECS
- LegacyImporter: Reads .kex binary directly → Coaster aggregate on load

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
├── Editor/         # Unity editor integration
└── LegacyImporter.cs  # .kex binary → Coaster conversion
```

## Entrypoints

- `Core/KexEditManager.cs` - Runtime initialization
- `Track/Track.cs` - Core track data structure
- `Persistence/CoasterLoader.cs` - File loading

## Dependencies

- KexEdit.Sim, KexEdit.App.Coaster, KexEdit.Sim.Nodes.*, KexEdit.Graph, Unity.Entities, Unity.Mathematics, Unity.Burst
