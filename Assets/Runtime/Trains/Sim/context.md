# KexEdit.Trains.Sim

Center-of-mass traversal through built track.

## Purpose

- SimFollower: traversal state (TraversalIndex, PointIndex, Facing)
- SimFollowerLogic: advance CoM at simulation rate (100Hz), interpolate between Points

## Layout

```
Sim/
├── context.md
├── KexEdit.Trains.Sim.asmdef
├── SimFollower.cs       # Traversal state
└── SimFollowerLogic.cs  # Advance/interpolate logic
```

## Dependencies

- KexEdit.Sim (Point)
- KexEdit.Track (Track)
- Unity.Burst, Unity.Mathematics
