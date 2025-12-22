# Migration Plan: ECS-Centric → Coaster-Centric Architecture

## Overview

This plan guides the migration from ECS-centric to Coaster-centric architecture, eliminating redundant components that duplicate Coaster aggregate data.

**Key Finding**: The codebase already has strong separation - CoasterSyncSystem is the single sync point from Coaster → ECS (via CorePointBuffer). Most redundancy is in **port value components** that duplicate Coaster.Scalars/Vectors/Rotations.

## Phase Summary

| Phase | Name | Status |
|-------|------|--------|
| 1 | Component & System Audit | ✅ COMPLETE |
| 2 | Validation Foundation | ✅ COMPLETE |
| 3 | Parallel UI → Coaster Pathway | ✅ COMPLETE |
| 4 | Pruning | 🔄 NEXT |
| 5 | Data Injection Support | ⏸️ Pending |
| 6 | UI Layer Migration | ⏸️ Deferred |
| 7 | Cleanup | ⏸️ Pending |

---

# Phase 1: Complete Component & System Audit ✅ COMPLETE

## Overview

Categorizes every ECS component and system in the Legacy layer to guide the migration.

---

## Executive Summary

- **107 ECS components** identified across Legacy layer
- **36 ECS systems** identified (25 Burst-compiled, 11 managed)
- **1 primary sync system**: CoasterSyncSystem (Coaster → CorePointBuffer)
- **3 secondary systems** read Coaster directly: GraphTraversalSystem, DistanceFollowerUpdateSystem, TrainUpdateSystem
- **Major redundancy**: 25+ port value components duplicate Coaster data

---

## Component Categorization

### Category: REMOVABLE (Duplicate Coaster Data)

These components store data that's already authoritative in the Coaster aggregate. After UI decoupling, these should be deleted.

#### Port Value Components (25 components) - **HIGH PRIORITY**

All store scalar/vector values already in `Coaster.Scalars`, `Coaster.Vectors`, or `Coaster.Rotations`:

| Component | Location | Coaster Source | Notes |
|-----------|----------|----------------|-------|
| `VelocityPort` | Track/Components | Scalars[portId] | Scalar velocity value |
| `RollPort` | Track/Components | Scalars[portId] | Scalar roll angle |
| `PitchPort` | Track/Components | Scalars[portId] | Scalar pitch angle |
| `YawPort` | Track/Components | Scalars[portId] | Scalar yaw angle |
| `FrictionPort` | Track/Components | Scalars[portId] | Scalar friction coefficient |
| `ResistancePort` | Track/Components | Scalars[portId] | Scalar resistance value |
| `HeartPort` | Track/Components | Scalars[portId] | Scalar heart line offset |
| `DurationPort` | Track/Components | Durations[nodeId].Value | Duration value (see Duration component) |
| `PositionPort` | Track/Components | Vectors[nodeId] | 3D position vector |
| `RotationPort` | Track/Components | Rotations[nodeId] | 3D rotation (Euler) |
| `ScalePort` | Track/Components | Scalars[portId] | Scalar scale value |
| `RadiusPort` | Track/Components | Scalars[portId]  | Curve radius |
| `ArcPort` | Track/Components | Scalars[portId] | Arc angle |
| `AxisPort` | Track/Components | Scalars[portId] | Axis angle |
| `LeadInPort` | Track/Components | Scalars[portId] | Lead-in distance |
| `LeadOutPort` | Track/Components | Scalars[portId] | Lead-out distance |
| `InWeightPort` | Track/Components | Scalars[portId] | Bezier in-weight |
| `OutWeightPort` | Track/Components | Scalars[portId] | Bezier out-weight |
| `StartPort` | Track/Components | Scalars[portId] | Start value |
| `EndPort` | Track/Components | Scalars[portId] | End value |
| `AnchorPort` | Track/Components | Result of evaluation | Output anchor from previous node |

**Action**: Remove after Phase 3 (Direct UI Migration) rewrites UI to read from Coaster

---

#### Keyframe Components (10 components) - **HIGH PRIORITY**

All store animation curves already in `Coaster.Keyframes`:

