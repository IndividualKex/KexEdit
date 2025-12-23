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
| 5 | Pruning | ⏸️ Pending |
| 6 | Cleanup | ⏸️ Pending |

---

# Completed Phases

## Phase 1-3: Summary

- **107 ECS components** audited; **35 removable** (port values + keyframe duplicates)
- Round-trip tests pass with CoasterSerializer
- Extension system implemented: UIMetadataChunk for node positions

## Phase 4A: Summary

- **SerializeToKEXD** method implemented in SerializationSystem
- **5 tests passing**: KexdRoundTripTests (3) + KexdIntegrationTests (2)
- **Debug hook** added to FileManager with `DEBUG_KEXD_FORMAT` flag
- Python tool already validates KEXD format
- Legacy save/load unchanged

## Phase 4B: Summary

- **Format detection** by "KEXD" magic header in `IsKexdFormat()`
- **DeserializeKexd** reads CORE + UIMD chunks, creates ECS entities
- **KexdAdapter** converts Coaster aggregate → ECS entities (nodes, ports, connections)
- **9 tests passing**: KexdRoundTripTests (7) + KexdIntegrationTests (2)
- Legacy `.kex` files still loadable via `DeserializeLegacy` path

### KEXD File Format

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

# Phase 4A: KEXD Write Path

**Goal**: Add KEXD output for validation without changing load behavior

## Strategy: Parallel Output

Write KEXD alongside existing operations for testing. This lets us validate the new format before switching.

## Tasks

### 1. Create SerializeToKEXD method

`SerializationSystem.cs`:

```csharp
public byte[] SerializeToKEXD(Entity target) {
    ref readonly var coasterData = ref SystemAPI.GetComponentRW<CoasterData>(target).ValueRO.Value;

    // Build UI metadata from ECS Node.Position
    var uiMeta = new UIMetadataChunk(Allocator.Temp);
    using var nodeEntities = _nodeQuery.ToEntityArray(Allocator.Temp);
    foreach (var entity in nodeEntities) {
        if (SystemAPI.GetComponent<CoasterReference>(entity).Value != target) continue;
        var node = SystemAPI.GetComponent<Node>(entity);
        uiMeta.Positions[node.Id] = node.Position;
    }

    // Write KEXD format
    var writer = new ChunkWriter(Allocator.Temp);
    CoasterSerializer.Write(writer, in coasterData);
    ExtensionSerializer.WriteUIMetadata(ref writer, in uiMeta);

    var data = writer.ToArray();
    var result = data.ToArray(); // Copy to managed array

    writer.Dispose();
    uiMeta.Dispose();
    data.Dispose();

    return result;
}
```

### 2. Add Validation Test

`KexdRoundTripTests.cs`:

```csharp
[Test]
public void SerializeToKEXD_ProducesValidFormat() {
    // Create coaster entity with nodes
    // Call SerializeToKEXD()
    // Validate with ChunkReader (correct magic, chunks present)
    // Parse with CoasterSerializer + ExtensionSerializer
    // Verify node count, positions match
}
```

### 3. Python Validation

Extend `tools/analyze_kexd.py` to:
- Accept file path argument
- Report chunk structure, node count, positions
- Validate checksums if applicable

### 4. Integration Hook

Add debug flag to write both formats during save:

```csharp
#if DEBUG_KEXD_FORMAT
var kexdData = SerializeToKEXD(target);
File.WriteAllBytes(path + ".kexd", kexdData);
#endif
```

## Definition of Done

- [x] `SerializeToKEXD()` implemented and tested
- [x] Python tool validates KEXD output (existing tool already supports this)
- [x] Round-trip test passes (KexdRoundTripTests - 3 tests passing)
- [x] Legacy save/load unchanged
- [x] Debug flag `DEBUG_KEXD_FORMAT` added for parallel output testing

---

# Phase 4B: KEXD Read Path

**Goal**: Add ability to load KEXD files with format detection

## Strategy: Format Detection + Adapter

