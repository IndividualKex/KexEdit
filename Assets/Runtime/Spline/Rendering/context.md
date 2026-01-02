# KexEdit.Spline.Rendering

Track segmentation with multi-style support and GPU mesh deformation.

## Purpose

- Multi-style piece configuration (flattened arrays with style ranges)
- Segment boundaries from arc-length (tolerance-based piece selection)
- GPU segment data building (pure Burst transforms)

## Layout

```
Spline/Rendering/
├── StylePieceConfig.cs     # Multi-style piece config (native arrays, ranges)
├── StyleBreakpoint.cs      # Style region (section, arc range, style index)
├── TrackPiece.cs           # Piece definition (nominal length, mesh index)
├── SegmentBoundary.cs      # Segment arc bounds, scale, piece index
├── SegmentationMath.cs     # Segment computation (single/multi-piece)
├── SegmentBuilder.cs       # Track + breakpoints → GPU segments (Burst)
├── RenderStyle.cs          # Rendering contract (colors)
├── GPUSplinePoint.cs       # GPU spline point (52 bytes)
├── GPUSegmentBoundary.cs   # GPU segment (48 bytes, includes SectionIndex)
└── Deform.cs               # CPU mesh deformation
```

## Mesh Authoring

- Origin at segment START, extends +Z (0 to +length)
- Axes: +X lateral, +Y up, +Z forward
- Filename: `<style>_<length>m.obj` (e.g., `modern_light_10m.obj`)
- Blender export: Forward -Y, Up Z

## Scope

- In: Segment math, style config, GPU structs, segment building
- Out: ECS systems, mesh loading, GPU buffer management, keyframe detection

## Dependencies

- KexEdit.Spline
- KexEdit.Track
