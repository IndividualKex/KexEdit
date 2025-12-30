# Runtime Context

Nested onion/hexagonal architecture for coaster track computation.

## Layout

```
Runtime/
├── Sim/              # Hex Core 1 + Layers
│   ├── Core/         # (KexEdit.Sim) FVD physics/math
│   ├── Schema/       # (KexEdit.Sim.Schema) Node types, ports
│   └── Nodes/        # (KexEdit.Sim.Nodes.*) Node implementations
├── Graph/            # Hex Core 2 + Layers
│   ├── Core/         # (KexEdit.Graph) Generic DAG
│   └── Typed/        # (KexEdit.Graph.Typed) Type-safe ops
├── Spline/           # Hex Core 3 + Layers
│   ├── Core/         # (KexEdit.Spline) Arc-length spline
│   └── Resampling/   # (KexEdit.Spline.Resampling) Point → SplinePoint
├── App/              # Track Backend (top)
│   ├── Coaster/      # Aggregate root + evaluator
│   └── Persistence/  # Save/load format
├── Trains/           # Domain Layer
├── Legacy/           # Application (Unity ECS)
├── Native/           # Rust FFI bindings
├── Shaders/          # Compute/rendering shaders
└── Resources/        # Runtime assets
```

## Entrypoints

- `Legacy/Core/KexEditManager.cs` - Runtime initialization
- `Legacy/Track/Track.cs` - Track data structure
- `Legacy/Persistence/CoasterLoader.cs` - File loading
