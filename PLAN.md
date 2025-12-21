# Migration Plan: Coaster as Source of Truth

## Target Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ UI Layer (Unity-specific, ECS ok)                           │
│   NodeGraphControlSystem modifies Coaster via ECS mapping   │
├─────────────────────────────────────────────────────────────┤
│ Application Layer (derived ECS view)                        │
│   CoasterSyncSystem: Coaster evaluation → CorePointBuffer   │
│   ECS entities: uint-indexed references, visualization      │
├─────────────────────────────────────────────────────────────┤
│ Core Layer (portable, unopinionated)                        │
│   Coaster aggregate: Graph + Keyframes + Properties         │
│   CoasterEvaluator: full or incremental subgraph eval       │
│   Nodes.*: pure build functions (Burst/Rust backend)        │
└─────────────────────────────────────────────────────────────┘
```

**Key principles:**
- Coaster is pure blittable data (no Unity dependencies beyond Collections/Mathematics)
- Core layer is portable - can have swappable Rust or Burst backend
- ECS is a derived view for UI interaction and rendering
- uint-indexed data maps naturally to/from ECS entities
- No managed storage needed - DynamicBuffers can alias blittable structs

## Migration Phases

### Phase 0: Naming Convention Cleanup

**Status: In Progress**

Migrate from legacy inverted naming to modern semantic naming where heart is the primary coordinate and spine is derived.

**Completed:**
- ✅ Renamed `PointData` struct fields to modern conventions
- ✅ Renamed `PointData` extension methods (`GetSpinePosition`, `GetSpineDirection`, `GetSpineLateral`)
- ✅ Renamed `CorePointBuffer` extension methods (`HeartPosition()`, `HeartArc()`, `SpineArc()`, etc.)
- ✅ Updated all Runtime/Legacy systems (Physics, Track, Trains, Visualization, Persistence)
- ✅ Updated Node implementations and test builders
- ✅ Fixed gold data test comparisons in `PointComparer.cs` and `SimPointComparer.cs`

**Remaining:**
- ⏳ Update UI layer extension method calls (5 files, ~207 references):
  - `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs` (~90 errors)
  - `Assets/Scripts/UI/Systems/StatsOverlaySystem.cs` (~81 errors)
  - `Assets/Scripts/UI/Systems/KeyframeGizmoUpdateSystem.cs` (~18 errors)
  - `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs` (~12 errors)
  - `Assets/Scripts/UI/Systems/GameViewControlSystem.cs` (~6 errors)

**Required changes in UI files:**
- `.Position()` → `.HeartPosition()`
- `.TotalLength()` → `.HeartArc()`
- `.TotalHeartLength()` → `.SpineArc()`
- `.Heart()` → `.HeartOffset()`
- `.GetHeartPosition()` → `.GetSpinePosition()`
- `.GetHeartDirection()` → `.GetSpineDirection()`
- `.GetHeartLateral()` → `.GetSpineLateral()`

**Naming reference:**

| Legacy (Inverted) | Modern (Semantic) | Meaning |
|-------------------|-------------------|---------|
| Position | HeartPosition | Primary coordinate (heart line) |
| TotalLength | HeartArc | Cumulative arc length along heart |
| HeartDistanceFromLast | SpineAdvance | Distance between spine points |
| DistanceFromLast | HeartAdvance | Distance between heart points |
| TotalHeartLength | SpineArc | Cumulative arc length along spine |
| FrictionCompensation | FrictionOrigin | Arc length where friction starts |
| Heart | HeartOffset | Perpendicular offset from heart to spine |

**Acceptance criteria:**
- All compiler errors resolved
- All tests pass
- No direct access to `GoldPointData` legacy fields outside `GoldDataTypes.cs`
- Modern accessor pattern used throughout codebase

### Phase 1: Parity Tests (Test-First Foundation)

**Status: In Progress**

**Completed:**
- ✅ `Shuttle_LoadAndEvaluate_MatchesGoldData` passes (2725 points)
- ✅ Extended `CoasterGoldTests.cs` with point-by-point parity checking using `SimPointComparer`
- ✅ Fixed driven velocity mode
- ✅ Fixed CurvedSection scalar port values
- ✅ Fixed ReversePathNode arc recalculation

#### 1. CopyPath velocity divergence (AllTypes test)

#### 2. Veloci test - cumulative drift

#### 3. Bridge lateral force calculation
- Separate bug in Bridge section physics
- Blocked pending other fixes

**Acceptance criteria:**
- All gold coasters pass point-by-point parity check
- CoasterEvaluator output matches Build*System output for same inputs

### Phase 2: Dual-Flow Validation

Run both flows simultaneously to prove CoasterEvaluator matches legacy ECS flow.

**Files to create:**
- `Assets/Tests/DualFlowParityTests.cs` - ECS tests comparing both paths

**Test strategy:**
```csharp
// For each gold section:
// 1. Create ECS entity via EntityBuilder (existing pattern)
// 2. Run Build*System → capture CorePointBuffer
// 3. Create Coaster from same inputs → CoasterEvaluator.Evaluate()
// 4. Compare point-by-point
```

**Acceptance:**
- Every Build*System produces identical output to CoasterEvaluator
- Tests cover all node types (Force, Geometric, Curved, Bridge, CopyPath, Reverse)

### Phase 3: ECS Entity Simplification

Reduce ECS entities to minimal derived view. Coaster holds authoritative data.

**Current ECS archetype:**
- Node, Anchor, Duration, PropertyOverrides
- DynamicBuffer<*Keyframe> (7 buffers per node)
- Port entities, Connection entities
- OutputPortReference, CorePointBuffer

**Target ECS archetype:**
- `CoasterReference` - IComponentData with uint coasterIndex + uint nodeId
- `CorePointBuffer` - DynamicBuffer for rendering (derived from Coaster eval)
- Node entity for UI position/selection (UI-only metadata)

**Strategy:**
- Coaster struct already holds all authoritative data (Graph, Keyframes, Scalars, etc.)
- ECS entities become thin references: `(coasterIndex, nodeId)`
- CorePointBuffer populated by sync system from evaluation result
- Can alias DynamicBuffer to NativeList for zero-copy where possible

**Files to create:**
- `Assets/Runtime/Legacy/Track/Components/CoasterReference.cs`

**Acceptance:**
- CoasterReference component created
- Can look up Coaster data from ECS entity

### Phase 4: CoasterSyncSystem

Create the forward sync that evaluates Coaster and writes to ECS.

**Files to create:**
- `Assets/Runtime/Legacy/Track/Systems/CoasterSyncSystem.cs`

**Sync logic:**
```csharp
// 1. Get Coaster from storage (NativeHashMap<uint, Coaster> or singleton)
// 2. CoasterEvaluator.Evaluate(in coaster, out result)
// 3. For each node with path in result.Paths:
//    - Find ECS entity via CoasterReference lookup
//    - Write path to CorePointBuffer
```

**Acceptance:**
- CorePointBuffer populated from Coaster evaluation
- Rendering/physics systems work unchanged
- Tests pass

### Phase 5: UI → Coaster Modification

UI modifies Coaster directly instead of ECS components.

**Files to modify:**
- `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs`

**Handler migration:**
| UI Action | Current (ECS) | Target (Coaster) |
|-----------|---------------|------------------|
| Add node | EntityManager.CreateEntity | Coaster.Graph.CreateNode |
| Remove node | ecb.DestroyEntity | Coaster.Graph.RemoveNodeCascade |
| Change duration | Duration.Value write | Coaster.Durations[nodeId] = |
| Change steering | Steering.Value write | Coaster.Steering.Add/Remove |
| Apply port value | Port component write | Coaster.Scalars/Vectors[portId] = |
| Add connection | Create Connection entity | Coaster.Graph.AddEdge |

**Strategy:**
- UI still uses ECS for selection, position, visual state
- Authoritative data modifications go to Coaster
- CoasterSyncSystem propagates changes to ECS CorePointBuffers

**Acceptance:**
- All UI operations modify Coaster
- ECS updated via CoasterSyncSystem
- Tests pass

### Phase 6: Remove Legacy Build*Systems

Delete the per-node ECS build systems.

**Files to delete:**
- `Assets/Runtime/Legacy/Track/Systems/BuildForceSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildGeometricSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildCurvedSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildBridgeSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildReverseSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildReversePathSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildCopyPathSectionSystem.cs`

**Files to simplify:**
- `Assets/Runtime/Legacy/Track/Systems/GraphSystem.cs` - remove propagation logic
- `Assets/Runtime/Legacy/Track/Systems/GraphTraversalSystem.cs` - remove (topology in Coaster.Graph)

**Acceptance:**
- Only CoasterSyncSystem writes CorePointBuffer
- Legacy Build*Systems deleted
- All tests still pass

### Phase 7: Remove Redundant ECS Components

Strip out components that are now stored in Coaster.

**Components to remove from entities:**
- Keyframe buffers (RollSpeedKeyframe, etc.) - data in Coaster.Keyframes
- Duration, PropertyOverrides - data in Coaster.Durations, Coaster.Steering
- Anchor (data component) - data in Coaster.Anchors
- Port value components - data in Coaster.Scalars/Vectors

**Components to keep:**
- Node (UI position, selection, ID reference)
- CoasterReference (links entity to Coaster)
- CorePointBuffer (rendering output)
- Tags for rendering/visual state

**Acceptance:**
- ECS is minimal derived view
- Memory usage reduced
- Rendering/physics still work

### Phase 8: Dirty Tracking (Optional Optimization)

Add incremental evaluation for performance.

**Files to modify:**
- `Assets/Runtime/Coaster/Coaster.cs` - add `NativeHashSet<uint> DirtyNodes`
- `Assets/Runtime/Coaster/CoasterEvaluator.cs` - add `EvaluateIncremental()` overload

**Behavior:**
- When node modified, mark nodeId dirty
- Dirty propagates to downstream nodes via graph edges
- `EvaluateIncremental()` only re-evaluates dirty subgraph
- Clean nodes retain cached results

**Acceptance:**
- Partial evaluation produces same results as full
- Performance improved for local edits

### Phase 9: Undo System Migration

Serialize Coaster for undo/redo.

**Files to modify:**
- `Assets/Runtime/Legacy/Persistence/SerializationSystem.cs`
- `Assets/Runtime/Persistence/CoasterSerializer.cs`

**Strategy:**
- Undo.Record() captures Coaster state (not ECS)
- Restore deserializes Coaster and triggers full sync
- Much simpler than ECS entity graph serialization

**Acceptance:**
- Undo/redo works correctly
- State fully restored

### Phase 10: Cleanup

Remove scaffolding and update documentation.

**Tasks:**
- Remove validation/comparison code
- Remove any feature flags
- Update context.md files
- Delete this PLAN.md

## Key Files Reference

| Area | Files |
|------|-------|
| Coaster aggregate | `Assets/Runtime/Coaster/Coaster.cs` |
| Coaster evaluator | `Assets/Runtime/Coaster/CoasterEvaluator.cs` |
| CopyPath node | `Assets/Runtime/Nodes/CopyPath/CopyPathNode.cs` |
| Point struct | `Assets/Runtime/Core/Point.cs` |
| Sim utilities | `Assets/Runtime/Core/Sim.cs` |
| Gold tests | `Assets/Tests/CoasterGoldTests.cs` |
| Point comparer | `Assets/Tests/SimPointComparer.cs` |
| Legacy CopyPath | `origin/dev:Assets/Runtime/Scripts/Track/Systems/BuildCopyPathSectionSystem.cs` |
| Legacy PointData | `origin/dev:Assets/Runtime/Scripts/Track/Components/PointData.cs` |
| Legacy Extensions | `origin/dev:Assets/Runtime/Scripts/Core/Extensions.cs` (ComputeEnergy) |

## Data Flow Diagram

```
Current (Legacy):
  ECS Entities → Build*Systems → CorePointBuffer → Rendering
       ↑
  UI Modifications

Target:
  Coaster (authoritative)
       ↓
  CoasterEvaluator.Evaluate()
       ↓
  CoasterSyncSystem → CorePointBuffer → Rendering
       ↑
  UI Modifications (via Coaster)
       ↑
  ECS Entities (derived view, UI position/selection)
```

## Validation

- `./run-tests.sh` after each phase
- Gold tests validate physics parity throughout
- No regressions in rendering/physics behavior
