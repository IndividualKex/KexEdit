# Runtime Migration Plan

Rebuild KexEdit runtime with hexagonal architecture. Enables Rust/WASM migration and clean separation of concerns.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              INFRASTRUCTURE ADAPTERS                            │
│   Unity ECS systems, serialization, positioning, rendering     │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              NODE TYPES (KexEdit.Nodes.*)                       │
│   ForceNode, GeometricNode, CurvedNode, etc.                    │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              NODE SCHEMA (KexEdit.Nodes)                        │
│   NodeSchema, PortId, PropertyId, NodeType enums                │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│         GRAPH SCHEMA (KexEdit.Graph.Schema) - future            │
│   Type validation, domain-specific contracts                    │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│              GRAPH CORE (KexGraph) ✓ implemented                │
│   Generic graph: nodes, ports, edges, traversal, validation     │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│                  CORE (KexEdit.Core)                            │
│   Point, Frame, Curvature, Forces, Sim, Keyframe                │
└─────────────────────────────────────────────────────────────────┘
```

## Current State

### Implemented

**KexGraph** (`Assets/Plugins/KexGraph/`)
- Domain-agnostic graph library
- Structure of Arrays (SoA) layout with NativeList for dynamic capacity
- Raw uint IDs (not wrapped structs)
- Node operations: add, remove, lookup
- 14 passing tests

### Patterns

**ID System**: Raw `uint` values
- NodeId, PortId, EdgeId are all `uint`
- Monotonic generation: 1, 2, 3... (0 reserved for null/invalid)
- O(1) lookup via `NativeHashMap<uint, int>`

**Memory Layout**: Structure of Arrays
```csharp
public struct Graph {
    public NativeList<uint> NodeIds;
    public NativeList<uint> NodeTypes;        // Opaque - consumer interprets
    public NativeList<float2> NodePositions;
    public NativeList<int> NodeInputStart;    // Index into ports
    public NativeList<int> NodeInputCount;
    public NativeList<int> NodeOutputStart;
    public NativeList<int> NodeOutputCount;

    public NativeList<uint> PortIds;
    public NativeList<uint> PortTypes;        // Opaque - consumer interprets
    public NativeList<uint> PortOwners;       // NodeId
    public NativeList<bool> PortIsInput;

    public NativeList<uint> EdgeIds;
    public NativeList<uint> EdgeSources;      // PortId
    public NativeList<uint> EdgeTargets;      // PortId
}
```

**Allocators**: Explicit, caller-controlled
- `Graph.Create(Allocator allocator)`
- Tests use `Allocator.Temp`
- Runtime will use `Allocator.Persistent`

**Removal**: Swap-and-pop pattern
- O(1) removal keeps arrays compact
- Updates index map when swapping

## Design Principles

- **Graph is Domain-Agnostic**: No coaster knowledge; consumers interpret type IDs
- **Core is Physics-Only**: No graph, train, or section awareness
- **Hexagonal Architecture**: Graph has no dependencies; adapters bridge to domain
- **Single Source of Truth**: SoA buffers; no derived state
- **Testability**: TDD with headless Unity tests
- **Portability**: Rust-compatible design (Vec, HashMap, u32)

## Next Steps

### Phase 1: Complete KexGraph Core

**Port Operations** ✓ implemented
- `AddInputPort(uint nodeId, uint portType)` → uint portId
- `AddOutputPort(uint nodeId, uint portType)` → uint portId
- `RemovePort(uint portId)` - updates node port counts
- `GetInputPorts(uint nodeId, out NativeArray<uint>, Allocator)` - caller owns memory
- `GetOutputPorts(uint nodeId, out NativeArray<uint>, Allocator)` - caller owns memory

**Edge Operations**
- `AddEdge(uint sourcePortId, uint targetPortId)` → uint edgeId
- `RemoveEdge(uint edgeId)`
- `GetOutgoingEdges(uint nodeId)` → NativeArray<uint>
- `GetIncomingEdges(uint nodeId)` → NativeArray<uint>

**Traversal**
- `FindSourceNodes()` - nodes with no incoming edges
- `FindSinkNodes()` - nodes with no outgoing edges
- `TopologicalSort()` - Kahn's algorithm for execution order

**Validation**
- `HasCycle()` - DFS-based cycle detection
- `Validate()` - returns ValidationResult with errors

**Performance**
- Burst-compiled jobs for hot paths
- Benchmarks: <1ms for 1000 node operations

### Phase 2: Graph Schema Layer

**Type System** (`Assets/Runtime/Graph.Schema/`)
- NodeTypeId, PortTypeId enums specific to KexEdit
- Type validation rules (what connects to what)
- Adapter traits for serialization

### Phase 3: Integration

**ECS Adapter** (`Assets/Runtime/Legacy/Track/`)
- Convert KexGraph → existing Node/Port/Connection entities
- Convert entities → KexGraph
- Run both systems in parallel, verify identical results

**Serialization Adapter** (`Assets/Runtime/Serialization/`)
- Load V1 format → KexGraph
- Save KexGraph → V2 format
- Migration tool for existing tracks

### Phase 4: Rust Port

**Rust Implementation** (`rust-backend/graph-core/`)
- Direct translation: `Vec<u32>`, `HashMap<u32, usize>`
- FFI layer for Unity interop
- Performance validation vs Burst

## Directory Structure

```
KexEdit/
├── Assets/
│   ├── Plugins/
│   │   └── KexGraph/              Generic graph library ✓
│   │       ├── KexGraph.asmdef
│   │       └── Graph.cs
│   ├── Runtime/
│   │   ├── Core/                  Physics/math primitives
│   │   ├── Nodes/                 Node schema + implementations
│   │   └── Legacy/                ECS adapters and existing code
│   └── Tests/
│       ├── GraphStructureTests.cs ✓
│       └── GraphNodeTests.cs      ✓
└── rust-backend/                  Future Rust port
    ├── graph-core/
    ├── kexedit-core/
    ├── kexedit-nodes/
    └── kexedit-ffi/
```

## Testing Strategy

**TDD Workflow**: Red-Green-Refactor
1. Write failing test first
2. Implement minimal code to pass
3. Refactor and optimize
4. Run `./run-tests.sh` to verify

**Test Categories**
- `[Category("Unit")]` - Pure logic, no ECS
- `[Category("Performance")]` - Burst-compiled benchmarks

**Current Coverage**: 31 tests passing
- Graph structure creation/disposal
- Node add/remove/lookup operations
- Port add/remove/lookup operations