Detect format by magic number. For KEXD, use an adapter to convert Coaster → SerializedGraph, allowing reuse of existing DeserializeNode.

## Tasks

### 1. Add Format Detection

```csharp
private static bool IsKEXDFormat(byte[] data) {
    return data.Length >= 4 &&
           data[0] == 'K' && data[1] == 'E' &&
           data[2] == 'X' && data[3] == 'D';
}
```

### 2. Create KEXD → SerializedGraph Adapter

`KexdAdapter.cs`:

```csharp
public static class KexdAdapter {
    public static SerializedGraph ToSerializedGraph(
        in Coaster coaster,
        in ExtensionData extensions,
        Allocator allocator) {
        // Build SerializedNode[] from Coaster.Graph
        // Extract keyframes from coaster.Keyframes
        // Extract port values from coaster.Scalars/Vectors
        // Apply positions from extensions.UIMetadata
        // Return SerializedGraph
    }
}
```

### 3. Update DeserializeGraph

```csharp
public Entity DeserializeGraph(byte[] data, bool restoreUIState = true) {
    if (IsKEXDFormat(data)) {
        return DeserializeFromKEXD(data, restoreUIState);
    }
    // ... existing legacy path
}

private Entity DeserializeFromKEXD(byte[] data, bool restoreUIState) {
    var reader = new ChunkReader(data);
    var coaster = CoasterSerializer.Read(reader, Allocator.Persistent);
    reader.Dispose();

    var reader2 = new ChunkReader(data);
    var extensions = ExtensionSerializer.ReadExtensions(ref reader2, Allocator.Temp);
    reader2.Dispose();

    var serializedGraph = KexdAdapter.ToSerializedGraph(in coaster, in extensions, Allocator.Temp);
    extensions.Dispose();

    // Create ECS entity, reuse existing node creation logic
    var coasterEntity = EntityManager.CreateEntity(typeof(Coaster), typeof(CoasterData));
    EntityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

    // ... create ECS entities via existing DeserializeNode
    // ... restore UI state if needed

    serializedGraph.Dispose();
    return coasterEntity;
}
```

### 4. Add Bi-directional Test

```csharp
[Test]
public void KEXD_RoundTrip_PreservesAllData() {
    // Create coaster with nodes, keyframes, positions
    // SerializeToKEXD()
    // DeserializeFromKEXD()
    // Verify all data matches
}
```

## Definition of Done

- [x] Format detection works
- [x] KexdAdapter converts Coaster → ECS entities
- [x] DeserializeFromKEXD creates correct ECS entities
- [x] Bi-directional round-trip test passes (7 tests)
- [x] Both formats loadable (backwards compatibility)

---

## Phase 4C: Summary

- **SerializeGraph** now delegates to SerializeToKEXD - all new saves use KEXD format
- **Format detection** moved to UI layer (`ProjectOperations.OpenProject`)
- Legacy files automatically marked as unsaved to prompt re-save in KEXD format
- **File extension**: `.kex` retained for both formats (KEXD detected by magic header)
- **IsValidFileHeader** updated to recognize both KEXD magic and legacy version
- **DEBUG_KEXD_FORMAT** code removed (no longer needed)

---

# Phase 4D: KEXD Parity Validation

**Goal**: Achieve full parity between legacy `.kex` and KEXD round-trip

**Status**: ✅ COMPLETE

## Summary

- **452 headless tests passing** (450 original + 2 new parity tests)
- **Rotation consolidation fixed**: Roll/Pitch/Yaw ports now properly consolidated into Rotation
- **UI position drift fixed**: Synthetic Anchor nodes now preserve positions through KEXD round-trip

## Issues Fixed

### 1. Rotation Port Consolidation
**Root cause**: `LegacyImporter.ImportPortValues` stored Roll/Pitch/Yaw as individual scalars but never called `SetRotation()` to store the combined rotation value. When `KexdAdapter` exported back, it called `GetRotation()` which returned zero.