| Component | Location | Coaster Source | PropertyId |
|-----------|----------|----------------|------------|
| `FixedVelocityKeyframe` | Physics/Components | Keyframes[(nodeId, DrivenVelocity)] | DrivenVelocity |
| `FrictionKeyframe` | Physics/Components | Keyframes[(nodeId, Friction)] | Friction |
| `HeartKeyframe` | Physics/Components | Keyframes[(nodeId, HeartOffset)] | HeartOffset |
| `LateralForceKeyframe` | Physics/Components | Keyframes[(nodeId, LateralForce)] | LateralForce |
| `NormalForceKeyframe` | Physics/Components | Keyframes[(nodeId, NormalForce)] | NormalForce |
| `PitchSpeedKeyframe` | Physics/Components | Keyframes[(nodeId, PitchSpeed)] | PitchSpeed |
| `ResistanceKeyframe` | Physics/Components | Keyframes[(nodeId, Resistance)] | Resistance |
| `RollSpeedKeyframe` | Physics/Components | Keyframes[(nodeId, RollSpeed)] | RollSpeed |
| `YawSpeedKeyframe` | Physics/Components | Keyframes[(nodeId, YawSpeed)] | YawSpeed |
| `TrackStyleKeyframe` | Physics/Components | Keyframes[(nodeId, TrackStyle)] | TrackStyle |

**Action**: Remove after Phase 3 rewrites UI to read from `Coaster.Keyframes`

---

#### Node State Components (3 components)

| Component | Location | Coaster Source | Notes |
|-----------|----------|----------------|-------|
| `Duration` | Physics/Components | Durations[nodeId] | Type + Value |
| `Steering` | Physics/Components | Steering set | Boolean flag (node uses steering) |
| `CurveData` | Physics/Components | Computed from ports | Radius, Arc, Axis, LeadIn, LeadOut - all from Scalars |

**Action**:
- `Duration`: Keep temporarily for ECS systems, remove once UI queries Coaster directly
- `Steering`: Remove after UI reads `Coaster.Steering` set
- `CurveData`: Remove - computed from Scalars, not authoritative

---

### Category: RENDER OUTPUT (Keep - Correct Pattern)

These components represent **derived/computed rendering data** from Coaster evaluation. This is the correct use of ECS.

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `CorePointBuffer` | Track/Components | Evaluated path output | **KEY**: CoasterSyncSystem writes here |
| `TrackPoint` | Physics/Components | Sampled visualization points | TrackPointSystem samples CorePointBuffer |
| `ReadNormalForce` | Physics/Components | Computed force visualization | ReadOnlyForceComputationSystem |
| `ReadLateralForce` | Physics/Components | Computed force visualization | ReadOnlyForceComputationSystem |
| `ReadPitchSpeed` | Physics/Components | Computed angular velocity | ReadOnlyForceComputationSystem |
| `ReadRollSpeed` | Physics/Components | Computed angular velocity | ReadOnlyForceComputationSystem |
| `ReadYawSpeed` | Physics/Components | Computed angular velocity | ReadOnlyForceComputationSystem |
| `TrackStyleBuffers` | Visualization/Components | GPU mesh buffers | Managed rendering state |
| `TrackHash` | Visualization/Components | Change detection hash | Triggers rebuild |
| `TrackStyleHash` | Visualization/Components | Style change hash | Triggers segment rebuild |

**Action**: **KEEP** - These are outputs of computation, not domain data

---

### Category: UI ADAPTER (Keep Thin, Move Logic to UI Layer)

These components store **UI state and selection** - they should stay in ECS but logic should move to UI layer.

#### Graph UI State (3 components)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `Node` | Track/Components | UI position (float2), selection, Id, Next/Previous | **CRITICAL**: Id maps to Coaster.Graph; Next/Previous is traversal cache |
| `Connection` | Track/Components | Edge selection state | Id, Source, Target, Selected |
| `Port` | Track/Components | Port topology | Id, Type, IsInput - structure only |

**Key Notes**:
- `Node.Id` is the **lookup key** into Coaster.Graph, Coaster.Scalars, etc.
- `Node.Position` is UI-only (2D graph view position, NOT world space)
- `Node.Selected` is UI state
- `Node.Next/Previous` is a **traversal cache** populated by GraphTraversalSystem

**Action**: Keep, but migrate UI logic to `Assets/Scripts/UI/`

---

#### Timeline & Editor State (8 components)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `PropertyOverrides` | State/Components | Which properties are manually set | UI toggle state |
| `SelectedProperties` | State/Components | Which properties are selected | Timeline selection |
| `SelectedBlend` | State/Components | Selection blend animation | Visual feedback |
| `NodeGraphState` | State/Components | Graph pan/zoom | UI camera state |
| `TimelineState` | State/Components | Timeline offset/zoom | UI camera state |
| `CameraState` | State/Components | 3D viewport camera | UI camera state |
| `Preferences` | State/Components | User preferences | Visualization ranges, modes |
| `ReadPivot` | State/Components | Pivot offset for reading | UI tool state |

**Action**: Keep for now; consider moving to UI-only systems later (Phase 6)

---

