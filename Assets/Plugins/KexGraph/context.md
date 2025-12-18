# KexGraph

Domain-agnostic graph library for Unity DOTS. Reusable for shader graphs, geometry nodes, or any directed acyclic graph system.

## Purpose

Generic graph data structure with nodes, ports, and edges. No domain knowledge. Consumers interpret type IDs and manage port data separately.

## Layout

```
KexGraph/
├── context.md                      # This file
├── KexGraph.asmdef                 # Assembly definition
├── Graph.cs                        # Graph struct with SoA layout
└── GraphSerializationExtensions.cs # Index map rebuild for deserialization
```

## Scope

**In-scope**
- Graph structure (nodes, ports, edges)
- Add/remove operations with O(1) lookup
- Traversal algorithms (topological sort, source/sink finding)
- Cycle detection and validation
- Burst-compatible, Rust-portable design

**Out-of-scope**
- Port data storage (consumer responsibility)
- Type validation (schema layer responsibility)
- Binary format and I/O (adapter layer responsibility)
- Domain-specific logic (KexEdit, shader graphs, etc.)

## Entrypoints

**Graph Lifecycle**
- `Graph.Create(Allocator)` - Creates new graph with explicit allocator

**Node Operations**
- `AddNode(uint nodeType, float2 position)` - Returns unique node ID
- `RemoveNode(uint nodeId)` - Swap-and-pop removal
- `TryGetNodeIndex(uint nodeId, out int index)` - O(1) lookup via hashmap

**Port Operations**
- `AddInputPort(uint nodeId, uint portType)` - Returns unique port ID
- `AddOutputPort(uint nodeId, uint portType)` - Returns unique port ID
- `GetInputPorts(uint nodeId, out NativeArray<uint>, Allocator)` - Caller owns memory
- `GetOutputPorts(uint nodeId, out NativeArray<uint>, Allocator)` - Caller owns memory
- `RemovePort(uint portId)` - Updates node port counts

**Edge Operations**
- `AddEdge(uint sourcePortId, uint targetPortId)` - Connects ports, returns edge ID
- `RemoveEdge(uint edgeId)` - Swap-and-pop removal
- `GetOutgoingEdges(uint nodeId, out NativeArray<uint>, Allocator)` - Edges from node
- `GetIncomingEdges(uint nodeId, out NativeArray<uint>, Allocator)` - Edges to node
- `TryGetEdgeIndex(uint edgeId, out int index)` - O(1) lookup via hashmap

**Traversal**
- `GetSuccessorNodes(uint nodeId, out NativeArray<uint>, Allocator)` - Nodes reachable via outgoing edges
- `GetPredecessorNodes(uint nodeId, out NativeArray<uint>, Allocator)` - Nodes with edges pointing here
- `FindSourceNodes(out NativeArray<uint>, Allocator)` - Nodes with no incoming edges
- `FindSinkNodes(out NativeArray<uint>, Allocator)` - Nodes with no outgoing edges
- `HasCycle()` - DFS-based cycle detection

**Serialization Support**
- `NextNodeId`, `NextPortId`, `NextEdgeId` - Public ID generators for serialization
- `NodeIndexMap`, `PortIndexMap`, `EdgeIndexMap` - Public for manual deserialization
- `RebuildIndexMaps()` - Rebuilds index maps after deserialization

## Dependencies

**Unity**
- `Unity.Collections` - NativeList, NativeHashMap
- `Unity.Mathematics` - float2
- `Unity.Burst` - [BurstCompile] attribute

**None** - Zero dependencies on KexEdit or domain logic
