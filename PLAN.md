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
| 4E | UI Read Migration | ✅ COMPLETE |
| 5A | Pruning: Port Components | ✅ COMPLETE |
| 5B | Pruning: UI Layer Components | ⏳ NEXT |
| 6 | Cleanup | ⏸️ Pending |

---

## Phase 5B: Pruning UI Layer Components ⏳ IN PROGRESS

**Goal**: Remove keyframe buffers and other duplicate ECS components

**Prerequisite**: View state now serialized via VWST extension (timeline/graph/camera state)

### Components to Remove
- Duration, Steering, Render ECS components
- Keyframe buffers (10 types): RollSpeedKeyframe, NormalForceKeyframe, etc.

### Progress

**Foundation Complete (Phases 1-2)**:
- ✅ KeyframeUIChunk/KeyframeUICodec - UI state storage (KFUI extension chunk)
- ✅ PropertyMapping - PropertyType ↔ PropertyId bidirectional mapping
- ✅ KeyframeConversion - Core.Keyframe ↔ Legacy.Keyframe conversion
- ✅ CoasterKeyframeManager - Unified Coaster.KeyframeStore + KeyframeUIChunk API

**Remaining Work**:
- ⏳ Phase 3: Rewrite PropertyAdapter to use CoasterKeyframeManager (HIGH RISK)
- ⏳ Phase 4: Remove ECS Dependencies (systems, gizmos)
- ⏳ Phase 5: Remove Serialization Layer keyframe logic
- ⏳ Phase 6: Delete 13 component files

### Key Design Decisions

**UI State Storage**: KeyframeUIChunk stores sparse UI-only metadata (Id, HandleType, Flags) separately from Core.Keyframe domain data. Selection state is transient (not persisted).

**ID Assignment**: Persistent keyframe IDs generated using composite key: `(nodeId << 16) | (counter & 0xFFFF)`. IDs maintained across add/remove operations via index tracking.

**PropertyType Mapping**: FixedVelocity↔DrivenVelocity, Heart↔HeartOffset due to enum name differences between UI and Core layers.

### Files Created
1. `Assets/Runtime/Persistence/Extensions/KeyframeUIChunk.cs`
2. `Assets/Runtime/Persistence/Extensions/KeyframeUICodec.cs`
3. `Assets/Scripts/UI/Timeline/PropertyMapping.cs`
4. `Assets/Scripts/UI/Timeline/KeyframeConversion.cs`
5. `Assets/Scripts/UI/Timeline/CoasterKeyframeManager.cs`
6. `Assets/Tests/KeyframeUITests.cs` (21 unit tests)

### Layer Separation
KeyframeUIState uses primitive bytes for HandleType/Flags to maintain layer separation (Persistence doesn't depend on Legacy). Conversion to/from enums happens in CoasterKeyframeManager.

### Next Steps

**Phase 3** is critical - PropertyAdapter is the Timeline UI integration point. Must:
1. Initialize CoasterKeyframeManager with Coaster reference
2. Rewrite all 10 adapter classes to use CoasterKeyframeManager
3. Update TimelineControlSystem to pass nodeId instead of Entity
4. Mark Coaster dirty after modifications
5. Thoroughly test Timeline/Curve editor for each property type

---

## Phase 6: Cleanup

1. Remove `VALIDATE_COASTER_PARITY` conditional code
2. Delete `CoasterPointBuffer`, `ParityValidationSystem`
3. Update `context.md` files
4. Archive this plan

---

## Key Files

### Reference (for understanding patterns)
- `Assets/Runtime/Coaster/Coaster.cs` - NodeMeta constants (240-254)
- `Assets/Runtime/Legacy/KexdAdapter.cs:89-91` - Render flag inversion semantics
