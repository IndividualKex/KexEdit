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

### Phase 1: Centralize Converters

Extract duplicated `ToPoint()`, `ToPointData()`, `ConvertKeyframes()` from 7 build systems into `PointConverter.cs`. Pure refactoring, no behavioral change.

### Phase 2: Extend Core.Point

Add computed property methods to `Core.Point` for derived fields (GetPitch, GetYaw, GetDistanceFromLast, etc.) so it can eventually replace PointData.

### Phase 3: ECS-Compatible Point Wrapper

Create `CorePointBuffer` that wraps `Core.Point` with extension methods for PointData-like access patterns.

### Phase 4: Dual-Write Mode

Build systems write to both old `DynamicBuffer<Point>` (PointData) and new `DynamicBuffer<CorePointBuffer>`. Validate parity in debug builds.

### Phase 5: Migrate Consumers

One-by-one, migrate downstream systems (StyleHash, TrackFollower, Train, Mesh) from old to new buffer.

### Phase 6: Remove Old Buffer

Delete old Point buffer and PointData conversion once all consumers migrated.

### Phase 7: Replace Build Systems

Single `CoasterSyncSystem` maintains Coaster struct mirroring ECS graph, calls `CoasterEvaluator.Evaluate()` when dirty, writes results to ECS buffers.

## Key Files

| Area | Files |
|------|-------|
| Build systems (×7) | `Assets/Runtime/Legacy/Track/Systems/Build*System.cs` |
| Legacy point | `Assets/Runtime/Legacy/Track/Components/PointData.cs` |
| New point | `Assets/Runtime/Core/Point.cs` |
| Coaster evaluator | `Assets/Runtime/Coaster/CoasterEvaluator.cs` |
| Proven converter | `Assets/Runtime/LegacyImport/LegacyImporter.cs` |

## Validation

- `./run-tests.sh` after each phase
- Gold tests validate physics parity throughout migration
- Dual-write mode catches mismatches before consumer migration
