# Serialization Architecture Plan

## Goal

Implement a clean serialization system following hexagonal architecture, where:
- Core coaster data is self-describing (serde-like pattern)
- UI metadata is "tacked on" as extensions (glTF-like pattern)
- No centralized serializer that knows all types
- Each hexagon is self-sufficient; composition happens at the outer layer

## Architecture

```
┌─────────────┐     ┌─────────────────────┐
│  KexGraph   │     │    KexEdit.Core     │
│  (Graph     │     │    (Coaster Core)   │
│   Hexagon)  │     │                     │
│             │     │  Point, Frame       │
│  Graph      │     │  Keyframe           │
│  Snapshot   │     └──────────┬──────────┘
└──────┬──────┘                │
       │              ┌────────▼──────────┐
       │              │   KexEdit.Nodes   │
       │              │                   │
       │              │  Schema:          │
       │              │   NodeType        │
       │              │   PortId          │
       │              │   PropertyId      │
       │              │   NodeSchema      │
       │              │                   │
       │              │  Storage:         │
       │              │   KeyframeStore   │ ← keyed by uint (opaque)
       │              │   ScalarStore     │
       │              │   VectorStore     │
       │              │   DurationTypeStore│
       │              │   SteeringStore   │
       │              └────────┬──────────┘
       │                       │
       └───────────┬───────────┘
                   │
          ┌────────▼────────┐
          │ KexEdit.NodeGraph│  (adapter - thin)
          │                  │
          │ TypedExtensions  │
          │ Validation       │
          └────────┬─────────┘
                   │
          ┌────────▼────────┐
          │ KexEdit.Document │  (model - IS the coaster)
          │                  │
          │ CoasterDocument  │
          │  = Graph         │
          │  + Stores        │  ← uint keys = nodeIds
          │  + Extensions    │
          └──────────────────┘
```

## Key Design Decisions

### Values as Leaf Nodes (Implemented)

Port values are `Scalar`/`Vector` leaf nodes, not port metadata. Graph is self-describing.

- `NodeType.Scalar` / `NodeType.Vector` (0 inputs, 1 output)
- `PortId.Scalar` / `PortId.Vector` (generic value outputs)
- Category-based validation (`PortDataType` matching)

### Data Stores in Coaster Hexagon

Stores live in `KexEdit.Nodes`, keyed by opaque `uint`. Each concern gets its own store (composition over bags). Follow existing patterns (factory, IDisposable, no nested containers):

```csharp
// All stores use uint keys - no dependency on KexGraph

// Animation curves: (nodeId, propertyId) → Keyframe[]
public struct KeyframeStore : IDisposable {
    public NativeList<Keyframe> Keyframes;
    public NativeHashMap<ulong, int2> Ranges;  // key = (nodeId << 8) | propertyId
}

// Leaf node values
public struct ScalarStore : IDisposable {
    public NativeHashMap<uint, float> Values;
}

public struct VectorStore : IDisposable {
    public NativeHashMap<uint, float3> Values;
}

// Node configuration (separate stores per concern)
public struct DurationTypeStore : IDisposable {
    public NativeHashMap<uint, DurationType> Values;
}

public struct SteeringStore : IDisposable {
    public NativeHashSet<uint> Enabled;  // presence = true
}
```

The stores don't know about "nodes" in the graph sense. The uint keys become meaningful as nodeIds only at the Document layer.

### Document is Model, Not Adapter

- **Adapter** (NodeGraph): Translates between graph and schema
- **Model** (Document): IS the coaster - composes graph + data + extensions

Document depends on both hexagons because it's the composition point.

### Extension Mechanism (UI Metadata)

UI state is separate from core coaster data:
- Document provides opaque `extensions: byte[]` slot
- UI layer serializes its own state (selection, camera, timeline)
- Document passes extension bytes through without interpretation

## What's Missing

| Component | Purpose | Location |
|-----------|---------|----------|
| **KeyframeStore** | `(nodeId, propertyId) → Keyframe[]` | `KexEdit.Nodes` |
| **ScalarStore** | `nodeId → float` (Scalar leaf values) | `KexEdit.Nodes` |
| **VectorStore** | `nodeId → float3` (Vector leaf values) | `KexEdit.Nodes` |
| **DurationTypeStore** | `nodeId → DurationType` | `KexEdit.Nodes` |
| **SteeringStore** | `nodeId → enabled` (set membership) | `KexEdit.Nodes` |
| **GraphSnapshot** | Serializable graph topology | `KexGraph` |
| **CoasterDocument** | Composes graph + stores + extensions | `KexEdit.Document` (new) |

## Implementation Order

1. **ScalarStore / VectorStore** in `KexEdit.Nodes` - leaf node values
2. **KeyframeStore** in `KexEdit.Nodes` - animation curves
3. **DurationTypeStore / SteeringStore** in `KexEdit.Nodes` - node config
4. **GraphSnapshot** in `KexGraph` - graph's own serializable form
5. **CoasterDocument** in new `KexEdit.Document` - composition layer
6. **Binary serialization** - Burst-compatible format
7. **UI extension adapter** - separate, injects metadata
