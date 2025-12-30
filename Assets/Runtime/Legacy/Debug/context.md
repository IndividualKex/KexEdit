# KexEdit.Legacy.Debug

Debug visualization for spline and train data.

## Purpose

- Draw spline paths in Scene view using Debug.DrawLine
- Visualize train center-of-mass following sim graph

## Layout

```
Legacy/Debug/
├── context.md
├── KexEdit.Legacy.Debug.asmdef
├── SplineGizmos.cs        # Draws SplineBuffer as lines
├── SegmentationGizmos.cs  # Draws segments with color coding
└── TrainGizmos.cs         # CoM frame following sim graph via Trains layer
```

## Dependencies

- KexEdit.Trains (SimFollowerLogic)
- KexEdit.Coaster (CoasterEvaluator)
- KexEdit.Spline
- Unity.Entities
