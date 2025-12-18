# Serialization Architecture Plan

## Goal

Implement a clean serialization system following hexagonal architecture where:

-   Domain cores are pure logic with no infrastructure dependencies
-   Application layer orchestrates domain objects and implements use cases
-   Adapters handle infrastructure (persistence, legacy import, UI)

**Success Criteria**: Load legacy .kex files, evaluate without ECS, match gold test output.

## Architecture

```
┌────────────────────────────────────────────────────────────────┐
│                      Domain Layer (Cores)                       │
│                                                                 │
│   ┌───────────┐    ┌─────────────┐    ┌──────────────────┐    │
│   │ KexGraph  │    │ KexEdit.Core │    │  KexEdit.Nodes   │    │
│   │           │    │              │    │                  │    │
│   │ Graph     │    │ Point, Frame │    │ NodeType, PortId │    │
│   │ algorithms│    │ Keyframe     │    │ ForceNode.Build  │    │
│   └───────────┘    └──────────────┘    └──────────────────┘    │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│                     Application Layer                           │
│                                                                 │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │                    KexEdit.Coaster                       │ │
│   │                                                           │ │
│   │  Coaster          - aggregate root (graph + data)         │ │
│   │  CoasterEvaluator - use case (coaster → track points)     │ │
│   └──────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│ KexEdit.Persistence      │    │ KexEdit.LegacyImport     │
│ (driven adapter)         │    │ (driven adapter)         │
│                          │    │                          │
│ CoasterSerializer        │    │ LegacyImporter           │
│ bytes ↔ Coaster          │    │ .kex → Coaster           │
└──────────────────────────┘    └──────────────────────────┘
```

## Key Design Decisions

### Layer Responsibilities

**Domain Layer (Cores)** - Pure business logic, no infrastructure:

-   `KexGraph` - Generic graph data structure and algorithms
-   `KexEdit.Core` - Physics primitives (Point, Frame, Keyframe)
-   `KexEdit.Nodes` - Node schemas and build methods (ForceNode.Build, etc.)

**Application Layer** - Orchestrates domain, implements use cases:

-   `KexEdit.Coaster` - Contains both the aggregate root AND the evaluator
    -   `Coaster` - The data model (graph + domain data)
    -   `CoasterEvaluator` - Use case: evaluate coaster → track points

**Adapters** - Infrastructure concerns:

-   `KexEdit.Persistence` - Serialization (bytes ↔ Coaster)
-   `KexEdit.LegacyImport` - Legacy .kex file import

### Why Evaluator is NOT an Adapter

Evaluation is core application logic ("given a coaster, compute the track"), not infrastructure adaptation. It belongs in the application layer because:

-   Both Coaster and CoasterEvaluator are application layer, not domain
-   Evaluator implements a use case, not I/O
-   They're conceptually coupled

### Assembly Dependencies

```
KexEdit.Coaster
  └── KexGraph, KexEdit.Core, KexEdit.Nodes

KexEdit.Persistence
  └── KexEdit.Coaster only

KexEdit.LegacyImport
  └── KexEdit.Coaster, KexEdit.Legacy (legacy serialization)
```

### Coaster (Pure Data)

```csharp
public struct Coaster : IDisposable {
    // Graph topology
    public Graph Graph;

    // Keyframe curves: (nodeId << 8 | propertyId) → slice into Keyframes
    public NativeList<Keyframe> Keyframes;
    public NativeHashMap<ulong, int2> KeyframeRanges;

    // Leaf node values
    public NativeHashMap<uint, float> Scalars;      // nodeId → float
    public NativeHashMap<uint, float3> Vectors;     // nodeId → float3

    // Node configuration
    public NativeHashMap<uint, Duration> Durations; // nodeId → Duration
    public NativeHashSet<uint> Steering;            // presence = enabled
    public NativeHashMap<uint, Point> Anchors;      // nodeId → initial state
}
```

Note: No extension data in Coaster itself. Extensions are handled at the serialization layer via chunk-based format (see Binary Format below).

### CoasterEvaluator (Use Case)

```csharp
public static class CoasterEvaluator {
    public static EvaluationResult Evaluate(in Coaster coaster, Allocator allocator);
}

public struct EvaluationResult : IDisposable {
    public NativeHashMap<uint, NativeList<Point>> Paths;   // nodeId → track points
    public NativeHashMap<uint, Point> OutputAnchors;       // nodeId → output anchor
}
```

