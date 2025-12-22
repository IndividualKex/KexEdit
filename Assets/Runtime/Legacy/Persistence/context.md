# Persistence Context

Save/load, serialization, and import/export

## Purpose

- Handles project save and load operations
- Manages track serialization format
- Provides import from external formats

## Key Systems

- `SerializationSystem` - Save/load, undo/redo; syncs Anchor component from port values on save
- `LegacyImporter` - Converts SerializedGraph to Coaster aggregate; populates Coaster.Scalars/Keyframes

## Serialization

- `GraphSerializer` - Node graph serialization to/from bytes
- `SerializedGraph` - Intermediate format for save/load

## Data Flow

```
Save: ECS entities → SerializeNode (syncs Anchor) → SerializedGraph → bytes
Load: bytes → SerializedGraph → LegacyImporter → Coaster aggregate
                             → DeserializeNode → ECS entities
```

## Entry Points

- `CoasterLoader.cs` - Main file loading entry point

## Dependencies

- Coaster aggregate (source of truth)
- Track components (ECS entities)

## Scope

- In: File I/O, serialization, import
- Out: Runtime logic, rendering, UI