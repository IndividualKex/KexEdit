# KexEdit.Trains

Domain layer for train traversal and car positioning.

## Purpose

- CoM traversal through Track segments (Trains.Sim)
- Car positioning with overhang via recursive section link following

## Layout

```
Trains/
├── context.md
├── KexEdit.Trains.asmdef
├── TrainCarLogic.cs           # Position car at arc offset with overhang
└── Sim/                       # Sub-assembly: CoM traversal
    ├── KexEdit.Trains.Sim.asmdef
    ├── SimFollower.cs         # SegmentIndex + PointIndex + Facing
    └── SimFollowerLogic.cs    # Advance through segments
```

## Dependencies

- KexEdit.Trains.Sim (SimFollower, segment traversal)
- KexEdit.Coaster (Track, Segment)
- KexEdit.Spline (SplinePoint)
