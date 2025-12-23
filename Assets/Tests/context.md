# Tests Context

Unit and integration tests for KexEdit functionality.

## Purpose

- Unit tests for KexGraph library (nodes, ports, edges)
- Unit tests for Core primitives and node types
- Unit tests for LegacyImporter (SerializedGraph → Coaster)
- Validation tests for Coaster aggregate integrity (Phase B-1)
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
├── CoasterSerializerTests.cs   # Chunk-based serialization tests
├── CoasterValidationTests.cs   # Phase B-1: round-trip validation, Python export
├── CoasterGoldTests.cs         # Integration tests: .kex → evaluate
├── *NodeTests.cs               # Golden tests for node types
├── *TestBuilder.cs             # Test data builders from gold fixtures
├── SimPointComparer.cs         # KexEdit.Core.Point comparison
├── PointComparer.cs            # ECS Point comparison
├── PerformanceTests/           # Burst vs Rust benchmarks
├── GoldData/                   # Gold data loading
├── Storage/                    # Storage layer tests
├── Coaster/                    # Coaster aggregate tests
├── TrackData/                  # Gold test fixtures (JSON)
└── Assets/                     # Test .kex files (veloci, shuttle, all_types)
```

## Running Tests

**Headless (CLI)**: `./run-tests.sh` (Burst backend, default)
**Rust backend**: `./run-tests.sh --rust-backend`
**Unity Editor**: Window → General → Test Runner

## Heart/Spine Naming Convention

| Term | Definition |
|------|------------|
| `HeartPosition` | Rider heart position (primary coordinate) |
| `SpinePosition` | Track centerline = `HeartPosition + Normal * HeartOffset` |
| `HeartArc` | Cumulative distance along heart path |
| `SpineArc` | Cumulative distance along spine path |
| `HeartAdvance` | Per-step distance along heart path |
| `FrictionOrigin` | HeartArc position where friction was last reset |

Gold test fixtures use modern camelCase naming (e.g., `heartPosition`, `heartArc`, `spineArc`).

## Dependencies

- KexGraph
- KexEdit.Core, KexEdit.Nodes.*, KexEdit.Coaster, KexEdit.LegacyImport
- KexEdit (ECS), KexEdit.Native.RustCore
- Unity.Entities.Tests, Unity.PerformanceTesting, NUnit
