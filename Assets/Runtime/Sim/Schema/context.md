# KexEdit.Sim.Schema

Node schema layer - shared vocabulary for node types.

## Purpose

- `NodeType` - 10 node types (Scalar/Vector leaf nodes + 8 track nodes)
- `PortSpec` - port specification: `(PortDataType, LocalIndex)` tuple
- `PortDataType` - four data types: Scalar, Vector, Anchor, Path
- `PropertyId` - keyframe property identifiers
- `NodeSchema` - port/property contracts per node type (Burst-compiled)
- `PropertyIndex` - serialization index ↔ PropertyId mapping

## Layout

```
Sim/Schema/
├── NodeType.cs         # 10 node types (Scalar, Vector first)
├── PortSpec.cs         # Port specification struct (DataType + LocalIndex)
├── PortDataType.cs     # Four port data types
├── PropertyId.cs       # Keyframe properties
├── NodeSchema.cs       # Static schema queries (InputSpec, OutputSpec, etc.)
├── PropertyIndex.cs    # Serialization mapping
|── KeyframeStore.cs    # (nodeId, propertyId) → Keyframe[] storage
├── Anchor/             # Creates initial state from position/rotation
├── Bridge/             # Bezier curve between anchors
├── CopyPath/           # Copies/transforms existing path
├── Curved/             # Arc/helix sections
├── Force/              # FVD force-based track generation
├── Geometric/          # Pitch/yaw/roll-based generation
├── Reverse/            # Reverses anchor direction
└── ReversePath/        # Reverses path order/orientation
```

## Node Types

**Leaf nodes** (value injection):
- `Scalar` - outputs generic float, connects to any scalar input
- `Vector` - outputs generic float3, connects to any vector input

**Track nodes**: Force, Geometric, Curved, CopyPath, Bridge, Anchor, Reverse, ReversePath

## Port System

Ports are identified by `PortSpec(PortDataType, LocalIndex)`:
- **PortDataType**: Scalar(0), Vector(1), Anchor(2), Path(3)
- **LocalIndex**: 0-based index within ports of the same data type

Example: Curved node has inputs `[Anchor0, Scalar0, Scalar1, Scalar2, Scalar3, Scalar4]`

**PropertyId** - Keyframe curves: RollSpeed, NormalForce, LateralForce, PitchSpeed, YawSpeed, DrivenVelocity, HeartOffset, Friction, Resistance, TrackStyle

## Serialization Index Mapping

Force and Geometric share indices 1-2 with different semantics:

| Index | Force | Geometric |
|-------|-------|-----------|
| 1 | NormalForce | PitchSpeed |
| 2 | LateralForce | YawSpeed |

Use `PropertyIndex.ToIndex()`/`FromIndex()` for mapping.

## Dependencies

- KexEdit.Sim, Unity.Collections, Unity.Mathematics, Unity.Burst

Node types must NOT depend on each other.
