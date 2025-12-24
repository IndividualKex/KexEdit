# Persistence Context

Chunk-based serialization for Coaster data.

## Purpose

- Self-describing chunk format (type + version + length)
- Per-chunk versioning for isolated migrations
- Extensions are just additional chunks (like glTF/glb)

## Layout

```
Persistence/
├── context.md
├── KexEdit.Persistence.asmdef
├── ChunkHeader.cs              # Chunk header (type, version, length)
├── ChunkWriter.cs              # Binary writer with nested chunk support
├── ChunkReader.cs              # Binary reader with chunk skipping
├── GraphCodec.cs               # Graph serialization (static Write/Read)
├── CoasterSerializer.cs        # Coaster ↔ bytes (CORE chunk with GRPH/DATA sub-chunks)
└── Extensions/
    ├── ExtensionSchema.cs      # Static schema (chunk types, versions)
    ├── UIMetadataChunk.cs      # Pure data struct for node positions
    └── UIMetadataCodec.cs      # Static read/write functions
```

## Architecture

**Layers** (bottom to top):
1. **Chunk primitives**: `ChunkWriter`, `ChunkReader` - domain-agnostic byte manipulation
2. **GraphCodec**: Serializes `Graph` - uses chunk primitives, no coaster knowledge
3. **CoasterSerializer**: Serializes `Coaster` - uses GraphCodec and chunk primitives
4. **Extensions**: Optional chunks (UIMD, etc.) - each has its own Codec class

**Extension model** (like glTF/glb):
- Extensions are additional top-level chunks after CORE
- Unknown chunks are skipped (forward compatibility)
- Each extension defines its own chunk type and IO functions
- No central registry or interface needed

## Entrypoints

- `CoasterSerializer.Write(writer, coaster)` - Serialize coaster to bytes
- `CoasterSerializer.Read(reader, allocator)` - Deserialize bytes to coaster
- `UIMetadataCodec.WriteChunk(writer, chunk)` - Write UI metadata extension
- `UIMetadataCodec.TryReadFromFile(reader, allocator, out chunk)` - Read UI metadata from file

## Dependencies

- KexGraph, KexEdit.Core, KexEdit.Nodes, KexEdit.Coaster