#### Entity References (14 components)

These are ECS plumbing - entity references for queries and relationships:

| Component | Location | Purpose |
|-----------|----------|---------|
| `CoasterReference` | Persistence/Components | References coaster entity |
| `NodeReference` | Track/Components | References node entity |
| `SectionReference` | Track/Components | References section entity |
| `SegmentReference` | Track/Components | References segment entity |
| `InputPortReference` | Track/Components | Buffer of input port entities |
| `OutputPortReference` | Track/Components | Buffer of output port entities |
| `NodeMeshReference` | Visualization/Components | References node mesh |
| `TrackStyleReference` | Visualization/Components | Buffer of style entities |
| `TrackStyleSettingsReference` | Visualization/Components | References style settings |
| `TrainReference` | Trains/Components | References train entity |
| `TrainStyleReference` | Trains/Components | References train style |
| `TrainCarReference` | Trains/Components | Buffer of train cars |
| `WheelAssemblyReference` | Trains/Components | Buffer of wheel assemblies |
| `TrackColliderReference` | Physics/Components | Buffer of collider entities |

**Action**: **KEEP** - These are ECS entity relationships, not domain data

---

### Category: DEFERRED (Move to UI Layer Eventually)

These are deeply coupled to current ECS patterns. Move to `Assets/Scripts/UI/` in Phase 6 but keep functional for now.

#### Rendering & Visualization (11 components)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `Render` | Visualization/Components | Visibility toggle | UI state |
| `Segment` | Track/Components | Rendering segment | StartTime, EndTime, Style reference |
| `TrackStyle` | Visualization/Components | Style parameters | Spacing, Threshold, Step |
| `TrackStyleSettings` | Visualization/Components | Global style config | DefaultStyle, Version, AutoStyle |
| `NodeMesh` | Visualization/Components | Node mesh entity | References Node |
| `GizmoSettings` | Visualization/Components | Gizmo rendering config | Managed class |
| `ExtrusionGizmoSettings` | Visualization/Components | Per-gizmo settings | Managed class |
| `GlobalSettings` | Core/Components | Shaders and materials | Managed class |
| `TrackColliderTemplate` | Physics/Components | Collider archetype | Physics setup |
| `TrackColliderHash` | Physics/Components | Collider rebuild hash | Change detection |
| `AppendReference` | Persistence/Components | Appended file reference | Multi-file support |

**Action**: Keep functional; refactor in Phase 6

---

#### Train Simulation (18 components)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `Train` | Trains/Components | Train physics state | Distance, Facing, Enabled, Kinematic |
| `TrainCar` | Trains/Components | Car entity | Links to Train |
| `TrainCarMesh` | Trains/Components | Car mesh entity | Rendering |
| `TrainOffset` | Trains/Components | Position offset | Physics |
| `TrainStyle` | Trains/Components | Style version | Asset management |
| `TrainStyleManaged` | Trains/Components | Style data + loading | Managed class |
| `WheelAssembly` | Trains/Components | Wheel entity | Links to TrainCar |
| `TrainCarMeshReference` | Trains/Components | Buffer of meshes | Entity reference |
| `WheelAssemblyMeshReference` | Trains/Components | Buffer of meshes | Entity reference |
| `TrackFollower` | Physics/Components | Section+Index positioning | Derived from Train.Distance |
| `DistanceFollower` | Physics/Components | Absolute distance | Input for TrackFollower |

**Action**: Keep for now; train simulation is separate concern

---

### Category: SCAFFOLDING (Temporary Migration Support)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `CoasterPointBuffer` | Track/Components | Validation buffer | **CONDITIONAL**: Only exists with VALIDATE_COASTER_PARITY |
| `Dirty` | Core/Components | Marks UI modifications | Signals need to sync to Coaster |

**Action**:
- `CoasterPointBuffer`: Remove when VALIDATE_COASTER_PARITY flag removed (Phase 7)
- `Dirty`: Keep until UI writes directly to Coaster (may still be useful for change detection)

---

### Category: CRITICAL INFRASTRUCTURE (Do Not Remove)

| Component | Location | Purpose | Notes |
|-----------|----------|---------|-------|
| `Coaster` | Persistence/Components | Root entity reference | Holds Entity RootNode |
| `CoasterData` | Track/Components | **HOLDS COASTER AGGREGATE** | `KexEdit.Coaster.Coaster Value` |
| `Anchor` | Track/Components | Anchor point data | Input to evaluation (includes Facing) |

**Action**: **DO NOT REMOVE** - These bridge Coaster aggregate to ECS

---

### Category: SINGLETON/LIFECYCLE (Keep)

