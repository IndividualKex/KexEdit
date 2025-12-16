# KexEdit.NodeGraph

KexEdit-aware extension layer bridging the domain-agnostic KexGraph with the node schema.

## Purpose

Extends `Graph` with type-safe operations using `NodeType` and `PortId` enums. Validates connections per schema rules. Burst-compatible and Rust-portable.

## Layout

```
NodeGraph/
├── context.md                   # This file
├── KexEdit.NodeGraph.asmdef     # Assembly definition
├── TypedGraphExtensions.cs      # Type-safe node creation and lookups
├── ConnectionValidator.cs       # Schema-compliant connection validation
├── ValidationError.cs           # Connection validation error codes
└── PortDataType.cs              # Port data type classification and defaults
```

## Scope

**In-scope**
- Type-safe node creation with automatic port wiring
- Port lookup by `PortId` type
- Connection validation (category-based: Scalar↔Scalar, Vector↔Vector, etc.)
- Cascade node removal (removes ports and edges)
- Port data category classification (`PortDataType`)

**Out-of-scope**
- Port data storage (consumer responsibility)
- Property/keyframe handling (see KexEdit.Nodes)
- Serialization (see PLAN.md)
- ECS integration (see Legacy)

## API (Extension Methods on Graph)

**Typed Node Operations**
- `graph.CreateNode(NodeType, float2, out inputs, out outputs, Allocator)` - Create node with schema-defined ports
- `graph.TryGetNodeType(uint nodeId, out NodeType)` - Get node's type
- `graph.TryGetPortType(uint portId, out PortId)` - Get port's type
- `graph.TryGetInputPort(uint nodeId, PortId, out uint portId)` - Find input port by type
- `graph.TryGetOutputPort(uint nodeId, PortId, out uint portId)` - Find output port by type
- `graph.RemoveNodeCascade(uint nodeId)` - Remove node, ports, and connected edges

**Connection Validation**
- `graph.ValidateConnection(uint sourcePortId, uint targetPortId, out ValidationError) → bool` - Check connection validity
- `graph.AddValidatedEdge(uint sourcePortId, uint targetPortId, out ValidationError) → uint` - Create edge if valid
- `graph.ValidateAllEdges(out int firstInvalidEdgeIndex) → bool` - Validate all edges

**PortId Extensions**
- `portId.DataType()` - Returns PortDataType (Scalar/Vector/Anchor/Path)
- `portId.DefaultValue()` - Default value for scalar ports

## Dependencies

- `KexGraph` - Domain-agnostic graph struct
- `KexEdit.Nodes` - NodeSchema, NodeType, PortId, PropertyId
- `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`
