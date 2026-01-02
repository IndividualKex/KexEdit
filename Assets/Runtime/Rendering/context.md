# KexEdit.Rendering

GPU rendering shell with multi-style support. Manages buffers and dispatches rendering.

## Purpose

- GPU buffer lifecycle (ComputeBuffer, GraphicsBuffer)
- Compute shader dispatch and indirect rendering
- Style breakpoint detection from keyframes

## Layout

```
Rendering/
├── PieceMesh.cs               # Mesh data container (mesh, nominal length)
├── TrackMeshPipeline.cs       # GPU shell (buffers, dispatch, render)
└── StyleBreakpointDetector.cs # Detect style regions from keyframes (Burst)
```

## Scope

- In: GPU buffer management, compute dispatch, rendering, keyframe-based style detection
- Out: ECS systems, resource loading, shader globals

## Dependencies

- KexEdit.Spline.Rendering (SegmentBuilder, GPU structs, StyleBreakpoint)
- KexEdit.Track (Track data structure)
- KexEdit.Sim.Schema (KeyframeStore, PropertyId, VisualizationMode)
