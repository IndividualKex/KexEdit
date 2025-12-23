# Persistence Context

Save/load, serialization, and import/export

## Purpose

- Handles project save and load operations
- Manages track serialization formats (legacy and KEXD)
- Provides import from external formats

## Key Systems

- `SerializationSystem` - Save/load, undo/redo; supports both legacy and KEXD formats
- `LegacyImporter` - Converts SerializedGraph to Coaster aggregate

## Serialization

- `GraphSerializer` - Legacy node graph serialization
- `SerializedGraph` - Legacy intermediate format
- `SerializeToKEXD` - New KEXD chunk-based format (Phase 4A complete)

## Data Flow

```
Legacy Save: ECS entities → SerializedGraph → bytes
KEXD Save:   ECS entities → Coaster + UI metadata → KEXD chunks
Load:        bytes → SerializedGraph → Coaster → ECS entities
```

## Entry Points

- `CoasterLoader.cs` - Main file loading entry point

## Dependencies

- Coaster aggregate (source of truth)
- Track components (ECS entities)

## Scope

- In: File I/O, serialization, import
- Out: Runtime logic, rendering, UI