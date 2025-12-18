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
  └── KexEdit.Coaster, KexEdit (legacy serialization)
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

    // Opaque extension data (UI state, etc.)
    public NativeArray<byte> Extensions;
}
```

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

### Extension Mechanism (UI Metadata)

UI state is separate from core coaster data:

-   Coaster provides opaque `Extensions: NativeArray<byte>` slot
-   UI layer serializes its own state (selection, camera, timeline)
-   Coaster passes extension bytes through without interpretation

## What's Done

| Component                                             | Status     |
| ----------------------------------------------------- | ---------- |
| KexGraph (graph algorithms)                           | ✓ Complete |
| KexGraph serialization support                        | ✓ Complete |
| KexEdit.Core (Point, Frame, Keyframe)                 | ✓ Complete |
| KexEdit.Nodes (10 node types, schemas, Build methods) | ✓ Complete |
| KexEdit.NodeGraph (typed extensions, validation)      | ✓ Complete |
| Rust backend (kexedit-core, kexedit-ffi)              | ✓ Complete |

## What's Missing

| Component             | Purpose                       | Location                 |
| --------------------- | ----------------------------- | ------------------------ |
| **Coaster**           | Aggregate root (graph + data) | `KexEdit.Coaster`        |
| **CoasterEvaluator**  | Use case: coaster → points    | `KexEdit.Coaster`        |
| **CoasterSerializer** | New binary format             | `KexEdit.Persistence`    |
| **LegacyImporter**    | .kex → Coaster                | `KexEdit.LegacyImport`   |
| **Gold tests**        | Validate parity with legacy   | `Assets/Tests/Document/` |

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

1. ✓ **Graph serialization support** - public IDs + RebuildIndexMaps in `KexGraph`
2. **Coaster** - aggregate root in `KexEdit.Coaster`
3. **CoasterEvaluator** - use case in `KexEdit.Coaster`
4. **CoasterSerializer** - new binary format in `KexEdit.Persistence`
5. **LegacyImporter** - .kex → Coaster in `KexEdit.LegacyImport`
6. **Gold tests** - validate parity with legacy system

## Files to Create

| File                                                      | Purpose             |
| --------------------------------------------------------- | ------------------- |
| `Assets/Runtime/Document/KexEdit.Coaster.asmdef`          | Assembly def        |
| `Assets/Runtime/Document/Coaster.cs`                      | Aggregate root      |
| `Assets/Runtime/Document/CoasterEvaluator.cs`             | Evaluation use case |
| `Assets/Runtime/Document/EvaluationResult.cs`             | Output container    |
| `Assets/Runtime/Persistence/KexEdit.Persistence.asmdef`   | Assembly def        |
| `Assets/Runtime/Persistence/CoasterSerializer.cs`         | Binary format       |
| `Assets/Runtime/LegacyImport/KexEdit.LegacyImport.asmdef` | Assembly def        |
| `Assets/Runtime/LegacyImport/LegacyImporter.cs`           | .kex → Coaster      |
| `Assets/Tests/Document/CoasterEvaluatorTests.cs`          | Gold test parity    |
| `Assets/Tests/Document/LegacyImporterTests.cs`            | Import tests        |
