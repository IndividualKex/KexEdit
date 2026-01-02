# KexEdit.Trains

Domain layer for train traversal and car positioning.

## Purpose

- CoM traversal through Track sections (Trains.Sim)
- Car/seat positioning with overhang via recursive section link following

## Layout

```
Trains/
├── context.md
├── KexEdit.Trains.asmdef
├── TrainCarLogic.cs           # Position at arc offset with overhang
└── Sim/                       # Sub-assembly: CoM traversal
    ├── KexEdit.Trains.Sim.asmdef
    ├── SimFollower.cs         # TraversalIndex + PointIndex + Facing
    └── SimFollowerLogic.cs    # Advance, GetCurrentPoint, SetFromProgress
```

## Entrypoints

- `TrainCarLogic.TryGetSplinePoint(follower, track, offset)` - Get facing-adjusted SplinePoint at offset from CoM
- `SimFollowerLogic.Advance(follower, track, dt, hz)` - Step CoM forward through track

## Dependencies

- KexEdit.Trains.Sim (SimFollower, traversal)
- KexEdit.Sim (Point)
- KexEdit.Track (Track, Section)
- KexEdit.Spline (SplinePoint)
