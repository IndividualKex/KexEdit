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

## Phase 5B: Pruning UI Layer Components ⏳ NEXT

**Goal**: Remove keyframe buffers and other duplicate ECS components

**Prerequisite**: View state now serialized via VWST extension (timeline/graph/camera state)

### Components to Remove
- Duration, Steering, Render ECS components
- Keyframe buffers (10 types): RollSpeedKeyframe, NormalForceKeyframe, etc.

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
