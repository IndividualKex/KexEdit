# Persistence Context

Save/load, serialization, and import/export

## Purpose

- Handles project save and load operations
- Manages track serialization format
- Provides import from external formats
- Handles clipboard operations

## Key Components

- `Coaster` - Project/coaster entity
- `CoasterReference` - References to coasters
- `AppendedCoasterTag` - Marks appended coasters
- Load events - Track and train style loading

## Key Systems

- `SerializationSystem` - Main save/load system
- `AppendCleanupSystem` - Cleans up appended data

## Serialization

- `BinaryReader/Writer` - Binary file I/O
- `GraphSerializer` - Node graph serialization
- `ClipboardSerializer` - Copy/paste operations
- `SerializedGraph` - Serialized graph format

## Import

- `EntityImporter` - Import entities
- `ObjImporter` - Import OBJ meshes
- `CoasterLoader` - Load coaster files

## Entry Points

- `CoasterLoader.cs` - Main file loading entry point

## Dependencies

- Track (for graph structure)
- Core (for utilities)

## Scope

- In: File I/O, serialization, import/export
- Out: Runtime logic, rendering, UI