**Fix**: Updated `LegacyImporter.ImportPortValues` to track Roll/Pitch/Yaw values during import and consolidate them into a single `SetRotation()` call at the end.

### 2. UI Position Drift
**Root cause**: Synthetic Anchor nodes created by `LegacyImporter.ImportBridgeTargets` were added to `Coaster.Graph.NodePositions` but didn't have corresponding ECS entities. When `SerializeToKEXD` wrote UIMD, it only iterated ECS entities, missing the synthetic nodes. On reload, `KexdAdapter` couldn't find positions for these nodes in UIMD.

**Fix**:
1. Updated `SerializeToKEXD` to first populate UIMD from `Graph.NodePositions` (includes synthetic nodes), then override with ECS entity positions (for nodes that may have moved in UI)
2. Updated `KexdAdapter.BuildSerializedNode` to fall back to `Graph.NodePositions` if UIMD doesn't have a position for a node

## Approach

### Step 1: Python Validation Tool
Extend `tools/validate_kexd_parity.py` to compare legacy vs KEXD:

```python
def compare_legacy_kexd(legacy_path: str, kexd_path: str):
    """Compare graphs from legacy .kex and KEXD formats."""
    legacy = parse_legacy(legacy_path)
    kexd = parse_kexd(kexd_path)

    # Compare node counts, types
    # Compare port counts per node, port types
    # Compare edge topology (source/target port IDs)
    # Compare scalar values (with tolerance)
    # Compare UI positions
    # Report all differences
```

**Validation checks**:
- [ ] Node count matches
- [ ] Node types match
- [ ] Port count per node matches
- [ ] Port types match (Roll/Pitch/Yaw vs Rotation)
- [ ] Edge count matches
- [ ] Edge source/target port IDs match
- [ ] Scalar values within tolerance (0.001)
- [ ] Vector values within tolerance
- [ ] UI positions within tolerance

### Step 2: Identify Root Causes
Use Python tool to pinpoint exact differences:
```bash
# Load legacy, save as KEXD, compare
python tools/validate_kexd_parity.py Assets/Tests/Assets/shuttle.kex --compare-roundtrip
```

### Step 3: Fix KexdAdapter
Based on findings, update `KexdAdapter.cs`:
- Fix rotation port handling (preserve Roll/Pitch/Yaw separation if needed)
- Fix any UI position edge cases

### Step 4: Add Headless Parity Tests
`Assets/Tests/KexdParityTests.cs`:

```csharp
[Test]
public void LegacyToKexd_PreservesRotationPorts() {
    // Load shuttle.kex via legacy path
    // Serialize to KEXD
    // Deserialize KEXD
    // Verify Roll, Pitch, Yaw ports exist separately
    // Verify values match original
}

[Test]
public void LegacyToKexd_PreservesAllUIPositions() {
    // Load all_types.kex via legacy path
    // Serialize to KEXD
    // Deserialize KEXD
    // Verify all node positions within tolerance
}

[Test]
public void LegacyToKexd_FullParity() {
    // Comprehensive test with shuttle.kex
    // Compare all nodes, ports, edges, values
}
```

### Step 5: Regression Suite
Ensure all test files round-trip correctly:
- `veloci.kex`
- `shuttle.kex`
- `all_types.kex`
- `shuttle_v1.kex`

## Definition of Done

- [x] Python validation tool extended with legacy comparison
- [x] Rotation values preserved correctly (consolidated to single Rotation)
- [x] UI positions stable after round-trip (including synthetic nodes)
- [x] `LegacyBridge_RollPitchYaw_ConsolidatedToRotation` test added
- [x] `SyntheticAnchor_UIPosition_PreservedOnRoundTrip` test added
- [x] 452 tests passing

---

# Phase 5: Pruning

**Goal**: Remove redundant ECS components now that Coaster is source of truth

**Prerequisites**: Phase 4D complete (full parity validated)

## Components to Remove (35 total)

