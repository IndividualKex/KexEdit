# Migration Plan: ECS-Centric → Coaster-Centric Architecture

## Overview

Migrate from ECS-centric to Coaster-centric architecture, eliminating redundant components that duplicate Coaster aggregate data.

**Key Insight**: The serialization format must be extensible (like .glb) - core domain data is serialized by the Coaster aggregate, while UI-specific metadata (keyframe selection, flags, etc.) is stored in extensions that the core format doesn't need to understand.

## Phase Summary

| Phase | Name | Status |
|-------|------|--------|
| 1 | Component & System Audit | ✅ COMPLETE |
| 2 | Validation Foundation | ✅ COMPLETE |
| 3 | Extensible Serialization | 🔄 IN PROGRESS |
| 4 | UI → Coaster Pathway | ⏸️ Blocked on Phase 3 |
| 5 | Pruning | ⏸️ Pending |
| 6 | UI Layer Migration | ⏸️ Deferred |
| 7 | Cleanup | ⏸️ Pending |

---

# Completed Phases (Summary)

## Phase 1: Component & System Audit ✅

- **107 ECS components** identified; **36 ECS systems**
- **35 removable components**: 25 port value + 10 keyframe components duplicate Coaster data
- **1 primary sync**: CoasterSyncSystem (Coaster → CorePointBuffer)
- Full audit preserved in git history

## Phase 2: Validation Foundation ✅

- Round-trip tests pass with 100% success rate
- Test corpus: veloci.kex, shuttle.kex, all_types.kex
- CoasterSerializer (chunk-based format) is lossless

---

# Phase 3: Extensible Serialization 🔄 IN PROGRESS

## Problem

The `Core.Keyframe` struct is a pure domain model:
```csharp
public readonly struct Keyframe {
    public readonly float Time, Value;
    public readonly InterpolationType InInterpolation, OutInterpolation;
    public readonly float InTangent, OutTangent, InWeight, OutWeight;
}
```

But legacy serialization needs UI-specific fields:
- `Id` - Unique keyframe identifier for UI selection tracking
- `HandleType` - UI curve editor handle mode
- `Flags` - Time/value lock flags
- `Selected` - UI selection state

**Wrong approach**: Add these fields to `Core.Keyframe` (pollutes domain model)

**Correct approach**: Serialization format supports extensions for UI metadata

## Design: Extension-Based Serialization

Like .glb, the serialization format should:
1. Serialize core domain data (Coaster aggregate)
2. Allow extensions to store additional metadata without core awareness
3. Extensions are optional - missing extensions use defaults

### Extension Architecture

```
CoasterSerializer (existing chunk format)
├── Core chunks (domain data)
│   ├── GRAPH - Node topology
│   ├── SCALAR - Port scalar values
│   ├── VECTOR - Node positions
│   ├── ROTATION - Node rotations
│   ├── DURATION - Node durations
│   ├── KEYFRAME - Animation curves (Core.Keyframe)
│   └── FLAGS - Driven/Steering sets
│
└── Extension chunks (UI metadata)
    ├── KFMETA - Keyframe metadata (Id, HandleType, Flags, Selected)
    ├── UIPOS - Node graph UI positions
    ├── UISEL - UI selection state
    └── ... (future extensions)
```

### Keyframe Extension Design

**Core keyframe data** (in KEYFRAME chunk):
- Time, Value, tangents, weights, interpolation types

**Extension metadata** (in KFMETA chunk):
- Maps `(nodeId, propertyId, keyframeIndex)` → `KeyframeMetadata`
- `KeyframeMetadata { uint Id; HandleType HandleType; KeyframeFlags Flags; }`
- `Selected` is transient (not serialized)

### Missing Extension: Node UI Position

The `Node` component stores 2D graph UI position (`float2 Position`), which is currently serialized with the node. This should move to a UI extension.

## Tasks

### 3.1: Design Extension Format
- Define chunk types for extensions
- Define `KeyframeMetadata` struct
- Define `NodeUIMetadata` struct (Position, Selected)

### 3.2: Implement Extension Reading/Writing
- Add extension chunk support to `CoasterSerializer`
- Write extension data alongside core data
- Read extension data and populate UI state

### 3.3: Update SerializationSystem
- Read core data from Coaster aggregate
- Read/write extensions for UI metadata
- Remove reads from port/keyframe ECS components

### 3.4: Validation
- Round-trip tests with extensions
- Backwards compatibility: load files without extensions (use defaults)

**Deliverable**: Serialization reads from Coaster + extensions, not ECS components

---

# Phase 4: UI → Coaster Pathway

**Goal**: UI reads/writes directly to Coaster aggregate

**Blocked by**: Phase 3 (extensible serialization)

## Partial Progress

**Completed** ✅:
- `NodeGraphControlSystem.ApplyInputPortValue()` - Writes to Coaster only
- `NodeGraphControlSystem.RebuildAnchorInCoaster()` - Reads from Coaster
- `NodeGraphControlSystem.CopySelectedNodes()` - Uses Coaster

**Remaining** ⏸️:
- Complete SerializationSystem migration (blocked on extensions)

---

# Phase 5: Pruning

**Goal**: Delete redundant ECS components

**Prerequisites**: Phases 3 & 4 complete

## Components to Remove (35 total)

### Port Components (22 files)
VelocityPort, RollPort, PitchPort, YawPort, FrictionPort, ResistancePort, HeartPort, DurationPort, PositionPort, RotationPort, ScalePort, RadiusPort, ArcPort, AxisPort, LeadInPort, LeadOutPort, InWeightPort, OutWeightPort, StartPort, EndPort, AnchorPort

**Keep**: `Port.cs` (base topology component)

### Keyframe Components (10 files)
RollSpeedKeyframe, NormalForceKeyframe, LateralForceKeyframe, PitchSpeedKeyframe, YawSpeedKeyframe, FixedVelocityKeyframe, HeartKeyframe, FrictionKeyframe, ResistanceKeyframe, TrackStyleKeyframe

### Other Components (3 files)
Duration, Steering, CurveData

## Approach
- Batch delete by category
- Fix compiler errors as found
- Test once per batch

---

# Phase 6: UI Layer Migration (Deferred)

Move UI state components to `Assets/Scripts/UI/`

---

# Phase 7: Cleanup

1. Remove `VALIDATE_COASTER_PARITY` conditional code
2. Delete `CoasterPointBuffer`, `ParityValidationSystem`
3. Update `context.md` files
4. Archive `PLAN.md`

---

# Key Files

## Serialization
- `Assets/Runtime/Persistence/ChunkIO/` - Chunk-based format
- `Assets/Runtime/Persistence/CoasterSerializer.cs` - Core serializer
- `Assets/Runtime/Legacy/Persistence/Serialization/SerializationSystem.cs` - ECS bridge

## Coaster Aggregate
- `Assets/Runtime/Coaster/Coaster.cs` - Aggregate definition
- `Assets/Runtime/Core/Keyframe.cs` - Domain keyframe struct
- `Assets/Runtime/Nodes/Storage/KeyframeStore.cs` - Keyframe storage

## Legacy Components (to remove)
- `Assets/Runtime/Legacy/Track/Components/` - Port components
- `Assets/Runtime/Legacy/Physics/Components/` - Keyframe components

---

# Next Steps

1. **Design extension chunk format** for keyframe metadata
2. **Implement KFMETA extension** in CoasterSerializer
3. **Update SerializationSystem** to read from Coaster + extensions
4. **Validate** with round-trip tests
5. **Proceed to Phase 4** once serialization is extension-based
