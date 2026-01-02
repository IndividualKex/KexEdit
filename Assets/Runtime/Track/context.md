# KexEdit.Track

Built track output from Document evaluation.

## Purpose

- Single source of truth for all built track data
- Contains simulation points, spline points, sections, and traversal order
- Portable (no Unity dependencies)

## Key Types

- `Track` - Built track struct containing all data
  - `Points[]` - Simulation points (physics, forces, velocity)
  - `SplinePoints[]` - Arc-parameterized geometry (position, direction, normal, lateral)
  - `SplineData[]` - Physics values at spline positions (velocity, normalForce, lateralForce, rollSpeed)
  - `Sections[]` - Per-node section boundaries and continuations
  - `TraversalOrder[]` - Playable section sequence for trains
- `Section` - Point/spline ranges, arc bounds, next/prev links, style index
- `SectionLink` - Continuation between sections (for train traversal)

## Entrypoints

- `Track.Build(Document, Allocator, splineResolution, defaultStyleIndex)` - Builds complete track from Document
- `Track.SamplePoint(section, arc)` - Sample physics point at arc position
- `Track.SampleFromSpline(section, arc)` - Sample from precomputed spline

## Dependencies

- KexEdit.Document
- KexEdit.Sim (Point, Frame)
- KexEdit.Spline (SplinePoint, SplineResampler)
- KexEdit.Graph (topological sort)
