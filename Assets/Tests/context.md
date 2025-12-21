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

## Legacy vs Modern Naming Convention

The legacy implementation (origin/dev) uses inverted naming for heart/spine concepts.

**Modern (correct) semantics:**
- **Heart** = rider heart position (fundamental, primary coordinate)
- **Spine** = track centerline (derived: `HeartPosition + Normal * HeartOffset`)

**Gold JSON field mapping:**

| Gold JSON Field      | Modern Field      | Description                           |
|----------------------|-------------------|---------------------------------------|
| `position`           | `HeartPosition`   | Rider heart position (fundamental)    |
| `totalLength`        | `HeartArc`        | Arc length along heart path           |
| `totalHeartLength`   | `SpineArc`        | Arc length along spine/track path     |
| `heart`              | `HeartOffset`     | Offset from heart to spine            |
| `frictionCompensation`| `FrictionOrigin` | Arc position where friction resets    |

**Why "inverted":** The legacy code's function `GetHeartPosition(offset)` confusingly returns `Position + Normal * offset`, which is actually the SPINE position. The field named `position` stores heart coordinates; the function named "GetHeartPosition" computes spine coordinates.

**Gold data conversion:**
- Gold JSON `position` field IS the heart position (no conversion needed for position)
- `SimPointComparer` maps field names when comparing (e.g., `totalLength` → `HeartArc`)
- `LegacyImporter` handles naming at .kex import time

## Dependencies

- KexGraph
- KexEdit.Core, KexEdit.Nodes.*, KexEdit.Coaster, KexEdit.LegacyImport
- KexEdit (ECS), KexEdit.Native.RustCore
- Unity.Entities.Tests, Unity.PerformanceTesting, NUnit
