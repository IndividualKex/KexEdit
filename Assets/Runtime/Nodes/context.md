# KexEdit.Nodes

Node schema layer - shared vocabulary for node types.

## Purpose

- `PortId` - graph connection identifiers (inputs/outputs between nodes)
- `PropertyId` - keyframe property identifiers (authored curve data)
- `NodeType` - 8 core node types
- `NodeSchema` - port/property contracts per node type (Burst-compiled static methods)
- `PropertyIndex` - serialization index ↔ PropertyId mapping
- `IterationConfig` - time/distance iteration params

## Layout

```
Nodes/
├── PortId.cs           # Graph ports (Anchor, Path, Duration, etc.)
├── PropertyId.cs       # Keyframe properties (RollSpeed, NormalForce, etc.)
├── NodeType.cs         # 8 node types
├── NodeSchema.cs       # Static schema queries
├── PropertyIndex.cs    # Serialization mapping
├── IterationConfig.cs
├── Anchor/             # AnchorNode - creates initial State from position/rotation
├── Bridge/             # BridgeNode - Bezier curve between anchors
├── CopyPath/           # CopyPathNode - copies/transforms existing path
├── Curved/             # CurvedNode - arc/helix sections
├── Force/              # ForceNode - FVD force-based track generation
├── Geometric/          # GeometricNode - pitch/yaw/roll-based generation
├── Reverse/            # ReverseNode - reverses anchor direction
└── ReversePath/        # ReversePathNode - reverses path order/orientation
```

## Legacy Types (Not in Refactor)

The old KexEdit solution had `Mesh` and `Append` node types. These were vestigial workarounds for UI/rendering concerns and do not belong in the clean core runtime. They are intentionally excluded from this refactored solution.

## Port Categories

**PortId** - Graph connections: `Anchor`, `Path`, `Duration`, `Radius`, `Arc`, etc.

**PropertyId** - Keyframe curves: `RollSpeed`, `NormalForce`, `LateralForce`, `PitchSpeed`, `YawSpeed`, `DrivenVelocity`, `HeartOffset`, `Friction`, `Resistance`, `TrackStyle`

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