Algorithm:

1. Topologically sort nodes via `Graph.FindSourceNodes()` + BFS
2. For each node in order:
    - Read inputs from predecessor outputs (via edges)
    - Read keyframes/config from coaster's hash maps
    - Call appropriate `*Node.Build()` method
    - Store outputs for successors

## What's Done

| Component                                             | Status     |
| ----------------------------------------------------- | ---------- |
| KexGraph (graph algorithms)                           | ✓ Complete |
| KexGraph serialization support                        | ✓ Complete |
| KexEdit.Core (Point, Frame, Keyframe)                 | ✓ Complete |
| KexEdit.Nodes (10 node types, schemas, Build methods) | ✓ Complete |
| KexEdit.NodeGraph (typed extensions, validation)      | ✓ Complete |
| Rust backend (kexedit-core, kexedit-ffi)              | ✓ Complete |
| KexEdit.Legacy namespace migration                    | ✓ Complete |
| KexEdit.Coaster (aggregate root)                      | ✓ Complete |

## What's Missing

| Component             | Purpose                       | Location                 |
| --------------------- | ----------------------------- | ------------------------ |
| **CoasterEvaluator**  | Use case: coaster → points    | `KexEdit.Coaster`        |
| **CoasterSerializer** | New binary format             | `KexEdit.Persistence`    |
| **LegacyImporter**    | .kex → Coaster                | `KexEdit.LegacyImport`   |
| **Gold tests**        | Validate parity with legacy   | `Assets/Tests/Coaster/`  |

### Binary Format (Chunk-Based)

Inspired by glTF/PNG/RIFF. Self-describing chunks eliminate size pre-calculation and enable clean extension support.

#### Design Principles

1. **Self-describing**: Each chunk has type + length, can be skipped if unknown
2. **Per-chunk versioning**: Each chunk versions independently, isolated migrations
3. **Sub-chunks in CORE**: Graph and data are separate sub-chunks for flexibility
4. **Compile-time extensions**: Assemblies register chunk handlers via attributes
5. **No forward compatibility**: Unknown chunks are dropped (simplifies Coaster struct)

#### File Structure

```
┌──────────────────────────────────────────────────────────────┐
│ FILE HEADER                                                   │
│   Magic:       "KEXD" (4 bytes)                               │
│   Version:     uint32 (format version, rarely changes)        │
│   ChunkCount:  uint32                                         │
├──────────────────────────────────────────────────────────────┤
│ CHUNK: CORE (required) ← contains sub-chunks                  │
│   Type:        "CORE" (4 bytes)                               │
│   Version:     uint32                                         │
│   Length:      uint32 (excludes 12-byte header)               │
│   ┌────────────────────────────────────────────────────────┐ │
│   │ SUB-CHUNK: GRPH                                         │ │
│   │   Type: "GRPH", Version: uint32, Length: uint32         │ │
│   │   [NodeCount, PortCount, EdgeCount]                     │ │
│   │   [Nodes: id, type, position...]                        │ │
│   │   [Ports: id, type, owner, isInput...]                  │ │
│   │   [Edges: id, source, target...]                        │ │
│   │   [NextNodeId, NextPortId, NextEdgeId]                  │ │
│   ├────────────────────────────────────────────────────────┤ │
│   │ SUB-CHUNK: DATA                                         │ │
│   │   Type: "DATA", Version: uint32, Length: uint32         │ │
│   │   [KeyframeCount + Keyframes[]]                         │ │
│   │   [RangeCount + KeyframeRanges[]]                       │ │
│   │   [ScalarCount + Scalars[]]                             │ │
│   │   [VectorCount + Vectors[]]                             │ │
│   │   [DurationCount + Durations[]]                         │ │
│   │   [SteeringCount + Steering[]]                          │ │
│   │   [AnchorCount + Anchors[]]                             │ │
│   └────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│ CHUNK: UIST (optional - UI state extension)                   │
│   Type:        "UIST" (4 bytes)                               │
│   Version:     uint32                                         │
│   Length:      uint32                                         │
│   [Camera: position, target, distance, pitch, yaw...]         │
│   [Timeline: offset, zoom]                                    │
│   [NodeGraph: panX, panY, zoom]                               │
├──────────────────────────────────────────────────────────────┤
│ CHUNK: SLCT (optional - selection extension)                  │
│   Type:        "SLCT" (4 bytes)                               │
│   Version:     uint32                                         │
│   Length:      uint32                                         │
│   [SelectedNodeCount + NodeIds[]]                             │
│   [PropertySelectionCount + (nodeId, propertyIds[])[]]        │
└──────────────────────────────────────────────────────────────┘
```

