# Serialization Architecture Plan

## Goal

Implement a clean serialization system following hexagonal architecture where:
- Domain cores are pure logic with no infrastructure dependencies
- Application layer orchestrates domain objects and implements use cases
- Adapters handle infrastructure (persistence, legacy import, UI)

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
│   │                    KexEdit.Document                       │ │
│   │                                                           │ │
│   │  CoasterDocument  - aggregate root (graph + data)         │ │
│   │  DocumentEvaluator - use case (document → track points)   │ │
│   └──────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│ KexEdit.Persistence      │    │ KexEdit.LegacyImport     │
│ (driven adapter)         │    │ (driven adapter)         │
│                          │    │                          │
│ DocumentSerializer       │    │ LegacyImporter           │
│ bytes ↔ Document         │    │ .kex → Document          │
└──────────────────────────┘    └──────────────────────────┘
```

## Key Design Decisions

### Layer Responsibilities

**Domain Layer (Cores)** - Pure business logic, no infrastructure:
- `KexGraph` - Generic graph data structure and algorithms
- `KexEdit.Core` - Physics primitives (Point, Frame, Keyframe)
- `KexEdit.Nodes` - Node schemas and build methods (ForceNode.Build, etc.)

**Application Layer** - Orchestrates domain, implements use cases:
- `KexEdit.Document` - Contains both the aggregate root AND the evaluator
  - `CoasterDocument` - The data model (graph + domain data)
  - `DocumentEvaluator` - Use case: evaluate document → track points

**Adapters** - Infrastructure concerns:
- `KexEdit.Persistence` - Serialization (bytes ↔ Document)
- `KexEdit.LegacyImport` - Legacy .kex file import

### Why Evaluator is NOT an Adapter

Evaluation is core application logic ("given a coaster, compute the track"), not infrastructure adaptation. It belongs with Document because:
- Both are application layer, not domain
- Evaluator implements a use case, not I/O
- They're conceptually coupled

### Assembly Dependencies

```
KexEdit.Document
  └── KexGraph, KexEdit.Core, KexEdit.Nodes

KexEdit.Persistence
  └── KexEdit.Document only

KexEdit.LegacyImport
  └── KexEdit.Document, KexEdit (legacy serialization)
```

### CoasterDocument (Pure Data)

```csharp
public struct CoasterDocument : IDisposable {
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

    // Opaque extension data (UI state, etc.)
    public NativeArray<byte> Extensions;
}
```

### DocumentEvaluator (Use Case)

```csharp
public static class DocumentEvaluator {
    public static EvaluationResult Evaluate(in CoasterDocument doc, Allocator allocator);
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
   - Read keyframes/config from document's hash maps
   - Call appropriate `*Node.Build()` method
   - Store outputs for successors

### Extension Mechanism (UI Metadata)

UI state is separate from core coaster data:
- Document provides opaque `Extensions: NativeArray<byte>` slot
- UI layer serializes its own state (selection, camera, timeline)
- Document passes extension bytes through without interpretation

## What's Done

| Component | Status |
|-----------|--------|
| KexGraph (graph algorithms) | ✓ Complete |
| KexEdit.Core (Point, Frame, Keyframe) | ✓ Complete |
| KexEdit.Nodes (10 node types, schemas, Build methods) | ✓ Complete |
| KexEdit.NodeGraph (typed extensions, validation) | ✓ Complete |
| Rust backend (kexedit-core, kexedit-ffi) | ✓ Complete |

## What's Missing

| Component | Purpose | Location |
|-----------|---------|----------|
| **Graph serialization support** | Public ID generators + RebuildIndexMaps | `KexGraph` |
| **CoasterDocument** | Aggregate root (graph + data) | `KexEdit.Document` |
| **DocumentEvaluator** | Use case: document → points | `KexEdit.Document` |
| **DocumentSerializer** | New binary format | `KexEdit.Persistence` |
| **LegacyImporter** | .kex → Document | `KexEdit.LegacyImport` |
| **Gold tests** | Validate parity with legacy | `Assets/Tests/Document/` |

### Graph Serialization Support

Graph is self-describing. Index maps are derived state, excluded from serialization.

**Changes to Graph.cs:**
1. Make `NextNodeId`, `NextPortId`, `NextEdgeId` public
2. Add `RebuildIndexMaps()` extension method

**Serialization flow:**
- Write: iterate Graph's public NativeLists + ID generators
- Read: create Graph, populate lists, set ID generators, call `RebuildIndexMaps()`

### New Binary Format

```
[Magic: 4 bytes "KEXD"]
[Version: uint32]
[Graph section]
  [NodeCount, PortCount, EdgeCount]
  [Nodes: id, type, position...]
  [Ports: id, type, owner, isInput...]
  [Edges: id, source, target...]
  [NextNodeId, NextPortId, NextEdgeId]
[Data section]
  [KeyframeRanges: count + (key, start, len)[]]
  [Keyframes: count + keyframes[]]
  [Scalars: count + (key, value)[]]
  [Vectors: count + (key, x, y, z)[]]
  [Durations: count + (key, type, value)[]]
  [Steering: count + keys[]]
  [Anchors: count + (key, Point)[]]
[Extensions: length + bytes]
```

## Implementation Order

1. **Graph serialization support** - public IDs + RebuildIndexMaps in `KexGraph`
2. **CoasterDocument** - aggregate root in `KexEdit.Document`
3. **DocumentEvaluator** - use case in `KexEdit.Document`
4. **DocumentSerializer** - new binary format in `KexEdit.Persistence`
5. **LegacyImporter** - .kex → Document in `KexEdit.LegacyImport`
6. **Gold tests** - validate parity with legacy system

## Files to Create

| File | Purpose |
|------|---------|
| `Assets/Plugins/KexGraph/GraphSerializationExtensions.cs` | RebuildIndexMaps |
| `Assets/Runtime/Document/KexEdit.Document.asmdef` | Assembly def |
| `Assets/Runtime/Document/CoasterDocument.cs` | Aggregate root |
| `Assets/Runtime/Document/DocumentEvaluator.cs` | Evaluation use case |
| `Assets/Runtime/Document/EvaluationResult.cs` | Output container |
| `Assets/Runtime/Persistence/KexEdit.Persistence.asmdef` | Assembly def |
| `Assets/Runtime/Persistence/DocumentSerializer.cs` | Binary format |
| `Assets/Runtime/LegacyImport/KexEdit.LegacyImport.asmdef` | Assembly def |
| `Assets/Runtime/LegacyImport/LegacyImporter.cs` | .kex → Document |
| `Assets/Tests/Document/DocumentEvaluatorTests.cs` | Gold test parity |
| `Assets/Tests/Document/LegacyImporterTests.cs` | Import tests |

## Files to Modify

| File | Change |
|------|--------|
| `Assets/Plugins/KexGraph/Graph.cs:26-28` | Make NextNodeId/NextPortId/NextEdgeId public |
