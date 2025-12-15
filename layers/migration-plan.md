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

-   **Core is Physics-Only**: No awareness of train direction, section boundaries, or friction resets
-   **Graph is Domain-Agnostic**: Pure structure with opaque data blobs, no type awareness
-   **Nodes Own State Management**: Node types control initial state for each section
-   **Hexagonal Architecture**: Core has no dependencies; adapters use core ports
-   **Single Source of Truth**: Continuous buffers; systems interpret as needed
-   **Testability**: Every layer has comprehensive tests before adding outer layers
-   **Portability**: Architecture designed for Rust/WASM port

## Graph + Serialization Architecture

### Layer 1: Graph Core (Pure, Reusable)

Generic graph structure with opaque data. No type awareness.

```csharp
// KexEdit.Graph - completely domain-agnostic
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile]
public readonly struct NodeId {
    public readonly uint Value;
    public NodeId(uint value) => Value = value;
}

[BurstCompile]
public readonly struct PortId {
    public readonly uint Value;
    public PortId(uint value) => Value = value;
}

[BurstCompile]
public readonly struct EdgeId {
    public readonly uint Value;
    public EdgeId(uint value) => Value = value;
}

[BurstCompile]
public readonly struct Node {
    public readonly NodeId Id;
    public readonly float2 Position;
    public readonly NativeArray<byte> Data;
    public readonly NativeArray<PortId> InputPorts;
    public readonly NativeArray<PortId> OutputPorts;

    public Node(NodeId id, float2 position, NativeArray<byte> data,
                NativeArray<PortId> inputPorts, NativeArray<PortId> outputPorts) {
        Id = id;
        Position = position;
        Data = data;
        InputPorts = inputPorts;
        OutputPorts = outputPorts;
    }
}

[BurstCompile]
public readonly struct Port {
    public readonly PortId Id;
    public readonly NativeArray<byte> Data;

    public Port(PortId id, NativeArray<byte> data) {
        Id = id;
        Data = data;
    }
}

[BurstCompile]
public readonly struct Edge {
    public readonly EdgeId Id;
    public readonly PortId Source;
    public readonly PortId Target;

    public Edge(EdgeId id, PortId source, PortId target) {
        Id = id;
        Source = source;
        Target = target;
    }
}

public struct Graph {
    public NativeArray<Node> Nodes;
    public NativeArray<Port> Ports;
    public NativeArray<Edge> Edges;
    public NativeArray<byte> Metadata;

    public void Dispose() {
        if (Nodes.IsCreated) Nodes.Dispose();
        if (Ports.IsCreated) Ports.Dispose();
        if (Edges.IsCreated) Edges.Dispose();
        if (Metadata.IsCreated) Metadata.Dispose();
    }
}
```

### Layer 2: Graph Schema (KexEdit-specific bindings)

Maps semantic types to graph blobs. Defines data contracts and adapter traits.

```csharp
// KexEdit.Graph.Schema - project-specific type mappings
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

public enum NodeTypeId : byte {
    Anchor = 1,
    ForceSection = 2,
    GeometricSection = 3,
}

public enum PortTypeId : byte {
    Anchor = 1,
    Path = 2,
    Float = 3,
    Float3 = 4,
}

[BurstCompile]
public readonly struct AnchorContract {
    public readonly float3 SpinePosition;
    public readonly float3 Direction;
    public readonly float Roll;
    public readonly float Velocity;
    public readonly float HeartOffset;
    public readonly float Friction;
    public readonly float Resistance;

    public AnchorContract(in float3 spinePosition, in float3 direction, float roll,
                          float velocity, float heartOffset, float friction, float resistance) {
        SpinePosition = spinePosition;
        Direction = direction;
        Roll = roll;
        Velocity = velocity;
        HeartOffset = heartOffset;
        Friction = friction;
        Resistance = resistance;
    }

    public static AnchorContract FromPoint(in Point point) {
        return new AnchorContract(
            point.SpinePosition,
            point.Direction,
            point.Roll,
            point.Velocity,
            point.HeartOffset,
            point.Friction,
            point.Resistance
        );
    }

    public Point ToPoint() {
        return Point.Create(
            SpinePosition,
            Direction,
            Roll,
            Velocity,
            HeartOffset,
            Friction,
            Resistance
        );
    }
}

public interface INodeAdapter<T> where T : unmanaged {
    NodeTypeId TypeId { get; }
    void Serialize(in T value, ref NativeArray<byte> output, Allocator allocator);
    T Deserialize(in NativeArray<byte> data);
}
```

### Layer 3: Serialization Adapter (Implementations)

Dedicated assembly that implements adapters for all serializable types.

```csharp
// KexEdit.Serialization - converts domain ↔ contracts
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using KexEdit.Core;
using KexEdit.Graph.Schema;

[BurstCompile]
public readonly struct AnchorAdapter : INodeAdapter<Point> {
    public NodeTypeId TypeId => NodeTypeId.Anchor;

    [BurstCompile]
    public void Serialize(in Point point, ref NativeArray<byte> output, Allocator allocator) {
        var contract = AnchorContract.FromPoint(point);
        int size = UnsafeUtility.SizeOf<AnchorContract>();

        if (!output.IsCreated || output.Length != size) {
            if (output.IsCreated) output.Dispose();
            output = new NativeArray<byte>(size, allocator);
        }

        unsafe {
            fixed (AnchorContract* ptr = &contract) {
                UnsafeUtility.MemCpy(output.GetUnsafePtr(), ptr, size);
            }
        }
    }

    [BurstCompile]
    public Point Deserialize(in NativeArray<byte> data) {
        unsafe {
            AnchorContract contract;
            UnsafeUtility.MemCpy(&contract, data.GetUnsafeReadOnlyPtr(),
                                 UnsafeUtility.SizeOf<AnchorContract>());
            return contract.ToPoint();
        }
    }
}

[BurstCompile]
public static class BurstSerializer {
    [BurstCompile]
    public static void SerializePoint(in Point point, ref NativeArray<byte> output, Allocator allocator) {
        var adapter = new AnchorAdapter();
        adapter.Serialize(point, ref output, allocator);
    }

    [BurstCompile]
    public static Point DeserializePoint(in NativeArray<byte> data) {
        var adapter = new AnchorAdapter();
        return adapter.Deserialize(data);
    }
}
```

### Benefits

-   **Domain-agnostic graph**: Graph layer is reusable, knows nothing about coasters
-   **Self-describing format**: Type tags + opaque blobs, easy versioning
-   **No manual flag management**: Add fields to contracts, serialization handles it
-   **Type-safe ports**: Proper typed values instead of PointData field overloading
-   **Identical Rust/C#**: Same three layers, same contracts
-   **Easy migration**: V1 → V2 converter reads old format, writes new graph

## Rust FFI

Rust backend validated and outperforms Burst. Toggle via `USE_RUST_BACKEND` flag.

**Build**: `./build-rust.sh` (cross-platform)

## Testing

**Run tests**: `./run-tests.sh` (all), `./run-tests.sh TestName` (filtered), `./run-tests.sh --rust-backend` (Rust)

## Next Steps

1. **Graph Core**: Implement generic graph structure (Burst-first)
2. **Graph Schema**: Define type IDs and data contracts
3. **Serialization Adapter**: Implement adapters for all node/port types
4. **V1 → V2 Migration**: One-way converter from legacy format
5. **Deprecate Legacy**: Remove old serialization after validation
