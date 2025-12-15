# Runtime Migration Plan

Rebuild KexEdit runtime with hexagonal architecture. Enables Rust/WASM migration and clean separation of concerns.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              INFRASTRUCTURE ADAPTERS                            │
│   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│   │ Unity ECS   │ │Serialization│ │ Positioning │  ...         │
│   │  Systems    │ │  Adapter    │ │   Systems   │              │
│   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘              │
│          └───────────────┼───────────────┘                      │
│                          ▼                                      │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              NODE TYPES (KexEdit.Nodes.*)                       │
│   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│   │  ForceNode  │ │ GeometricN. │ │ CurvedNode  │  ...         │
│   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘              │
│          └───────────────┼───────────────┘                      │
│                          ▼                                      │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              NODE SCHEMA (KexEdit.Nodes)                        │
│   PortId, PropertyId, NodeType, NodeSchema, PropertyIndex       │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│         GRAPH SCHEMA (KexEdit.Graph.Schema)                     │
│   TypeId enums, Data contracts, Adapter traits                  │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              GRAPH CORE (KexEdit.Graph)                         │
│   Node, Port, Edge, Graph - pure structure, opaque data blobs   │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│                  CORE (KexEdit.Core)                            │
│   Point, Frame, Curvature, Forces, Sim, Keyframe                │
└─────────────────────────────────────────────────────────────────┘
```

## Directory Structure

```
KexEdit/
├── rust-backend/
│   ├── graph-core/            Generic graph (reusable)
│   ├── kexedit-graph-schema/  Type mappings, data contracts
│   ├── kexedit-core/          Pure physics/math
│   ├── kexedit-nodes/         Node implementations
│   └── kexedit-ffi/           C FFI layer for Unity
├── Assets/Runtime/
│   ├── Graph/                 Generic graph (C#)
│   ├── Graph.Schema/          Type mappings, data contracts (C#)
│   ├── Core/                  Pure physics/math (C#)
│   ├── Nodes/                 Node schema + implementations (C#)
│   ├── Serialization/         Serialization adapter (C#)
│   ├── Native/RustCore/       Rust FFI bindings
│   └── Legacy/                ECS adapters and legacy code
```

## Assembly Dependencies

```
KexEdit.Serialization ──► KexEdit.Graph.Schema ──► KexEdit.Graph
         │                        │
         ▼                        ▼
KexEdit.Nodes.* ──────► KexEdit.Nodes ──────────► KexEdit.Core
```

## Design Principles

- **Core is Physics-Only**: No awareness of train direction, section boundaries, or friction resets
- **Graph is Domain-Agnostic**: Pure structure with opaque data blobs, no type awareness
- **Nodes Own State Management**: Node types control initial state for each section
- **Hexagonal Architecture**: Core has no dependencies; adapters use core ports
- **Single Source of Truth**: Continuous buffers; systems interpret as needed
- **Testability**: Every layer has comprehensive tests before adding outer layers
- **Portability**: Architecture designed for Rust/WASM port

## Graph + Serialization Architecture

### Layer 1: Graph Core (Pure, Reusable)

Generic graph structure with opaque data. No type awareness.

```csharp
// KexEdit.Graph - completely domain-agnostic
public struct Node {
    public ulong Id;
    public float2 Position;
    public byte[] Data;           // Opaque payload
    public ulong[] InputPorts;
    public ulong[] OutputPorts;
}

public struct Port {
    public ulong Id;
    public byte[] Data;           // Opaque payload
}

public struct Edge {
    public ulong Id;
    public ulong Source;          // Port ID
    public ulong Target;          // Port ID
}

public struct Graph {
    public Node[] Nodes;
    public Port[] Ports;
    public Edge[] Edges;
    public byte[] Metadata;       // UI state, etc.
}
```

### Layer 2: Graph Schema (KexEdit-specific bindings)

Maps semantic types to graph blobs. Defines data contracts and adapter traits.

```csharp
// KexEdit.Graph.Schema - project-specific type mappings

public enum NodeTypeId : byte {
    Anchor = 1,
    ForceSection = 2,
    GeometricSection = 3,
    // ...
}

public enum PortTypeId : byte {
    Anchor = 1,
    Path = 2,
    Float = 3,
    Float3 = 4,
}

// Data contracts - shapes for serialization
public struct AnchorContract {
    public float3 SpinePosition;
    public float3 Direction;
    public float Roll;
    public float Velocity;
    public float HeartOffset;
    public float Friction;
    public float Resistance;
}

// Adapter trait
public interface INodeAdapter {
    NodeTypeId TypeId { get; }
    byte[] ToContract(object domain);
    object FromContract(byte[] data);
}
```

### Layer 3: Serialization Adapter (Implementations)

Dedicated assembly that implements adapters for all serializable types.

```csharp
// KexEdit.Serialization - converts domain ↔ contracts

public class AnchorAdapter : INodeAdapter {
    public NodeTypeId TypeId => NodeTypeId.Anchor;

    public byte[] ToContract(object domain) {
        var point = (Point)domain;
        var contract = new AnchorContract {
            SpinePosition = point.SpinePosition,
            Direction = point.Direction,
            Roll = point.Roll,
            // ...
        };
        return BinarySerializer.Serialize(contract);
    }

    public object FromContract(byte[] data) {
        var contract = BinarySerializer.Deserialize<AnchorContract>(data);
        return Point.Create(
            contract.SpinePosition,
            contract.Direction,
            contract.Roll,
            // ...
        );
    }
}

// Registry wires up all adapters
public static class AdapterRegistry {
    public static void RegisterAll(Registry registry) {
        registry.Register(new AnchorAdapter());
        registry.Register(new ForceSectionAdapter());
        // ...
    }
}
```

### Benefits

- **Domain-agnostic graph**: Graph layer is reusable, knows nothing about coasters
- **Self-describing format**: Type tags + opaque blobs, easy versioning
- **No manual flag management**: Add fields to contracts, serialization handles it
- **Type-safe ports**: Proper typed values instead of PointData field overloading
- **Identical Rust/C#**: Same three layers, same contracts
- **Easy migration**: V1 → V2 converter reads old format, writes new graph

## Rust FFI

Rust backend validated and outperforms Burst. Toggle via `USE_RUST_BACKEND` flag.

**Build**: `./build-rust.sh` (cross-platform)

## Continuous Track Buffer

Single continuous `DynamicBuffer<TrackPoint>` for entire track, enabling zero-copy aliasing across systems.

```csharp
public struct TrackPoint : IBufferElementData {
    public float3 Position;
    public float3 Direction;
    public float3 Normal;
    public float3 Lateral;
    public float Distance;
}
```

## Testing

**Run tests**: `./run-tests.sh` (all), `./run-tests.sh TestName` (filtered), `./run-tests.sh --rust-backend` (Rust)

## Next Steps

1. **Graph Core**: Implement generic graph structure (Burst-first)
2. **Graph Schema**: Define type IDs and data contracts
3. **Serialization Adapter**: Implement adapters for all node/port types
4. **V1 → V2 Migration**: One-way converter from legacy format
5. **Deprecate Legacy**: Remove old serialization after validation