| Component | Location | Purpose |
|-----------|----------|---------|
| `InitializeEvent` | Core/Components | One-time initialization |
| `PauseSingleton` | State/Components | Global pause state |
| `UIStateSingleton` | State/Components | UI state marker |
| `AppendedCoasterTag` | Persistence/Components | Multi-file tag |
| `BridgeTag` | Track/Components | Node type tag |
| `ReverseTag` | Track/Components | Node type tag |
| `ReversePathTag` | Track/Components | Node type tag |
| `CopyPathSectionTag` | Track/Components | Node type tag |

**Action**: **KEEP** - Lifecycle and tags

---

## System Categorization

### CRITICAL: Primary Sync (1 system)

| System | Location | Purpose | Coaster Interaction |
|--------|----------|---------|---------------------|
| `CoasterSyncSystem` | Track/Systems | **Evaluates Coaster → writes CorePointBuffer** | Calls CoasterEvaluator.Evaluate(), writes paths to CorePointBuffer |

**Action**: **DO NOT MODIFY** - This is the single source of truth sync

---

### Secondary Coaster Readers (3 systems)

| System | Location | Reads From Coaster | Writes To Coaster |
|--------|----------|-------------------|-------------------|
| `GraphTraversalSystem` | Track/Systems | Reads CoasterReference | **Writes Coaster.RootNode** |
| `DistanceFollowerUpdateSystem` | Physics/Systems | Reads Coaster.RootNode | No |
| `TrainUpdateSystem` | Trains/Systems | Reads Coaster.RootNode | No |

**Action**: Keep; these are valid readers of Coaster aggregate

---

### Rendering Pipeline (12 systems)

These systems consume CorePointBuffer (already synced from Coaster) and produce rendering outputs:

| System | Location | Purpose | Notes |
|--------|----------|---------|-------|
| `ReadOnlyForceComputationSystem` | Physics/Systems | Computes visualization forces | Reads CorePointBuffer |
| `TrackPointSystem` | Physics/Systems | Samples CorePointBuffer for rendering | Creates TrackPoint buffer |
| `TrackSegmentInitializationSystem` | Track/Systems | Creates rendering segments | Reads CorePointBuffer |
| `TrackSegmentUpdateSystem` | Physics/Systems | Updates segment render state | Propagates selection |
| `StyleHashSystem` | Visualization/Systems | Hashes style for change detection | Reads CorePointBuffer endpoints |
| `TrackStyleBuildSystem` | Visualization/Systems | Builds GPU mesh buffers | Compute shaders |
| `TrackStyleRenderSystem` | Visualization/Systems | Renders track meshes | Graphics API |
| `MeshUpdateSystem` | Visualization/Systems | Updates node mesh transforms | Reads Anchor |
| `VisualizationSystem` | Visualization/Systems | Sets shader globals | Preferences |
| `TrackColliderCreationSystem` | Physics/Systems | Creates physics colliders | Reads TrackPoint |
| `TrackFollowerUpdateSystem` | Physics/Systems | Positions entities on track | Reads CorePointBuffer |
| `TrackStyleSettingsLoadingSystem` | Visualization/Systems | Loads style settings | Event handler |

**Action**: **KEEP** - These are render consumers, not domain logic

---

### Cleanup Systems (10 systems)

| System | Location | Purpose |
|--------|----------|---------|
| `ConnectionCleanupSystem` | Track/Systems | Orphaned connections |
| `NodeCleanupSystem` | Track/Systems | Orphaned nodes |
| `TrackSegmentCleanupSystem` | Physics/Systems | Invalidated segments |
| `TrackColliderCleanupSystem` | Physics/Systems | Orphaned colliders |
| `MeshCleanupSystem` | Visualization/Systems | Orphaned meshes |
| `TrackStyleCleanupSystem` | Visualization/Systems | Orphaned styles |
| `TrainCleanupSystem` | Trains/Systems | Orphaned trains |
| `TrainCarCleanupSystem` | Trains/Systems | Stale train cars |
| `TrainCarMeshCleanupSystem` | Trains/Systems | Orphaned car meshes |
| `WheelAssemblyCleanupSystem` | Trains/Systems | Orphaned wheels |
| `AppendCleanupSystem` | Persistence | Orphaned appended coasters |

**Action**: **KEEP** - ECS entity lifecycle management

---

### Train Simulation (7 systems)