#### Chunk Header (12 bytes)

```csharp
public struct ChunkHeader {
    public FixedString4Bytes Type;    // "CORE", "UIST", "SLCT", etc.
    public uint Version;               // Chunk-specific version
    public uint Length;                // Content length (excludes header)
}
```

#### Extension System

```csharp
// Attribute for compile-time discovery
[AttributeUsage(AttributeTargets.Class)]
public class ChunkExtensionAttribute : Attribute {
    public string ChunkType { get; }
    public ChunkExtensionAttribute(string type) => ChunkType = type;
}

// Interface for extension handlers
public interface IChunkExtension {
    uint CurrentVersion { get; }
    void Write(ChunkWriter writer);
    void Read(ChunkReader reader, uint version);
    void Clear();  // Reset state when loading new file
}

// Example: UI state extension (lives in editor assembly)
[ChunkExtension("UIST")]
public class UIStateExtension : IChunkExtension {
    public CameraState Camera;
    public TimelineState Timeline;
    public NodeGraphViewState NodeGraph;

    public uint CurrentVersion => 1;
    public void Write(ChunkWriter w) { /* ... */ }
    public void Read(ChunkReader r, uint v) { /* ... */ }
    public void Clear() { /* reset to defaults */ }
}
```

#### Serialization API

```csharp
public static class CoasterSerializer {
    // Core-only (portable, runtime)
    public static void WriteCore(ChunkWriter writer, in Coaster coaster);
    public static Coaster ReadCore(ChunkReader reader, Allocator allocator);

    // With extensions (editor)
    public static void Write(ChunkWriter writer, in Coaster coaster,
                             IReadOnlyList<IChunkExtension> extensions);
    public static Coaster Read(ChunkReader reader, Allocator allocator,
                               IReadOnlyList<IChunkExtension> extensions);
}

// Usage in runtime (no extensions)
var coaster = CoasterSerializer.ReadCore(reader, Allocator.Persistent);

// Usage in editor (with extensions)
var extensions = ChunkExtensionRegistry.GetAll();  // compile-time discovered
var coaster = CoasterSerializer.Read(reader, Allocator.Persistent, extensions);
```

#### Benefits Over Legacy

| Legacy Pain Point              | Chunk-Based Solution                      |
| ------------------------------ | ----------------------------------------- |
| SizeCalculator sync            | Length written after content              |
| Monolithic version             | Per-chunk versioning                      |
| UI state in core               | Separate UIST chunk, editor-only          |
| 5-point updates per field      | Add field, bump chunk version             |
| Can't skip unknown             | Length field enables skip                 |
| Migration complexity           | Isolated per-chunk migration              |
| Port type explosion (900 LOC)  | Data-driven via node schemas              |

## Implementation Order

Hexagonal (core → adapters), TDD (tests with each step).

### Phase 1: Application Layer

1. ✓ **Graph serialization**
2. ✓ **Coaster** + unit tests
3. **CoasterEvaluator** + unit tests (hand-crafted coasters)

### Phase 2: Legacy Adapter → Gold Tests

4. **LegacyImporter** + tests
5. **Gold tests** - load .kex → evaluate → compare to legacy output

Success criteria met here: legacy files load and evaluate correctly.

### Phase 3: New Persistence

6. **Chunk infrastructure** (ChunkReader/Writer, extension registry)
7. **CoasterSerializer** + round-trip tests
8. **Editor extensions** (UIST, SLCT)

## Files to Create

```
Assets/Runtime/Coaster/
  KexEdit.Coaster.asmdef
  Coaster.cs
  CoasterEvaluator.cs

Assets/Runtime/Persistence/
  KexEdit.Persistence.asmdef
  ChunkReader.cs, ChunkWriter.cs
  IChunkExtension.cs, ChunkExtensionAttribute.cs
  CoasterSerializer.cs

Assets/Runtime/LegacyImport/
  KexEdit.LegacyImport.asmdef
  LegacyImporter.cs

Assets/Editor/Persistence/
  UIStateExtension.cs      (UIST chunk)
  SelectionExtension.cs    (SLCT chunk)
```
