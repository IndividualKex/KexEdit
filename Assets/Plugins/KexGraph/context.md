# KexGraph

Domain-agnostic graph library for Unity DOTS. Reusable for shader graphs, geometry nodes, or any directed acyclic graph system.

## Purpose

Generic graph data structure with nodes, ports, and edges. No domain knowledge. Consumers interpret type IDs and manage port data separately.

## Layout

```
KexGraph/
├── context.md           # This file
├── KexGraph.asmdef      # Assembly definition
└── Graph.cs             # Graph struct with SoA layout
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
- Serialization (adapter layer responsibility)
- Domain-specific logic (KexEdit, shader graphs, etc.)

## Entrypoints

`Graph.Create(Allocator)` - Creates new graph with explicit allocator
`AddNode(uint nodeType, float2 position)` - Returns unique node ID
`RemoveNode(uint nodeId)` - Swap-and-pop removal
`TryGetNodeIndex(uint nodeId, out int index)` - O(1) lookup via hashmap

## Dependencies

**Unity**
- `Unity.Collections` - NativeList, NativeHashMap
- `Unity.Mathematics` - float2
- `Unity.Burst` - [BurstCompile] attribute

**None** - Zero dependencies on KexEdit or domain logic
