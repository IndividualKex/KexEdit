# KexEdit.Persistence

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
    ├── ExtensionSchema.cs      # Static schema (UIST chunk type/version)
    ├── UIStateChunk.cs         # Unified UI state (positions, view state, keyframe UI)
    └── UIExtensionCodec.cs     # UIST chunk read/write
```

## Architecture

**Layers** (bottom to top):
1. **Chunk primitives**: `ChunkWriter`, `ChunkReader` - domain-agnostic byte manipulation
2. **GraphCodec**: Serializes `Graph` - uses chunk primitives, no coaster knowledge
3. **CoasterSerializer**: Serializes `Coaster` - uses GraphCodec and chunk primitives
4. **Extensions**: Optional UIST chunk for UI state

**Extension model** (like glTF/glb):
- Extensions are additional top-level chunks after CORE
- Unknown chunks are skipped (forward compatibility)
- Each extension defines its own chunk type and IO functions
- No central registry or interface needed

## Entrypoints

- `CoasterSerializer.Write/Read` - Core coaster data
- `UIExtensionCodec.Write/TryRead` - UI state extension (positions, view, keyframe UI)

## Dependencies

- KexEdit.Graph, KexEdit.Sim, KexEdit.Sim.Schema, KexEdit.Coaster
