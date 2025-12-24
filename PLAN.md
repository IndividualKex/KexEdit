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
| 4E | UI Read Migration | ⏳ NEXT |
| 5A | Pruning: Port Components | ✅ COMPLETE |
| 5B | Pruning: UI Layer Components | ⏸️ DEFERRED |
| 6 | Cleanup | ⏸️ Pending |

---

## Phase 4E: UI Read Migration ⏳ NEXT

**Goal**: Migrate UI reads from ECS "view model" components to Coaster aggregate

**Problem**: Phase 5A removed port components but left UI systems reading from ECS components (Duration, Render) that are no longer updated on edit. This causes values to revert on save/load.

**Result**: Duration and Render changes persist correctly through save/load

### Current State (Bug)

| Property | Read Source | Write Target | Status |
|----------|-------------|--------------|--------|
| Duration.Value | ECS Duration | Coaster.Scalars | BROKEN - read/write mismatch |
| Duration.Type | ECS Duration | Coaster.Flags | BROKEN - read/write mismatch |
| Render | ECS Render | ECS only | Missing Coaster.Flags write |

### Implementation Steps

#### Step 1: Add Render flag write to NodeGraphControlSystem

**File:** `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs:837-840`

```csharp
// BEFORE
private void OnRenderToggleChange(RenderToggleChangeEvent evt) {
    ref var render = ref SystemAPI.GetComponentRW<Render>(evt.Node).ValueRW;
    render.Value = evt.Render;
}

// AFTER
private void OnRenderToggleChange(RenderToggleChangeEvent evt) {
    ref var render = ref SystemAPI.GetComponentRW<Render>(evt.Node).ValueRW;
    render.Value = evt.Render;

    uint nodeId = SystemAPI.GetComponent<Node>(evt.Node).Id;
    ref var coaster = ref GetCoasterRef();
    ulong renderKey = CoasterAggregate.InputKey(nodeId, NodeMeta.Render);
    coaster.Flags[renderKey] = evt.Render ? 0 : 1;  // Inverted: 0=render, 1=hidden

    SystemAPI.SetComponentEnabled<Dirty>(evt.Node, true);
}
```

#### Step 2: Migrate HasEditableDuration()

**File:** `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs:629-631`

```csharp
// BEFORE
private bool HasEditableDuration() {
    return SystemAPI.HasComponent<Duration>(_data.Entity);
}

// AFTER
private bool HasEditableDuration() {
    if (_data.Entity == Entity.Null) return false;
    var nodeType = SystemAPI.GetComponent<Node>(_data.Entity).Type;
    return nodeType == NodeType.ForceSection || nodeType == NodeType.GeometricSection;
}
```

#### Step 3: Migrate GetDuration()

**File:** `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs:616-627`

```csharp
// BEFORE
private float GetDuration() {
    if (SystemAPI.HasComponent<Duration>(_data.Entity)) {
        return SystemAPI.GetComponent<Duration>(_data.Entity).Value;
    }
    // ... fallback to point buffer ...
}

// AFTER
private float GetDuration() {
    if (HasEditableDuration()) {
        uint nodeId = SystemAPI.GetComponent<Node>(_data.Entity).Id;
        ref var coaster = ref GetCoasterRef();
        ulong durKey = CoasterAggregate.InputKey(nodeId, NodeMeta.Duration);
        if (coaster.Scalars.TryGetValue(durKey, out float duration)) {
            return duration;
        }
    }
    // ... fallback to point buffer unchanged ...
}
```

#### Step 4: Migrate GetDurationType()

**File:** `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs:633-638`

```csharp
// BEFORE
private DurationType GetDurationType() {
    if (_data.Active && SystemAPI.HasComponent<Duration>(_data.Entity)) {
        return SystemAPI.GetComponent<Duration>(_data.Entity).Type;
    }
    return DurationType.Time;
}

// AFTER
private DurationType GetDurationType() {
    if (_data.Active && HasEditableDuration()) {
        uint nodeId = SystemAPI.GetComponent<Node>(_data.Entity).Id;
        ref var coaster = ref GetCoasterRef();
        ulong durTypeKey = CoasterAggregate.InputKey(nodeId, NodeMeta.DurationType);
        if (coaster.Flags.TryGetValue(durTypeKey, out int durType) && durType == 1) {
            return DurationType.Distance;
        }
    }
    return DurationType.Time;
}
```

#### Step 5: Update OnDurationChange validation

**File:** `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs:1096-1099`

```csharp
// BEFORE
if (!SystemAPI.HasComponent<Duration>(_data.Entity)) {

// AFTER
if (!HasEditableDuration()) {
```

#### Step 6: Update Duration reads in UpdatePlayhead (lines 235-257)

Replace `SystemAPI.HasComponent<Duration>` and `SystemAPI.GetComponent<Duration>` with `HasEditableDuration()` and `GetDurationType()`.

#### Step 7: Update Duration reads in GetPointAtTime (lines 1733-1745)

Same pattern as Step 6.

### Risks

1. **Render flag inversion**: Coaster uses `0=render, 1=hidden` (per KexdAdapter.cs:91)
2. **Performance**: GetCoasterRef() in hot paths - monitor if issues arise

### Verification

1. Run `./run-tests.sh`
2. Manual test: Change Duration → Save → Load → Verify value persists
3. Manual test: Toggle Render → Save → Load → Verify state persists

---

## Phase 5B: UI Layer Components (DEFERRED)

**Goal**: Remove keyframe buffers and other duplicate ECS components

**Prerequisite**: Phase 4E must be complete first

### Components to Remove (after UI migration)
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

### Phase 4E Implementation
- `Assets/Scripts/UI/Timeline/Systems/TimelineControlSystem.cs` - Duration read migration
- `Assets/Scripts/UI/NodeGraph/Systems/NodeGraphControlSystem.cs` - Render flag write

### Reference (for understanding patterns)
- `Assets/Runtime/Coaster/Coaster.cs` - NodeMeta constants (Duration=248, DurationType=250, Render=254)
- `Assets/Runtime/Legacy/KexdAdapter.cs:89-91` - Render flag inversion semantics
