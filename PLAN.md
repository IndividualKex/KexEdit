# Migration Plan: ECS-Centric → Coaster-Centric Architecture

## Overview

Migrate from ECS-centric to Coaster-centric architecture, eliminating redundant components that duplicate Coaster aggregate data.

**Key Insight**: The serialization format is extensible (like .glb) - core domain data is serialized by the Coaster aggregate, while UI-specific metadata (node positions, keyframe flags, etc.) is stored in extensions that the core format doesn't need to understand.

## Phase Summary

| Phase | Name | Status |
|-------|------|--------|
| 1 | Component & System Audit | ✅ COMPLETE |
| 2 | Validation Foundation | ✅ COMPLETE |
| 3 | Extensible Serialization | ✅ COMPLETE |
| 4 | UI → Coaster Pathway | ⏸️ Next |
| 5 | Pruning | ⏸️ Pending |
| 6 | UI Layer Migration | ⏸️ Deferred |
| 7 | Cleanup | ⏸️ Pending |

---

# Completed Phases (Summary)

## Phase 1: Component & System Audit ✅

- **107 ECS components** identified; **36 ECS systems**
- **35 removable components**: 25 port value + 10 keyframe components duplicate Coaster data
- **1 primary sync**: CoasterSyncSystem (Coaster → CorePointBuffer)

## Phase 2: Validation Foundation ✅

- Round-trip tests pass with 100% success rate
- Test corpus: veloci.kex, shuttle.kex, all_types.kex
- CoasterSerializer (chunk-based format) is lossless

## Phase 3: Extensible Serialization ✅

Data-driven extension system implemented for Rust FFI compatibility.

### Extension Architecture

```
KEXD File Format
├── File Header (magic + version)
├── CORE chunk (domain data)
│   ├── GRPH sub-chunk - Node/port/edge topology
│   └── DATA sub-chunk - Keyframes, scalars, vectors, durations, flags
│
└── Extension chunks (UI metadata, optional)
    ├── UIMD - Node UI positions (float2 per node)
    └── KFMD - Keyframe metadata (Id, HandleType, Flags) [future]
```

### Design: Data-Driven (No Interfaces)

Following the Rust `NodeSchema` pattern:

```
Assets/Runtime/Persistence/Extensions/
├── ExtensionSchema.cs      # Static schema (chunk types, versions)
├── UIMetadataChunk.cs      # Pure data struct
├── UIMetadataIO.cs         # Static read/write functions
├── ExtensionSerializer.cs  # Orchestration (explicit dispatch)
└── ExtensionData.cs        # Container for all extensions
```

**Key principles**:
- No interfaces (virtual dispatch incompatible with Rust FFI)
- Pure data structs + static functions
- Explicit dispatch via switch (like Rust pattern matching)
- Separation: chunk format (IO) separate from runtime storage

### Validation Tools

- `tools/analyze_kexd.py` - Python script to validate KEXD chunk structure
- `ExtensionSerializerTests.cs` - Round-trip tests for extensions

---

# Phase 4: UI → Coaster Pathway

**Goal**: UI reads/writes directly to Coaster aggregate

## Partial Progress

**Completed** ✅:
- `NodeGraphControlSystem.ApplyInputPortValue()` - Writes to Coaster only
- `NodeGraphControlSystem.RebuildAnchorInCoaster()` - Reads from Coaster
- `NodeGraphControlSystem.CopySelectedNodes()` - Uses Coaster

**Remaining**:
- Wire up `ExtensionSerializer` to save/load UI metadata
- Update `SerializationSystem` to use new extension system

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
- `Assets/Runtime/Persistence/` - Chunk-based format
- `Assets/Runtime/Persistence/Extensions/` - Data-driven extension system
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

1. **Wire up extension system** in SerializationSystem
2. **Validate** with round-trip tests including UI metadata
3. **Proceed to Phase 4** - complete UI → Coaster pathway
4. **Phase 5** - prune redundant ECS components