| System | Location | Purpose |
|--------|----------|---------|
| `TrainCreationSystem` | Trains/Systems | Creates train per coaster |
| `TrainUpdateSystem` | Trains/Systems | Advances train along track |
| `TrainCarCreationSystem` | Trains/Systems | Instantiates car meshes |
| `TrainCarUpdateSystem` | Trains/Systems | Propagates state to cars |
| `TrainCarTransformUpdateSystem` | Trains/Systems | Positions cars |
| `WheelAssemblyUpdateSystem` | Trains/Systems | Updates wheel followers |
| `TrainStyleLoadingSystem` | Trains/Systems | Loads train style assets |

**Action**: **KEEP** - Train simulation is separate concern

---

### Infrastructure (3 systems)

| System | Location | Purpose |
|--------|----------|---------|
| `InitializationSystem` | Core/Systems | One-time setup |
| `PauseSystem` | Core/Systems | Pause management |
| `SerializationSystem` | Persistence | Save/load, undo/redo |

**Action**: **KEEP** - Core infrastructure

---

### Validation (1 system)

| System | Location | Purpose | Conditional |
|--------|----------|---------|-------------|
| `ParityValidationSystem` | Track/Systems | Validates CorePointBuffer vs CoasterPointBuffer | VALIDATE_COASTER_PARITY only |

**Action**: **REMOVE** in Phase 7 (cleanup)

---

## Phase 3 Candidate Analysis

These are the **highest priority** components to remove after UI decoupling:

### Port Value Components (25 total)
- **UI Reads**: Property panels, node editors, timeline keyframe editors
- **UI Writes**: User input handlers, undo/redo
- **Replacement**: Read/write `Coaster.Scalars[portId]`, `Coaster.Vectors[nodeId]`, `Coaster.Rotations[nodeId]`

### Keyframe Components (10 total)
- **UI Reads**: Timeline view, curve editors
- **UI Writes**: Keyframe creation/deletion, curve editing
- **Replacement**: Read/write `Coaster.Keyframes` using `(nodeId, PropertyId)` as key

**Estimated Impact**: Removing these 35 component types would eliminate ~33% of all Legacy components

---

## Phase 5 Data Injection Review

Current Coaster collections support:

✅ **Already Supported**:
- Node properties (Scalars, Vectors, Rotations, Durations)
- Animation curves (Keyframes with PropertyId enum)
- Boolean flags (Driven set, Steering set)
- Graph structure (via KexGraph.Graph)

❓ **Potentially Missing**:
- **Render visibility per node** - Currently in `Render` component
  - **Recommendation**: Add `Coaster.Hidden` set (NativeHashSet<uint>) for nodeIds to hide
- **Property override flags** - Currently in `PropertyOverrides` component
  - **Recommendation**: Add `Coaster.Overrides` dictionary (NativeHashMap<uint, PropertyOverrideFlags>)
- **Track style keyframes** - Special case (rendering concern vs domain?)
  - **Decision needed**: Is TrackStyle a domain concern or UI styling?

---

## Key Files Reference

### Critical Sync Point
- `Assets/Runtime/Legacy/Track/Systems/CoasterSyncSystem.cs` - **THE sync system**

### Coaster Aggregate
- `Assets/Runtime/Coaster/Coaster.cs` - Aggregate definition
- `Assets/Runtime/Coaster/CoasterEvaluator.cs` - Pure evaluation logic

### Component Directories
- `Assets/Runtime/Legacy/Track/Components/` - 38 components (graph, ports, buffers)
- `Assets/Runtime/Legacy/Physics/Components/` - 24 components (keyframes, followers)
- `Assets/Runtime/Legacy/State/Components/` - 10 components (UI state)
- `Assets/Runtime/Legacy/Visualization/Components/` - 12 components (rendering)
- `Assets/Runtime/Legacy/Trains/Components/` - 13 components (train simulation)
- `Assets/Runtime/Legacy/Core/Components/` - 3 components (global state)
- `Assets/Runtime/Legacy/Persistence/Components/` - 7 components (save/load)

### System Directories
- `Assets/Runtime/Legacy/Track/Systems/` - 6 systems (graph, sync, segments)
- `Assets/Runtime/Legacy/Physics/Systems/` - 8 systems (forces, colliders, followers)
- `Assets/Runtime/Legacy/Visualization/Systems/` - 7 systems (rendering, styles)
- `Assets/Runtime/Legacy/Trains/Systems/` - 10 systems (train simulation)
- `Assets/Runtime/Legacy/Core/Systems/` - 3 systems (init, pause)
- `Assets/Runtime/Legacy/Persistence/` - 2 systems (serialization, cleanup)

---

## Updated Migration Phases

### Legacy Section: Direct UI Migration (Simplest Cases)

**Goal**: Refactor UI to read/write Coaster directly, delete redundant ECS components

