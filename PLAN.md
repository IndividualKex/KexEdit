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
       │              │   NodeConfigStore │ ← keyed by uint (opaque)
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
          │  + KeyframeStore │  ← uint keys = nodeIds
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

Stores live in `KexEdit.Nodes`, keyed by opaque `uint`:

```csharp
// Does NOT depend on KexGraph - uses uint keys
public struct KeyframeStore {
    NativeHashMap<uint, NativeHashMap<PropertyId, NativeList<Keyframe>>> data;
}

public struct NodeConfigStore {
    NativeHashMap<uint, NodeConfig> data;
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
| **KeyframeStore** | Maps `uint → (PropertyId → Keyframe[])` | `KexEdit.Nodes` |
| **NodeConfigStore** | Maps `uint → NodeConfig` | `KexEdit.Nodes` |
| **GraphSnapshot** | Serializable graph topology | `KexGraph` |
| **CoasterDocument** | Composes graph + stores + extensions | `KexEdit.Document` (new) |

### NodeConfig

Per-node configuration beyond scalar inputs:
- `DurationType` (Time vs Distance) - Force/Geometric
- `Steering` flag - Geometric
- `MeshFilePath` - future Mesh nodes

## Implementation Order

1. **KeyframeStore** in `KexEdit.Nodes` - uint-keyed storage
2. **NodeConfigStore** in `KexEdit.Nodes` - uint-keyed storage
3. **GraphSnapshot** in `KexGraph` - graph's own serializable form
4. **CoasterDocument** in new `KexEdit.Document` - composition layer
5. **Binary serialization** - Burst-compatible format
6. **UI extension adapter** - separate, injects metadata
