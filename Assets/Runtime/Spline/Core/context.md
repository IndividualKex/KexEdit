# KexEdit.Spline

Domain-agnostic spline point representation and interpolation.

## Purpose

- Define arc-parameterized spline points with orientation frame
- Binary search and interpolation along discretized spline data

## Layout

```
Spline/Core/
├── context.md
├── KexEdit.Spline.asmdef
├── SplinePoint.cs           # Arc-parameterized point with frame
└── SplineInterpolation.cs   # Binary search + interpolation
```

## Key Types

| Type | Purpose |
|------|---------|
| `SplinePoint` | Arc, Position, Direction, Normal, Lateral |
| `SplineInterpolation` | FindIndex, GetInterpolationFactor, Interpolate |

## Dependencies

- Unity.Mathematics, Unity.Burst, Unity.Collections
