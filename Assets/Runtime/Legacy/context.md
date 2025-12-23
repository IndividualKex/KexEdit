# Legacy Runtime Context

Monolithic ECS runtime being hollowed out. Systems here call into clean Core/Nodes layer.

## Purpose

- Unity DOTS ECS systems for track processing
- Adapters between modern Core layer and Unity ECS
- LegacyImporter: Converts SerializedGraph → Coaster aggregate on load

## Layout

```
Legacy/
├── Core/           # Foundation & shared infrastructure
├── Track/          # Track construction, graph, section building
├── Trains/         # Vehicle systems
├── Physics/        # Simulation & dynamics
├── Visualization/  # Rendering & mesh generation
├── Persistence/    # Save/load & import (SerializationSystem calls LegacyImporter)
├── State/          # Application state & singletons
├── Editor/         # Unity editor integration
└── LegacyImporter.cs  # SerializedGraph → Coaster conversion
```

## Entrypoints

- `Core/KexEditManager.cs` - Runtime initialization
- `Track/Track.cs` - Core track data structure
- `Persistence/CoasterLoader.cs` - File loading

## Dependencies

- KexEdit.Core, KexEdit.Coaster, KexEdit.Nodes.*, KexGraph, Unity.Entities, Unity.Mathematics, Unity.Burst
