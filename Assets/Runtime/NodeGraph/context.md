# KexEdit.NodeGraph

KexEdit-aware extension layer bridging the domain-agnostic KexGraph with the node schema.

## Purpose

Extends `Graph` with type-safe operations using `NodeType` and `PortSpec`. Validates connections per schema rules. Burst-compatible and Rust-portable.

## Layout

```
NodeGraph/
├── context.md                   # This file
├── KexEdit.NodeGraph.asmdef     # Assembly definition
├── TypedGraphExtensions.cs      # Type-safe node creation and lookups
├── ConnectionValidator.cs       # Schema-compliant connection validation
└── ValidationError.cs           # Connection validation error codes
```

## Scope

**In-scope**
- Type-safe node creation with automatic port wiring
- Port lookup by index or `(PortDataType, LocalIndex)`
- Connection validation (data-type-based: Scalar↔Scalar, etc.)
- Cascade node removal (removes ports and edges)

**Out-of-scope**
- Port data storage (consumer responsibility)
- Property/keyframe handling (see KexEdit.Nodes)
- Serialization (see KexEdit.Persistence)
- ECS integration (see Legacy)

## API (Extension Methods on Graph)

**Typed Node Operations**
- `graph.CreateNode(NodeType, float2, out inputs, out outputs, Allocator)` - Create node with schema-defined ports
- `graph.TryGetNodeType(uint nodeId, out NodeType)` - Get node's type
- `graph.TryGetPortSpec(uint portId, out PortSpec)` - Get port's spec
- `graph.TryGetInput(uint nodeId, int index, out uint portId)` - Get input port by index
- `graph.TryGetOutput(uint nodeId, int index, out uint portId)` - Get output port by index
- `graph.TryGetInputBySpec(uint nodeId, PortDataType, int localIndex, out uint portId)` - Find input by spec
- `graph.TryGetOutputBySpec(uint nodeId, PortDataType, int localIndex, out uint portId)` - Find output by spec
- `graph.RemoveNodeCascade(uint nodeId)` - Remove node, ports, and connected edges

**Connection Validation**
- `graph.ValidateConnection(uint sourcePortId, uint targetPortId, out ValidationError)` - Check connection validity
- `graph.AddValidatedEdge(uint sourcePortId, uint targetPortId, out ValidationError)` - Create edge if valid

## Dependencies

- `KexGraph` - Domain-agnostic graph struct
- `KexEdit.Nodes` - NodeSchema, NodeType, PortSpec, PortDataType
- `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`