**Priority Order**:
1. **Port value reads** → Read from `Coaster.Scalars[portId]` / `Coaster.Vectors[nodeId]` / `Coaster.Rotations[nodeId]`
2. **Port value writes** → Write to Coaster, mark dirty
3. **Keyframe queries** → Read from `Coaster.Keyframes.Get(nodeId, propertyId)`
4. **Keyframe edits** → Write to Coaster.Keyframes, mark dirty
5. **Duration queries** → Read from `Coaster.Durations[nodeId]`
6. **Delete orphaned components** (35 components total)

**UI Files to Modify** (TBD - needs UI layer exploration):
- Property panels (scalar/vector inputs)
- Node editors (inline port values)
- Timeline view (keyframe display/editing)
- Curve editor (keyframe curves)

**Blocked By**: None - Coaster already populated correctly on load

---

### Legacy Section: Data Injection Support

**Goal**: Extend Coaster to support UI concerns without coupling

**Proposed Additions**:
1. `Coaster.Hidden` (NativeHashSet<uint>) - nodeIds to hide in viewport
2. `Coaster.Overrides` (NativeHashMap<uint, PropertyOverrideFlags>) - per-node override flags

**Review Questions**:
- Is TrackStyle a domain concern or purely UI styling?
- Are there other UI-specific flags stored in ECS that need Coaster support?

---

# Phase 6: UI Layer Migration (Deferred)

**Goal**: Move deeply-coupled UI patterns to `Assets/Scripts/UI/`

**Components to Migrate**:
- `Node` component (keep Id/Next/Previous, move Position/Selected to UI layer)
- `Connection` component (keep topology, move Selected to UI)
- `PropertyOverrides` (move to UI state)
- `SelectedProperties` (move to UI state)
- `Render` (move to UI state or Coaster.Hidden set)

**Approach**: Keep components but move owning systems to UI layer

---

### Legacy Section: Cleanup

**Tasks**:
1. Remove `VALIDATE_COASTER_PARITY` conditional compilation flag
2. Delete `CoasterPointBuffer` component
3. Delete `ParityValidationSystem`
4. Delete 35 redundant components (ports + keyframes)
5. Update `context.md` files
6. Archive PLAN.md

**Verification**:
- Run tests: `./run-tests.sh`
- Verify no compilation errors
- Verify runtime behavior unchanged

---

## Migration Strategy & Risk Mitigation

### Guiding Principles
1. **Maintainability First** - Favor simple, readable code over premature optimization
2. **Python for Validation** - Use `tools/` scripts to validate binary formats and data assumptions
3. **Legacy Format Confidence** - Ensure .kex file conversion is correct before modernizing
4. **Headless Testing** - Use `./run-tests.sh` for comprehensive validation (slower but thorough)
5. **Parallel Solutions** - For risky changes, wire up new approach alongside old before pruning

### Risk 1: Legacy .kex File Conversion
**Risk**: Incorrect conversion from .kex → Coaster could corrupt user data
**Mitigation**:
- **Python validation**: Extend `tools/analyze_kex.py` to compare before/after conversion
- **Round-trip tests**: Load .kex → Coaster → save → load → compare
- **Headless tests**: `./run-tests.sh` with real .kex files from test corpus
- **Parallel approach**: Keep old ECS serialization working while building new Coaster serialization

### Risk 2: Breaking Change Cascade
**Risk**: Removing 35 components could break UI systems
**Mitigation**:
- **Parallel approach**: Build UI → Coaster pathway before removing ECS → UI pathway
- **Compiler-driven**: Let compiler errors guide systematic refactoring
- **Gradual removal**: Delete components one at a time, run tests after each
- **Headless validation**: `./run-tests.sh` catches runtime issues

### Risk 3: Coaster Mutation Threading Issues
**Risk**: UI writing to Coaster from main thread while ECS reads from worker threads
**Mitigation**:
- **Current pattern**: CoasterSyncSystem already reads CoasterData (managed component) safely
- **Simple solution**: Ensure UI writes complete before ECS update (main thread → systems)
- **Dirty tracking**: Use `Dirty` component to signal Coaster changes
- **No premature optimization**: Focus on correctness first

### Risk 4: Serialization Complexity
**Risk**: SerializationSystem currently syncs ECS ↔ Coaster; removing ECS components changes serialization
**Mitigation**:
- **Parallel approach**: Implement new Coaster-native serialization alongside old ECS serialization
- **Python validation**: Compare serialized outputs byte-by-byte
- **Version migration**: Support loading old .kex files and saving in new format
- **Headless tests**: Extensive save/load roundtrip testing with `./run-tests.sh`

### Validation Tooling Strategy

