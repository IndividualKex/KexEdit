# Persistence Context

Save/load, serialization, and import/export

## Purpose

- Handles project save and load operations
- Manages track serialization formats (legacy and KEXD)
- Provides import from external formats

## Key Systems

- `SerializationSystem` - Save/load, undo/redo; auto-detects format by magic header
- `LegacyImporter` - Reads .kex binary directly → Coaster aggregate
- `KexdAdapter` - Converts Coaster aggregate → ECS entities

## Serialization

- `ClipboardSerializer` - Clipboard copy/paste using CoasterSerializer
- `SerializedGraph.cs` - Legacy format structures (SerializedNode, SerializedPort, version migration types)
- KEXD format - Chunk-based format with CORE + UIST extensions

## Data Flow

```
KEXD Save:   ECS entities → Coaster + UI state → KEXD chunks
KEXD Load:   KEXD bytes → Coaster → KexdAdapter → ECS entities
Legacy Load: .kex bytes → LegacyImporter → Coaster → KexdAdapter → ECS entities
```

## Entry Points

- `CoasterLoader.cs` - Main file loading entry point

## Dependencies

- Coaster aggregate (source of truth)
- Track components (ECS entities)

## Scope

- In: File I/O, serialization, import
- Out: Runtime logic, rendering, UI