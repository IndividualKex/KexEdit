# Migration Plan: Legacy ECS → Hexagonal Architecture

## Current State

The hexagonal architecture is implemented and proven:

```
┌─────────────────────────────────────────────────────────────┐
│ Domain Layer (complete)                                      │
│   KexGraph, KexEdit.Core, KexEdit.Nodes                     │
├─────────────────────────────────────────────────────────────┤
│ Application Layer (complete)                                 │
│   Coaster (aggregate), CoasterEvaluator (use case)          │
├─────────────────────────────────────────────────────────────┤
│ Adapters (complete)                                          │
│   KexEdit.Persistence (chunk-based serialization)           │
│   KexEdit.LegacyImport (legacy .kex → Coaster)              │
└─────────────────────────────────────────────────────────────┘
```

**Proven path**: LegacyImporter → Coaster → CoasterEvaluator (validated via gold tests)

## The Problem

ECS build systems duplicate conversion logic and maintain parallel data models:

```
ECS Entity                    Domain Layer                 ECS Buffer
┌──────────────┐             ┌──────────────┐             ┌──────────────┐
│ Anchor       │──ToPoint()─►│ ForceNode    │─ToPointData►│ Point Buffer │
│ (PointData)  │  (×7 dupe)  │ .Build()     │  (×7 dupe)  │ (PointData)  │
└──────────────┘             └──────────────┘             └──────────────┘
```

**Data model mismatch**:
- `PointData` (Legacy): 23 fields, mutable, stores derived values (Roll, AngleFromLast, etc.)
- `Core.Point` (New): 16 fields, immutable, computes derived values on demand

## Migration Strategy

Incremental phases that maintain runtime compatibility:

### Phase 1: Centralize Converters ✓

`PointConverter.cs` provides shared conversion between `Core.Point` and `PointData`.

### Phase 2: ECS-Compatible Point Buffer ✓

`CorePointBuffer` IBufferElementData wraps `Core.Point` with cached derived values and PointData-compatible extension methods.

### Phase 3: Dual-Write Mode ✓

Build systems write to both `DynamicBuffer<Point>` and `DynamicBuffer<CorePointBuffer>`.

### Phase 4: Migrate Consumers ✓

Runtime ECS systems migrated from `DynamicBuffer<Point>` to `DynamicBuffer<CorePointBuffer>`:
- StyleHashSystem, TrainUpdateSystem, TrackFollowerUpdateSystem
- TrackPointSystem, ReadOnlyForceComputationSystem, DistanceFollowerUpdateSystem
- TrackSegmentInitializationSystem

UI systems (StatsOverlaySystem, TimelineControlSystem, KeyframeGizmoUpdateSystem) deferred to Phase 5.

### Phase 5: Remove Old Buffer

Delete `Point` buffer and `PointData` once all consumers migrated.

### Phase 6: Replace Build Systems

Single `CoasterSyncSystem` mirrors ECS graph to Coaster, calls `CoasterEvaluator.Evaluate()` when dirty, writes to ECS buffers.

## Key Files

| Area | Files |
|------|-------|
| Domain point | `Assets/Runtime/Core/Point.cs` |
| ECS point buffer | `Assets/Runtime/Legacy/Track/Components/CorePointBuffer.cs` |
| Point converter | `Assets/Runtime/Legacy/Track/Utils/PointConverter.cs` |
| Build systems | `Assets/Runtime/Legacy/Track/Systems/Build*System.cs` |
| Coaster evaluator | `Assets/Runtime/Coaster/CoasterEvaluator.cs` |

## Validation

- `./run-tests.sh` after each phase
- Gold tests validate physics parity
- Dual-write mode catches mismatches before consumer migration
