# KexEdit.Spline.Resampling

Adapter layer converting Point arrays to SplinePoint arrays with configurable resampling.

## Purpose

- Convert Point (FVD domain) to SplinePoint (spline domain)
- Support direct 1:1 conversion or arc-length uniform resampling

## Layout

```
Spline/Resampling/
├── context.md
├── KexEdit.Spline.Resampling.asmdef
└── SplineResampler.cs
```

## Key Types

| Type | Purpose |
|------|---------|
| `SplineResampler` | Static Burst functions for Point → SplinePoint conversion |

## API

```csharp
// Direct 1:1 conversion
SplineResampler.Resample(in NativeArray<Point> points, ref NativeList<SplinePoint> output);

// Arc-length uniform resampling
SplineResampler.Resample(in NativeArray<Point> points, float resolution, ref NativeList<SplinePoint> output);
```

## Dependencies

- KexEdit.Sim (Point)
- KexEdit.Spline (SplinePoint)
- Unity.Mathematics, Unity.Burst, Unity.Collections
