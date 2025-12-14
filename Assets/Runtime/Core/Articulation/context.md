# KexEdit.Core.Articulation

Generic spline-based articulation positioning - pure math with no external dependencies.

## Purpose

- Position anchors along discretized spline data
- Compute body transforms from anchor positions
- Portable: same patterns as kexedit-core Rust backend

## Layout

```
Articulation/
├── SplinePoint.cs          # Input: discretized spline point with frame
├── AnchorOffset.cs         # Definition: arc offset + local 3D offset
├── Anchor.cs               # Output: positioned anchor on spline
├── BodyTransform.cs        # Output: body position and rotation
├── SplineInterpolation.cs  # Binary search + interpolation
├── AnchorPositioning.cs    # Position anchors on spline
└── BodyPositioning.cs      # Compute body transform from anchors
```

## Key Types

| Type | Purpose |
|------|---------|
| `SplinePoint` | Input: Arc, Position, Direction, Normal, Lateral |
| `AnchorOffset` | Definition: Arc offset + Local 3D offset |
| `Anchor` | Output: Positioned anchor with frame |
| `BodyTransform` | Output: Position + Rotation |

## Entrypoints

- `AnchorPositioning.Position(spline, arc)` - Position anchor at arc
- `BodyPositioning.FromAnchors(leading, trailing, pivot)` - Body between anchors

## Dependencies

- Unity.Mathematics, Unity.Burst, Unity.Collections
