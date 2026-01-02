# KexEdit.Trains.Sim

Center-of-mass traversal through built track.

## Purpose

- SimFollower: traversal state (TraversalIndex, PointIndex, Facing)
- SimFollowerLogic: advance CoM at 100Hz, interpolate Points, seek via progress

## Layout

```
Sim/
├── context.md
├── KexEdit.Trains.Sim.asmdef
├── SimFollower.cs       # Traversal state struct
└── SimFollowerLogic.cs  # Advance, GetCurrentPoint, SetFromProgress, GetProgress
```

## Dependencies

- KexEdit.Sim (Point)
- KexEdit.Track (Track)
- Unity.Burst, Unity.Mathematics
