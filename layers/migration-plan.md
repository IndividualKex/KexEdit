# Runtime Migration Plan

Rebuild KexEdit runtime with hexagonal architecture. Enables Rust/WASM migration and clean separation of concerns.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              INFRASTRUCTURE ADAPTERS (KexEdit.Adapters)         │
│   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐               │
│   │ Unity ECS   │ │   File I/O  │ │ Positioning │  ...          │
│   │  Systems    │ │  (Kex/JSON) │ │   Systems   │               │
│   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘               │
│          └───────────────┼───────────────┘                       │
│                          ▼                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────┐
│              NODE TYPES (KexEdit.Nodes.*)                        │
│   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐               │
│   │  ForceNode  │ │ GeometricN. │ │ CurvedNode  │  ...          │
│   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘               │
│          └───────────────┼───────────────┘                       │
│                          ▼                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────┐
│              NODE SCHEMA (KexEdit.Nodes)                         │
│   PortId, PropertyId, NodeType, NodeSchema, PropertyIndex        │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────┐
│                  CORE (KexEdit.Core)                             │
│   Point, Frame, Curvature, Forces, Sim, Keyframe                 │
└──────────────────────────────────────────────────────────────────┘
```

## Directory Structure

```
KexEdit/
├── rust-backend/               Rust implementation (production-ready)
│   ├── kexedit-core/           Pure physics/math
│   ├── kexedit-nodes/          Node implementations
│   └── kexedit-ffi/            C FFI layer for Unity
├── Assets/Runtime/
│   ├── Core/                   Pure physics/math (C#)
│   ├── Nodes/                  Node implementations (C#)
│   ├── Native/RustCore/        Rust FFI bindings
│   └── Legacy/                 ECS adapters and legacy code
│       ├── Track/              Section building adapters
│       ├── Trains/             Train systems
│       └── Physics/            Track following, positioning
```

## Assembly Dependencies

```
KexEdit.Adapters ──► KexEdit.Nodes.Force ──► KexEdit.Nodes ──► KexEdit.Core
                     KexEdit.Nodes.Geometric ─┘
                     KexEdit.Nodes.Curved ────┘
                     (node types cannot depend on each other)
```

## Design Principles

- **Core is Physics-Only**: No awareness of train direction, section boundaries, or friction resets
- **Nodes Own State Management**: Node types control initial state for each section
- **Hexagonal Architecture**: Core has no dependencies; adapters use core ports
- **Single Source of Truth**: Continuous buffers; systems interpret as needed
- **Zero-Copy Aliasing**: Multiple systems read same data via buffer lookups
- **Testability**: Every layer has comprehensive tests before adding outer layers
- **Portability**: Architecture designed for Rust/WASM port

## Key Architectural Decisions

### Hexagonal Layers

Core → Nodes → Adapters. Dependencies flow inward. Core is pure physics with no Unity dependencies.

### Rust FFI (Production Ready)

Rust backend validated and outperforms Burst (3.5mm vs 5.2mm drift). Toggle via `USE_RUST_BACKEND` compilation flag.

**Build**: `./build-rust.sh` (cross-platform)

### Continuous Track Buffer

**Previous architecture**: Per-section `DynamicBuffer<Point>` - positioning systems needed graph knowledge to traverse sections.

**New architecture**: Single continuous `DynamicBuffer<TrackPoint>` for entire track.

```
Track Entity
└── DynamicBuffer<TrackPoint>  (continuous, resampled, entire track)
     ↓ (zero-copy read-only aliasing)
     ├── Rendering System (batches for GPU)
     ├── Positioning System (sequential reads, no graph knowledge)
     ├── Physics System (distance queries)
     └── UI Systems (playhead, stats)
```

**Benefits**:
- Positioning has no section/graph dependency
- Seamless boundary crossing (train at indices [145.3, 147.8, 150.2])
- Zero-copy via `BufferLookup<TrackPoint>(isReadOnly: true)`
- Cache-friendly continuous memory
- Systems choose their own chunking/interpretation

**Data Contract**:

```csharp
public struct TrackPoint : IBufferElementData {
    public float3 Position;   // World position
    public float3 Direction;  // Forward tangent
    public float3 Normal;     // Up vector
    public float3 Lateral;    // Right vector
    public float Distance;    // Cumulative arc length (for queries)
}
```

**Resampling Adapter**: Future system will convert per-section `Point` → continuous `TrackPoint` at fixed sample rate or quality criteria.

### Legacy Naming Mapping

The legacy code used inverted naming. Clean code uses physically accurate names:

| Legacy Field         | Clean Name       | Meaning                           |
| -------------------- | ---------------- | --------------------------------- |
| `position`           | `HeartPosition`  | Rider center position             |
| `totalLength`        | `HeartArc`       | Cumulative rider path length      |
| `totalHeartLength`   | `SpineArc`       | Cumulative track rail path length |
| `distanceFromLast`   | `SpineAdvance`   | Per-step track rail distance      |
| `heartDistanceFromLast` | `HeartAdvance` | Per-step rider distance        |
| `frictionCompensation` | `FrictionOrigin` | SpineArc baseline for friction |
| `heart`              | `HeartOffset`    | Distance from heart to spine      |

**Heart** = rider center (primary reference for forces); **Spine** = track rail (derived from heart)

## Testing

**Strategy**: Unit tests → Parameterized edge cases → Golden fixtures (265+ tests)

**Golden fixtures**: `shuttle.kex`, `veloci.kex`, `all_types.kex` (validated against dev branch)

**Run tests**:
- All: `./run-tests.sh`
- Filtered: `./run-tests.sh TestName`
- Rust backend: `./run-tests.sh --rust-backend`

**Coverage**:
- Rust: 76 core tests, 57 node tests
- C#: Core, Nodes, ECS adapters all validated
- FFI: Integration tests validate C# ↔ Rust equivalence

## Current Status

### ✅ Completed

- Hexagonal architecture (Core → Nodes → Adapters)
- Rust implementation: production-ready, outperforms Burst
- Node types: Force, Geometric, Curved, CopyPath, Bridge
- ECS adapters: All Build*System tests passing
- Comprehensive test suite: 265+ tests

### 🔧 Next Steps

1. **Track Buffer Architecture**
   - Implement continuous `DynamicBuffer<TrackPoint>`
   - Create resampling adapter (Point → TrackPoint)
   - Migrate positioning systems to new buffer

2. **Positioning System Rebuild**
   - Pure positioning logic (no graph knowledge)
   - Comprehensive tests with synthetic tracks
   - Replace brittle train car positioning

3. **Additional Rust Node Types**
   - Port remaining node types to Rust
   - Maintain golden test validation

### 🎯 Long-term Goals

- **WASM Target**: Compile Rust core to WASM for WebGL builds
- **GPU Acceleration**: Move force integration to compute shaders
- **Legacy Removal**: Complete migration to adapter pattern
