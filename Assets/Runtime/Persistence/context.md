# Persistence Context

Chunk-based serialization for Coaster data.

## Purpose

- Self-describing chunk format (type + version + length)
- Per-chunk versioning for isolated migrations
- Extension system for editor-only data

## Layout

```
Persistence/
├── context.md
├── KexEdit.Persistence.asmdef
├── ChunkHeader.cs              # Chunk header (type, version, length)
├── ChunkWriter.cs              # Binary writer with nested chunk support
├── ChunkReader.cs              # Binary reader with chunk skipping
├── IChunkExtension.cs          # Extension interface
├── ChunkExtensionAttribute.cs  # Extension discovery attribute
└── CoasterSerializer.cs        # Coaster ↔ bytes (CORE chunk with GRPH/DATA sub-chunks)
```

## Entrypoints

- `CoasterSerializer.Write(writer, coaster)` - Serialize coaster to bytes
- `CoasterSerializer.Read(reader, allocator)` - Deserialize bytes to coaster

## Dependencies

- KexGraph, KexEdit.Core, KexEdit.Nodes, KexEdit.Coaster
