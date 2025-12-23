# Persistence Context

Save/load, serialization, and import/export

## Purpose

- Handles project save and load operations
- Manages track serialization formats (legacy and KEXD)
- Provides import from external formats

## Key Systems

- `SerializationSystem` - Save/load, undo/redo; auto-detects format by magic header
- `LegacyImporter` - Converts SerializedGraph → Coaster aggregate
- `KexdAdapter` - Converts Coaster aggregate → ECS entities

## Serialization

- `GraphSerializer` - Legacy node graph serialization (read-only for old files)
- `SerializedGraph` - Legacy intermediate format
- KEXD format - Chunk-based format with CORE + UIMD extensions

## Data Flow

```
KEXD Save:   ECS entities → Coaster + UI metadata → KEXD chunks
KEXD Load:   KEXD bytes → Coaster → KexdAdapter → ECS entities
Legacy Load: Legacy bytes → SerializedGraph → LegacyImporter → Coaster → ECS entities
```

## Entry Points

- `CoasterLoader.cs` - Main file loading entry point

## Dependencies

- Coaster aggregate (source of truth)
- Track components (ECS entities)

## Scope

- In: File I/O, serialization, import
- Out: Runtime logic, rendering, UI