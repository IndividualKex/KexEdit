# KexEdit.Legacy.Debug

Debug visualization for spline data.

## Purpose

- Draw spline paths in Scene view using Debug.DrawLine

## Layout

```
Legacy/Debug/
├── context.md
├── KexEdit.Legacy.Debug.asmdef
└── SplineGizmos.cs    # SystemBase that draws SplineBuffer
```

## Dependencies

- KexEdit.Spline (SplinePoint)
- KexEdit.Legacy (SplineBuffer)
- Unity.Entities
