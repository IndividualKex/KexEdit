# Persistence Context

Chunk-based serialization for Coaster data.

## Purpose

- Self-describing chunk format (type + version + length)
- Per-chunk versioning for isolated migrations
- Data-driven extension system for editor-only data

## Layout

```
Persistence/
├── context.md
├── KexEdit.Persistence.asmdef
├── ChunkHeader.cs              # Chunk header (type, version, length)
├── ChunkWriter.cs              # Binary writer with nested chunk support
├── ChunkReader.cs              # Binary reader with chunk skipping
├── CoasterSerializer.cs        # Coaster ↔ bytes (CORE chunk with GRPH/DATA sub-chunks)
└── Extensions/
    ├── ExtensionSchema.cs      # Static schema (chunk types, versions)
    ├── ExtensionData.cs        # Container for all extension data
    ├── ExtensionSerializer.cs  # Extension read/write orchestration
    ├── UIMetadataChunk.cs      # Pure data struct for node positions
    └── UIMetadataIO.cs         # Static read/write functions
```

## Extension System

Data-driven design (no interfaces) for Rust FFI compatibility:

- **Schema**: `ExtensionSchema` defines chunk types and versions (like NodeSchema in Rust)
- **Data structs**: Plain structs (`UIMetadataChunk`) hold extension data
- **Static IO**: `UIMetadataIO.Read/Write` handle serialization
- **Orchestration**: `ExtensionSerializer` coordinates extension handling

## Entrypoints

- `CoasterSerializer.Write(writer, coaster)` - Serialize coaster to bytes
- `CoasterSerializer.Read(reader, allocator)` - Deserialize bytes to coaster
- `ExtensionSerializer.WriteUIMetadata(writer, chunk)` - Write UI metadata extension
- `ExtensionSerializer.ReadExtensions(reader, allocator)` - Read all extensions

## Dependencies

- KexGraph, KexEdit.Core, KexEdit.Nodes, KexEdit.Coaster
