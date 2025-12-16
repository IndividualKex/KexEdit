# KexEdit.Nodes

Node schema layer - shared vocabulary for node types.

## Purpose

- `NodeType` - 10 node types (Scalar/Vector leaf nodes + 8 track nodes)
- `PortId` - graph port identifiers (Scalar/Vector generic + typed ports)
- `PropertyId` - keyframe property identifiers
- `NodeSchema` - port/property contracts per node type (Burst-compiled)
- `PropertyIndex` - serialization index ↔ PropertyId mapping
- `IterationConfig` - time/distance iteration params

## Layout

```
Nodes/
├── NodeType.cs         # 10 node types (Scalar, Vector first)
├── PortId.cs           # Graph ports (Scalar, Vector, Anchor, Path, etc.)
├── PropertyId.cs       # Keyframe properties
├── NodeSchema.cs       # Static schema queries
├── PropertyIndex.cs    # Serialization mapping
├── IterationConfig.cs
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

## Port Categories

**PortId** types map to **PortDataType** categories:
- `Scalar` → any scalar port (Duration, Radius, Arc, etc.)
- `Vector` → any vector port (Position, Rotation)
- `Anchor` → Anchor ports only
- `Path` → Path ports only

**PropertyId** - Keyframe curves: RollSpeed, NormalForce, LateralForce, PitchSpeed, YawSpeed, DrivenVelocity, HeartOffset, Friction, Resistance, TrackStyle

## Serialization Index Mapping

Force and Geometric share indices 1-2 with different semantics:

| Index | Force | Geometric |
|-------|-------|-----------|
| 1 | NormalForce | PitchSpeed |
| 2 | LateralForce | YawSpeed |

Use `PropertyIndex.ToIndex()`/`FromIndex()` for mapping.

## Dependencies

- KexEdit.Core, Unity.Mathematics, Unity.Burst

Node types must NOT depend on each other.