### Port Value Components (21 files)
VelocityPort, RollPort, PitchPort, YawPort, FrictionPort, ResistancePort, HeartPort, DurationPort, PositionPort, RotationPort, ScalePort, RadiusPort, ArcPort, AxisPort, LeadInPort, LeadOutPort, InWeightPort, OutWeightPort, StartPort, EndPort

**Keep**: `Port.cs` (base topology), `AnchorPort.cs` (output computed values)

### Keyframe Components (10 files)
RollSpeedKeyframe, NormalForceKeyframe, LateralForceKeyframe, PitchSpeedKeyframe, YawSpeedKeyframe, FixedVelocityKeyframe, HeartKeyframe, FrictionKeyframe, ResistanceKeyframe, TrackStyleKeyframe

### Other Duplicate Components (4 files)
Duration, Steering, CurveData, PropertyOverrides

## Pruning Strategy

### Batch 1: Port Value Components
1. Delete files
2. Fix compiler errors (update SerializeNode, DeserializeNode, KexdAdapter)
3. Test: `./run-tests.sh`

### Batch 2: Keyframe Components
1. Delete files
2. Fix compiler errors (remove buffer management from DeserializeNode)
3. Test: `./run-tests.sh`

### Batch 3: Other Components
1. Delete Duration, Steering, etc.
2. Update systems that read these (redirect to Coaster)
3. Test: `./run-tests.sh`

## Definition of Done

- [ ] 35 redundant component files deleted
- [ ] ECS entities are topology-only (Node, Port, Connection)
- [ ] All data reads/writes go through Coaster aggregate
- [ ] All tests pass

---

# Phase 6: Cleanup

1. Remove `VALIDATE_COASTER_PARITY` conditional code
2. Delete `CoasterPointBuffer`, `ParityValidationSystem`
3. Remove `LegacyImporter` (if no longer needed for old file import)
4. Update `context.md` files
5. Archive this plan

---

# Key Files

## New Files (Phase 4)
- `Assets/Runtime/Legacy/KexdAdapter.cs` - Coaster → ECS entities
- `Assets/Tests/KexdRoundTripTests.cs` - Format validation
- `Assets/Tests/KexdParityTests.cs` - Parity validation (Phase 4D)
- `tools/validate_kexd_parity.py` - Python parity validation tool

## Modified Files
- `Assets/Runtime/Legacy/Persistence/Serialization/SerializationSystem.cs` - KEXD read/write
- `tools/analyze_kexd.py` - Extended validation

## Reference (Existing)
- `Assets/Runtime/Persistence/CoasterSerializer.cs` - CORE chunk
- `Assets/Runtime/Persistence/Extensions/ExtensionSerializer.cs` - UIMD chunk
- `Assets/Runtime/Legacy/LegacyImporter.cs` - SerializedGraph → Coaster (keep for old files)

---

# Testing Strategy

## Per-Phase Tests

| Phase | Test Type | Purpose |
|-------|-----------|---------|
| 4A | Unit | SerializeToKEXD produces valid chunks |
| 4A | Python | External format validation |
| 4B | Unit | DeserializeFromKEXD creates correct entities |
| 4B | Integration | Both formats loadable |
| 4C | Integration | Undo/redo with KEXD |
| 5 | Regression | All existing tests pass after pruning |

## Test Corpus
- `veloci.kex`, `shuttle.kex`, `all_types.kex` (existing)
- New KEXD files generated from each

---

# Next Steps

1. ~~Implement Phase 4A: SerializeToKEXD + validation~~ ✅
2. ~~Run tests, validate with Python tool~~ ✅
3. ~~Implement Phase 4B: Format detection + adapter~~ ✅
4. ~~Implement Phase 4C: Switch default format~~ ✅
5. ~~Phase 4D: KEXD Parity Validation~~ ✅
   - ~~Extend Python parity tool to compare legacy vs KEXD~~
   - ~~Fix rotation port consolidation (Roll/Pitch/Yaw → Rotation)~~
   - ~~Fix UI position drift for synthetic nodes~~
   - ~~Add comprehensive parity tests~~
6. **Phase 5: Prune redundant components** ← NEXT
