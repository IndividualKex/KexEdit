# Migration Plan: ECS-Centric → Coaster-Centric Architecture

## Overview

Migrate from ECS-centric to Coaster-centric architecture, eliminating redundant components that duplicate Coaster aggregate data.

**Key Insight**: The serialization format is extensible (like .glb) - core domain data is serialized by the Coaster aggregate, while UI-specific metadata (node positions, keyframe flags, etc.) is stored in extensions.

## Phase Summary

| Phase | Name | Status |
|-------|------|--------|
| 1 | Component & System Audit | ✅ COMPLETE |
| 2 | Validation Foundation | ✅ COMPLETE |
| 3 | Extensible Serialization | ✅ COMPLETE |
| 4A | KEXD Write Path | ✅ COMPLETE |
| 4B | KEXD Read Path | ✅ COMPLETE |
| 4C | Switch to KEXD-Only | ✅ COMPLETE |
| 4D | KEXD Parity Validation | ✅ COMPLETE |
| 5A | Pruning: Port Components | ✅ COMPLETE |
| 5B | Pruning: UI Layer Components | ⏸️ DEFERRED |
| 6 | Cleanup | ⏸️ Pending |

---

## KEXD File Format

```
KEXD File Format
├── File Header (magic "KEXD" + version)
├── CORE chunk (domain data)
│   ├── GRPH sub-chunk - Node/port/edge topology
│   └── DATA sub-chunk - Keyframes, scalars, vectors, durations, flags
│
└── Extension chunks (UI metadata)
    └── UIMD - Node UI positions (uint nodeId → float2 position)
```

---

## Phase 5A: Port Value Components ✅ COMPLETE

**Goal**: Remove port value components that duplicate Coaster.Scalars/Vectors/Durations

**Result**: 20 files deleted, 458 tests passing

### Removed Components
VelocityPort, RollPort, PitchPort, YawPort, FrictionPort, ResistancePort, HeartPort, DurationPort, PositionPort, RotationPort, ScalePort, RadiusPort, ArcPort, AxisPort, LeadInPort, LeadOutPort, InWeightPort, OutWeightPort, StartPort, EndPort, AnchorPort

### Kept Components
- `Port.cs` - Base topology component (Id, Type, IsInput)

### Files Modified
- `SerializationSystem.cs` - Simplified port serialization/deserialization
- `NodeGraphControlSystem.cs` - Removed port component additions in AddNode
- `TimelineControlSystem.cs` - Removed DurationPort write (now only updates Coaster)
- `KexdParityTests.cs` - Updated to verify values in Coaster instead of ECS components

---

## Phase 5B: UI Layer Components (DEFERRED)

**Goal**: Remove keyframe buffers and other duplicate components

**Status**: Deferred - requires UI refactoring

### Components Identified
- **Keyframe buffers** (10 types): RollSpeedKeyframe, NormalForceKeyframe, etc.
- **Other duplicates** (4 types): Duration, Steering, CurveData, PropertyOverrides

### Architecture Finding
These components serve as a **UI "view model"** layer:
- ECS buffers/components are the working copy for timeline/node graph editing
- Changes are synced to Coaster via `SyncKeyframeBuffer` and similar patterns
- Coaster remains source of truth for persistence and evaluation

Removing these requires refactoring UI systems to work directly with Coaster, which is a larger undertaking. The current dual-layer architecture (ECS for UI, Coaster for domain) is functional.

---

## Phase 6: Cleanup

1. Remove `VALIDATE_COASTER_PARITY` conditional code
2. Delete `CoasterPointBuffer`, `ParityValidationSystem`
3. Update `context.md` files
4. Archive this plan

---

## Key Files

### Core Serialization
- `Assets/Runtime/Persistence/CoasterSerializer.cs` - CORE chunk read/write
- `Assets/Runtime/Persistence/Extensions/ExtensionSerializer.cs` - UIMD chunk
- `Assets/Runtime/Legacy/Persistence/Serialization/SerializationSystem.cs` - KEXD integration

### Adapters
- `Assets/Runtime/Legacy/KexdAdapter.cs` - Coaster → ECS entities
- `Assets/Runtime/Legacy/LegacyImporter.cs` - SerializedGraph → Coaster (legacy file support)

### Tests
- `Assets/Tests/KexdRoundTripTests.cs` - Format validation
- `Assets/Tests/KexdParityTests.cs` - Legacy/KEXD parity
