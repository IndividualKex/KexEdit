# Migration Plan: Coaster as Source of Truth

## Target Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ UI Layer                                                     │
│   NodeGraphControlSystem modifies Coaster directly          │
├─────────────────────────────────────────────────────────────┤
│ Domain Layer (authoritative)                                 │
│   Coaster aggregate: Graph + Keyframes + Properties         │
│   CoasterEvaluator: incremental subgraph evaluation         │
├─────────────────────────────────────────────────────────────┤
│ Application Layer (derived)                                  │
│   CoasterSyncSystem: Coaster → ECS CorePointBuffer          │
│   ECS entities: visualization, physics, rendering           │
└─────────────────────────────────────────────────────────────┘
```

**Key changes from legacy:**
- Coaster is authoritative (not ECS entities)
- Per-entity Coaster storage (supports multiple coasters)
- Incremental dirty subgraph evaluation (not full rebuild)
- UI modifies Coaster directly (not ECS components)
- ECS becomes a derived view for rendering/physics

## Migration Phases

### Phase 1: Per-Entity Coaster Storage

Add managed Coaster storage alongside existing ECS flow.

**Files to create:**
- `Assets/Runtime/Legacy/Persistence/Components/CoasterStore.cs` - IComponentData with Entity→Coaster mapping via managed singleton

**Files to modify:**
- `Assets/Runtime/Legacy/Persistence/Components/Coaster.cs` - rename to `CoasterEntity` to avoid confusion

**Acceptance:**
- CoasterStore holds KexEdit.Coaster.Coaster instances keyed by Entity
- Lifecycle management (create on entity spawn, dispose on destroy)
- Tests pass

### Phase 2: ECS → Coaster Sync (Scaffolding)

Create temporary sync that mirrors ECS state to Coaster for validation.

**Files to create:**
- `Assets/Runtime/Legacy/Track/Systems/EcsToCoasterSyncSystem.cs` - reads ECS entities, populates Coaster struct

**Sync mapping:**
- `Node.Id` → Coaster.Graph node
- `Port` entities → Coaster.Graph ports
- `Connection` entities → Coaster.Graph edges
- Port value components → Coaster.Scalars/Vectors/Rotations
- Keyframe buffers → Coaster.Keyframes
- `Duration` → Coaster.Durations
- `Steering` → Coaster.Steering
- `Anchor` → Coaster.Anchors

**Acceptance:**
- After sync, Coaster contains equivalent data to ECS
- Can call CoasterEvaluator.Evaluate() and get results
- Tests pass

### Phase 3: Evaluation Parity Validation

Verify CoasterEvaluator produces identical results to Build*Systems.

**Files to create:**
- `Assets/Tests/EditMode/CoasterEvaluatorParityTests.cs`

**Test strategy:**
- Load gold test coasters
- Run legacy Build*Systems → capture CorePointBuffer
- Run EcsToCoasterSync → CoasterEvaluator.Evaluate() → compare
- Assert physics parity (positions, velocities, forces within epsilon)

**Acceptance:**
- All gold tests pass parity check
- CoasterEvaluator matches Build*Systems output

### Phase 4: Dirty Tracking Infrastructure

Add dirty node tracking to Coaster for incremental evaluation.

**Files to modify:**
- `Assets/Runtime/Coaster/Coaster.cs` - add `NativeHashSet<uint> DirtyNodes`

**Files to create:**
- `Assets/Runtime/Coaster/CoasterDirtyTracker.cs` - utilities for marking dirty and computing affected subgraph

**Dirty propagation:**
- When node X is marked dirty, all downstream nodes (via graph edges) are also dirty
- Topological sort of dirty set determines evaluation order
- Clean nodes retain cached results

**Acceptance:**
- Can mark individual nodes dirty
- Dirty propagation follows graph topology
- Tests pass

### Phase 5: Incremental Subgraph Evaluation

Modify CoasterEvaluator to support partial evaluation.

**Files to modify:**
- `Assets/Runtime/Coaster/CoasterEvaluator.cs` - add overload accepting dirty set
- `Assets/Runtime/Coaster/Coaster.cs` - add result caching (OutputAnchors, Paths stored on Coaster)

**New signature:**
```csharp
public static void EvaluateIncremental(
    ref Coaster coaster,
    in NativeHashSet<uint> dirtyNodes,
    Allocator allocator
)
```

**Behavior:**
- Only evaluates nodes in dirtyNodes (topologically sorted)
- Reads cached anchors/paths for clean upstream nodes
- Updates cached results for evaluated nodes
- Clears dirtyNodes after successful evaluation

**Acceptance:**
- Partial evaluation produces same results as full evaluation
- Only dirty nodes are re-computed
- Tests pass

### Phase 6: Coaster → ECS Sync System

Create the forward sync that writes evaluation results to ECS.

**Files to create:**
- `Assets/Runtime/Legacy/Track/Systems/CoasterToEcsSyncSystem.cs`

**Sync logic:**
- For each node in Coaster.Graph:
  - Find corresponding ECS entity via Node.Id lookup
  - If node has cached Path, write to CorePointBuffer
  - Update output port AnchorPort from OutputAnchors
- Run in parallel per-coaster (IJobEntity with coaster entity)

**Acceptance:**
- CorePointBuffer populated from Coaster evaluation results
- Downstream ECS systems (rendering, physics) work unchanged
- Tests pass

### Phase 7: Dual-Mode Validation

Run both flows simultaneously and compare results.

**Files to modify:**
- `Assets/Runtime/Legacy/Track/Systems/CoasterToEcsSyncSystem.cs` - add validation mode

**Validation:**
- Legacy Build*Systems write to CorePointBuffer
- CoasterToEcsSyncSystem writes to separate validation buffer
- Compare buffers, log discrepancies
- Feature flag to enable/disable validation

**Acceptance:**
- No discrepancies in any test scenario
- Performance acceptable with both flows active

### Phase 8: UI → Coaster Modification (Reading)

UI reads from Coaster instead of ECS for display.

**Files to modify:**
- `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs` - add CoasterStore access
- `Assets/Scripts/UI/NodeGraph/Data/NodeData.cs` - read from Coaster

**Strategy:**
- Create helper methods that read Coaster data
- Replace ECS reads with Coaster reads in UI
- ECS entities still exist but UI doesn't read them

**Acceptance:**
- UI displays correct data from Coaster
- No visual regressions
- Tests pass

### Phase 9: UI → Coaster Modification (Writing)

UI writes to Coaster instead of ECS. This is the flip point.

**Files to modify:**
- `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs` - all event handlers

**Handler migration (per NodeGraphControlSystem methods):**
| Method | Current | Target |
|--------|---------|--------|
| `AddNode()` | EntityManager.CreateEntity | Coaster.Graph.CreateNode + mark dirty |
| `RemoveSelected()` | ecb.DestroyEntity | Coaster.Graph.RemoveNodeCascade |
| `OnDragNodes()` | Node.Position write | (UI-only, no Coaster change) |
| `OnDurationTypeChange()` | Duration.Type write | Coaster.Durations update + mark dirty |
| `OnRenderToggleChange()` | Render.Value write | (UI-only metadata) |
| `OnSteeringToggleChange()` | Steering.Value write | Coaster.Steering update + mark dirty |
| `OnPriorityChange()` | Node.Priority write | (UI-only metadata) |
| `ApplyInputPortValue()` | Port component writes | Coaster.Scalars/Vectors + mark dirty |
| `AddConnection()` | Create Connection entity | Coaster.Graph.AddEdge + mark dirty |

**Dirty marking:**
- After any modification, mark affected nodeId dirty
- CoasterToEcsSyncSystem will evaluate and sync

**Acceptance:**
- All UI operations modify Coaster
- Dirty tracking triggers re-evaluation
- ECS updated via sync system
- Tests pass

### Phase 10: Remove Legacy Flow

Delete Build*Systems and ECS propagation.

**Files to delete:**
- `Assets/Runtime/Legacy/Track/Systems/BuildForceSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildGeometricSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildCurvedSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildBridgeSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildReverseSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildReversePathSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/BuildCopyPathSectionSystem.cs`
- `Assets/Runtime/Legacy/Track/Systems/EcsToCoasterSyncSystem.cs` (scaffolding)

**Files to modify:**
- `Assets/Runtime/Legacy/Track/Systems/GraphSystem.cs` - remove propagation logic, keep minimal node linking
- `Assets/Runtime/Legacy/Track/Systems/GraphTraversalSystem.cs` - simplify (graph topology now in Coaster)

**Acceptance:**
- Only CoasterToEcsSyncSystem writes CorePointBuffer
- Legacy systems deleted
- Tests pass

### Phase 11: ECS Entity Simplification

Reduce ECS to minimal derived view.

**Files to modify/delete:**
- Remove unused port components (data now in Coaster)
- Remove keyframe buffers from entities (data now in Coaster)
- Keep: Node (for UI position), CorePointBuffer (for rendering)

**New ECS archetype:**
- Node entity: Node, CorePointBuffer, CoasterReference
- Coaster entity: CoasterEntity (pointer to Coaster in store)

**Acceptance:**
- ECS is minimal derived view
- Rendering/physics still work
- Memory usage reduced
- Tests pass

### Phase 12: Undo System Migration

Serialize Coaster instead of ECS graph.

**Files to modify:**
- `Assets/Runtime/Legacy/Persistence/SerializationSystem.cs` - serialize Coaster struct
- `Assets/Runtime/Persistence/CoasterSerializer.cs` - ensure complete serialization

**Strategy:**
- Undo.Record() captures Coaster state (not ECS)
- Restore deserializes Coaster and triggers full re-sync

**Acceptance:**
- Undo/redo works correctly
- State fully restored
- Tests pass

### Phase 13: Cleanup

Remove scaffolding and update documentation.

**Tasks:**
- Remove validation/comparison code
- Remove feature flags
- Update context.md files
- Update CLAUDE.md if needed
- Delete this PLAN.md (migration complete)

## Key Files Reference

| Area | Files |
|------|-------|
| Coaster aggregate | `Assets/Runtime/Coaster/Coaster.cs` |
| Coaster evaluator | `Assets/Runtime/Coaster/CoasterEvaluator.cs` |
| ECS coaster storage | `Assets/Runtime/Legacy/Persistence/Components/CoasterStore.cs` (new) |
| Sync system | `Assets/Runtime/Legacy/Track/Systems/CoasterToEcsSyncSystem.cs` (new) |
| UI control | `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs` |
| Graph extensions | `Assets/Runtime/NodeGraph/TypedGraphExtensions.cs` |
| Point buffer | `Assets/Runtime/Legacy/Track/Components/CorePointBuffer.cs` |

## Validation

- `./run-tests.sh` after each phase
- Gold tests validate physics parity throughout
- No regressions in rendering/physics behavior
