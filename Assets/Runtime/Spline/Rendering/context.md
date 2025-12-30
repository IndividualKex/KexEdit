# KexEdit.Spline.Rendering

Track segment computation for mesh-based rendering.

## Purpose

- Compute segment boundaries from arc-length data
- Uniform segment distribution with scale factors for GPU mesh deformation

## Layout

```
Spline/Rendering/
├── context.md
├── KexEdit.Spline.Rendering.asmdef
├── SegmentBoundary.cs
└── SegmentationMath.cs
```

## Scope

- In: Segment boundary math, scale computation
- Out: ECS components (see Legacy/Track), GPU shaders, mesh loading

## Entrypoints

- `SegmentationMath.ComputeSegments` — called by Legacy `SegmentationSystem`

## Dependencies

- KexEdit.Spline