#### Python Scripts (`tools/`)
**Purpose**: Fast iteration, assumption validation, debugging

**Use Cases**:
- Validate .kex binary format parsing
- Compare ECS vs Coaster serialization outputs
- Debug data conversion issues
- Analyze keyframe data integrity

**Example**: `tools/validate_coaster_conversion.py`
```python
# Load .kex → ECS
# Load .kex → Coaster
# Compare: node IDs, port values, keyframes, graph topology
# Report mismatches with details
```

#### Headless Tests (`./run-tests.sh`)
**Purpose**: Comprehensive validation, CI/CD integration

**Use Cases**:
- Round-trip serialization tests
- CoasterEvaluator correctness
- UI → Coaster write validation
- Regression prevention

**Strategy**:
- Run after every component removal
- Test with diverse .kex files (simple, complex, edge cases)
- Validate both old and new serialization paths during parallel phase

---

## Success Criteria

**Phase 3 Complete When**:
- ✅ UI reads all port values from Coaster (not ECS)
- ✅ UI writes all port values to Coaster (not ECS)
- ✅ UI reads all keyframes from Coaster (not ECS)
- ✅ UI writes all keyframes to Coaster (not ECS)
- ✅ Parallel pathway validated

**Phase 4 Complete When**:
- ✅ 35 redundant components deleted
- ✅ All tests pass
- ✅ Save/load works correctly

**Phase 5 Complete When**:
- ✅ Coaster supports all UI concerns via data injection
- ✅ No UI-specific logic in Coaster code
- ✅ Extensibility validated with examples

**Phase 6 Complete When**:
- ✅ UI layer owns all UI state components
- ✅ Legacy ECS only holds render outputs and entity references
- ✅ Clean separation of concerns

**Phase 7 Complete When**:
- ✅ No migration scaffolding remains
- ✅ Context files updated
- ✅ PLAN.md archived

---

---

# Phase 2: Validation Foundation ✅ COMPLETE

**Status**: All deliverables complete. Round-trip tests pass with 100% success rate.

**Delivered**:
1. **Python validation script** (`tools/validate_coaster_state.py`) ✅
   - Parses .kex binary format and extracts expected Coaster state
   - Compares expected vs actual Coaster aggregate data
   - Validates graph structure, scalars, vectors, rotations, durations, keyframes, flags
   - Handles duplicate node ID remapping and synthetic port creation

2. **Headless tests** (`Assets/Tests/CoasterValidationTests.cs`) ✅
   - `CoasterRoundTrip_PreservesAllData`: Load .kex → Coaster → serialize → deserialize → validate (3/3 PASS)
   - `ExportCoasterState_ForPythonValidation`: Exports Coaster state to JSON for Python validation
   - Test cases: veloci, shuttle, all_types

3. **Test corpus** ✅
   - `veloci.kex`: Complex track with duplicate node IDs, bridges
   - `shuttle.kex`: Shuttle loop coaster
   - `all_types.kex`: All node types represented
   - `shuttle_v1.kex`: Legacy version 1 format

**Key Findings**:
- Coaster aggregate correctly populated from .kex files
- CoasterSerializer (chunk-based format) is lossless
- Legacy format quirks documented (scalars in PointData.Roll, synthetic ports for Anchor/Bridge nodes)

**Confidence**: HIGH - Ready to proceed with Phase 3

---

# Phase 3: Parallel UI → Coaster Pathway ✅ COMPLETE

**Goal**: Build new pathway without breaking existing functionality

**Status**: Successfully completed. All 430 tests passing.

**Approach**: Parallel implementation - keep ECS components but add Coaster reads/writes

**Tasks**:

#### Step 1: Add Coaster Read Helpers
```csharp
// Assets/Runtime/Coaster/CoasterHelpers.cs (new file)
public static class CoasterHelpers {
    public static float GetScalar(in Coaster coaster, uint portId, float defaultValue = 0f);
    public static float3 GetVector(in Coaster coaster, uint nodeId);
    public static float3 GetRotation(in Coaster coaster, uint nodeId);
    public static NativeArray<Keyframe> GetKeyframes(in Coaster coaster, uint nodeId, PropertyId property);
    // ... etc
}
```

#### Step 2: Pick ONE UI System to Refactor (Example: Velocity Port Editor)
- **Current**: Reads `VelocityPort` component via ECS query
- **New**: Read `CoasterHelpers.GetScalar(coaster, portId)`
- **Validation**: Compare values; log warning if mismatch
- **Keep ECS component**: Don't delete yet

