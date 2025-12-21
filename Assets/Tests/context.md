# Tests Context

Unit and integration tests for KexEdit functionality.

## Purpose

- Unit tests for KexGraph library (nodes, ports, edges)
- Unit tests for Core primitives and node types
- Unit tests for LegacyImporter (SerializedGraph → Coaster)
- Golden fixture tests validating physics against exported ground truth
- Performance benchmarks comparing Burst vs Rust implementations

## Layout

```
Tests/
├── context.md
├── Tests.asmdef
├── Graph*Tests.cs              # Unit tests for KexGraph library
├── Core*Tests.cs               # Unit tests for Core layer
├── LegacyImporterTests.cs      # Unit tests for legacy import
├── CoasterGoldTests.cs         # Integration tests: .kex → evaluate
├── *NodeTests.cs               # Golden tests for node types
├── Build*SystemTests.cs        # Golden tests for ECS layer
├── *TestBuilder.cs             # Build test data from gold fixtures
├── *EntityBuilder.cs           # Build ECS entities from gold fixtures
├── SimPointComparer.cs         # KexEdit.Core.Point comparison
├── PointComparer.cs            # ECS Point comparison
├── PerformanceTests/           # Burst vs Rust benchmarks
├── GoldData/                   # Gold data loading
├── Storage/                    # Storage layer tests
├── TrackData/                  # Gold test fixtures (JSON)
└── Assets/                     # Test asset files (.kex)
```

## Running Tests

**Headless (CLI)**: `./run-tests.sh` (Burst backend, default)
**Rust backend**: `./run-tests.sh --rust-backend`
**Unity Editor**: Window → General → Test Runner

## Heart/Spine Naming Convention

**CRITICAL: Legacy code had INVERTED naming. Gold data uses legacy names. Modern code uses correct names.**

### Modern (correct) semantics - used everywhere in modern code:

| Term | Definition |
|------|------------|
| `HeartPosition` | Rider heart position (fundamental, primary coordinate) |
| `SpinePosition` | Track centerline = `HeartPosition + Normal * HeartOffset` |
| `HeartArc` | Cumulative distance along heart path |
| `SpineArc` | Cumulative distance along spine path |
| `HeartAdvance` | Per-step distance along heart path |
| `FrictionOrigin` | HeartArc position where friction was last reset |

### Gold JSON uses legacy names - MUST be remapped on load:

| Gold JSON Field (legacy) | Modern Field | Why confusing |
|--------------------------|--------------|---------------|
| `position` | `HeartPosition` | Correct - stores heart position |
| `totalLength` | `HeartArc` | INVERTED - legacy `TotalLength` was calculated from GetHeartPosition (which returned spine!) |
| `totalHeartLength` | `SpineArc` | INVERTED - legacy `TotalHeartLength` was calculated from Position (which was heart!) |
| `heart` | `HeartOffset` | Correct |
| `frictionCompensation` | `FrictionOrigin` | Correct |

### Why legacy naming was inverted:

Legacy function `GetHeartPosition(offset)` returned `Position + Normal * offset` = SPINE position.
Legacy field `Position` stored heart coordinates.
So legacy `TotalLength` (accumulated from GetHeartPosition distances) was actually SPINE arc.
And legacy `TotalHeartLength` (accumulated from Position distances) was actually HEART arc.

### Where conversion happens:

- `GoldDataLoader.cs` - Loads JSON with legacy field names
- `SimPointComparer.cs` - Maps legacy JSON fields to modern Point fields for comparison
- `LegacyImporter.cs` - Handles naming at .kex import time

### Verification:

Modern code must use correct semantics consistently. The ONLY place legacy naming should appear is when loading gold data or legacy files.

## Dependencies

- KexGraph
- KexEdit.Core, KexEdit.Nodes.*, KexEdit.Coaster, KexEdit.LegacyImport
- KexEdit (ECS), KexEdit.Native.RustCore
- Unity.Entities.Tests, Unity.PerformanceTesting, NUnit
