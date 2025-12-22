# KexEdit.Core

Core domain layer - pure physics and math with no external dependencies.

## Purpose

- Atomic math primitives for roller coaster simulation
- Data contracts (ports) for node adapters
- Keyframe curve evaluation
- Portable: designed for Rust/WASM migration

## Layout

```
Core/
├── Sim.cs              # Constants (G, HZ, DT) and pure energy functions
├── Point.cs            # Complete point state: geometry + physics + iteration data
├── Frame.cs            # Orthonormal basis with rotation methods
├── Curvature.cs        # Angular change between frames
├── Forces.cs           # G-force computation from curvature
├── Keyframe.cs         # Keyframe data structure (input contract)
├── KeyframeEvaluator.cs # Bezier curve evaluation
├── PhysicsParams.cs    # Per-step physics inputs (heartOffset, friction, roll)
├── FrameChange.cs      # Frame transform builders (FromAngles, FromAxis)
└── Articulation/       # Spline-based body positioning (see subfolder context.md)
```

## Key Types

| Type | Purpose |
|------|---------|
| `Point` | Complete state: position, frame, forces, energy, arc lengths, SpineAdvance |
| `Frame` | Orthonormal basis (Direction, Normal, Lateral) with rotation methods |
| `PhysicsParams` | HeartOffset, Friction, Resistance, DeltaRoll, Driven |

## Frame Methods

| Method | Purpose |
|--------|---------|
| `RotateAround(axis, angle)` | Atomic rotation around arbitrary axis |
| `WithRoll(deltaRoll)` | Rotate around Direction |
| `WithPitch(deltaPitch)` | Rotate around horizontal axis |
| `WithYaw(deltaYaw)` | Rotate around world up |
| `FromDirectionAndRoll` | Construct from direction vector |
| `FromEuler` | Construct from euler angles |

## Constants (Sim)

| Constant | Value | Description |
|----------|-------|-------------|
| `G` | 9.80665 | Gravity (m/s²) |
| `HZ` | 100 | Simulation rate (Hz) |
| `DT` | 0.01 | Time step (1/HZ) |
| `MIN_VELOCITY` | 1e-3 | Minimum velocity threshold (m/s) |

## Naming Conventions

- `HeartPosition` / `SpinePosition` - Rider center vs track rail position
- `HeartArc` / `SpineArc` - Cumulative path lengths (rider vs track)
- `SpineAdvance` - Per-step distance traveled along spine

## Entrypoints

- `KeyframeEvaluator.Evaluate(keyframes, t, defaultValue)` - Bezier interpolation
- `Curvature.FromFrames(curr, prev)` + `Forces.Compute(...)` - G-force calculation
- `Frame.RotateAround/WithPitch/WithYaw/WithRoll` - Frame transformations

## Dependencies

- Unity.Mathematics, Unity.Burst, Unity.Collections
