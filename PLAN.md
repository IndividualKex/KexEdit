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

**Prerequisite**: View state now serialized via UIST extension (unified UI state)

### Components to Remove
- Duration, Steering, Render ECS components
- Keyframe buffers (10 types): RollSpeedKeyframe, NormalForceKeyframe, etc.

### Progress

**Progress**:
- ✅ Phase 1-2: Foundation (UIStateChunk, PropertyMapping, KeyframeConversion, CoasterKeyframeManager)
- ✅ Phase 3: PropertyAdapter rewrite (Timeline now reads/writes directly to Coaster)
- ⏳ Phase 4: Remove ECS Dependencies (systems, gizmos)
- ⏳ Phase 5: Remove Serialization Layer keyframe logic
- ⏳ Phase 6: Delete 13 component files

### Phase 3 Complete

PropertyAdapter now delegates to CoasterKeyframeManager instead of ECS buffers:
- UIStateData component persists UIStateChunk on coaster entity alongside CoasterData
- TimelineControlSystem initializes CoasterKeyframeManager when entity changes
- Removed SyncKeyframesToCoaster (keyframes now written directly)
- Friction/Resistance scaling via ValueScale property

### Next Steps (Phase 4)

Remove ECS dependencies that read from keyframe buffers:
- Keyframe gizmo systems
- Serialization path keyframe extraction
- Any remaining buffer reads