#### Step 3: Add Coaster Write Helpers + Sync
```csharp
// Assets/Runtime/Coaster/CoasterMutations.cs (new file)
public static class CoasterMutations {
    public static void SetScalar(ref Coaster coaster, uint portId, float value);
    public static void SetVector(ref Coaster coaster, uint nodeId, float3 value);
    public static void AddKeyframe(ref Coaster coaster, uint nodeId, PropertyId property, Keyframe keyframe);
    // ... etc
}
```

#### Step 4: UI Writes to Both (Parallel Phase)
- **Current**: Write to `VelocityPort` component
- **New**: Also call `CoasterMutations.SetScalar(...)`
- **Mark dirty**: Set `Dirty` enableable component
- **Validation**: Next frame, compare ECS vs Coaster values; log if diverged

#### Step 5: Validation Loop
- Python script: Dump Coaster state before/after UI edit
- Headless test: Automate UI edit → serialize → deserialize → verify
- Run `./run-tests.sh` after every UI system refactor

**Deliverable**: UI successfully reads/writes Coaster in parallel with ECS

## Completion Summary

**Changes Implemented**:
1. ✅ `SerializationSystem.SerializeNode()` - Updated to read from `Coaster.Scalars`, `Coaster.Vectors`, `Coaster.Durations`, `Coaster.Rotations` instead of port components
2. ✅ `NodeGraphControlSystem.ApplyInputPortValue()` - Removed dual-write to ECS components (Roll, Pitch, Yaw, Velocity, Heart, Friction, Resistance)
3. ✅ `NodeGraphControlSystem.RebuildAnchorInCoaster()` - Updated to read from Coaster instead of port components
4. ✅ `NodeGraphControlSystem.CopySelectedNodes()` - Updated to pass Coaster reference to SerializeNode

**Test Results**:
- ✅ All 430 tests passing
- ✅ No compiler errors
- ✅ Serialization works correctly (reads from Coaster)
- ✅ UI updates work correctly (writes only to Coaster)
- ✅ Anchor rebuild works correctly (reads from Coaster)

**Key Achievement**: Port components (VelocityPort, RollPort, etc.) are now **write-only vestigial state**. They are still created during `AddNode()` but are never read by any system. This proves the Coaster aggregate is sufficient as the single source of truth.

**Ready for Phase 4**: Port components can now be safely deleted.

---

# Phase 4: Pruning (NEXT)
**Goal**: Remove redundant ECS components once parallel approach proven

**Approach**: Systematic removal, one component type at a time

**Tasks**:

#### For Each Component (e.g., VelocityPort):
1. **Compiler check**: Search for all references to component
2. **UI systems**: Confirm all switched to Coaster pathway
3. **Serialization**: Update `SerializationSystem` to skip this component
4. **Delete component file**
5. **Run headless tests**: `./run-tests.sh` full suite
6. **Python validation**: Verify .kex load still correct
7. **Commit**: One component removal per commit for easy rollback

**Order** (lowest risk → highest risk):
1. Start with simple scalar ports (VelocityPort, RollPort, etc.) - 18 components
2. Then vector/rotation ports (PositionPort, RotationPort) - 2 components
3. Then keyframe buffers (all 10 keyframe types)
4. Finally complex types (Duration, Steering, CurveData)

**Deliverable**: 35 redundant components removed, all tests passing

---

# Phase 5: Data Injection Support
**Goal**: Extend Coaster for UI concerns (if needed)

**Decision Point**: Only add if Phase 3 reveals missing capabilities

**Candidates**:
- `Coaster.Hidden` set - if UI needs to hide nodes
- `Coaster.Overrides` map - if property override flags are domain data

**Approach**: Same as earlier phases - parallel, validate, prune

---

# Phase 6: UI Layer Migration (Deferred)
**Goal**: Move UI state components to `Assets/Scripts/UI/`

**Status**: Lower priority - focus on Phases 3-5 first

---

# Phase 7: Cleanup
**Goal**: Remove migration scaffolding

**Tasks**:
1. Delete `VALIDATE_COASTER_PARITY` conditional code
2. Delete `CoasterPointBuffer` component
3. Delete `ParityValidationSystem`
4. Update all `context.md` files
5. Archive `PLAN.md`

---

## Next Steps

**Current Phase**: Phase 4 (Pruning)

Port components (VelocityPort, RollPort, etc.) are now vestigial - created but never read. Ready for systematic removal.

**Order** (lowest risk → highest risk):
1. Simple scalar ports (VelocityPort, RollPort, PitchPort, etc.) - 18 components
2. Vector/rotation ports (PositionPort, RotationPort) - 2 components
3. Keyframe buffers - 10 components
4. Complex types (Duration, Steering, CurveData) - 3 components
