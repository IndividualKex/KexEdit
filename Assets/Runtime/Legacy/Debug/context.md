# KexEdit.Legacy.Debug

Debug visualization for track and train data.

## Purpose

- Draw track segments in Scene view using Debug.DrawLine
- Visualize train CoM via TrackSingleton + SimFollowerSingleton

## Layout

```
Legacy/Debug/
├── context.md
├── KexEdit.Legacy.Debug.asmdef
├── SegmentationGizmos.cs  # Draws segments with color coding (uses TrackSingleton)
└── TrainGizmos.cs         # Reads singletons, draws car frames
```

## Dependencies

- KexEdit.Trains.Sim (SimFollowerLogic, TrainCarLogic)
- KexEdit.Legacy (TrackSingleton, SimFollowerSingleton)
- KexEdit.Spline, KexEdit.Spline.Rendering
- Unity.Entities
