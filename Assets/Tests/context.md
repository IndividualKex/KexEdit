# Tests Context

Unit and integration tests for KexEdit functionality.

## Purpose

- Unit tests for KexGraph library (nodes, ports, edges)
- Golden fixture tests validating physics against exported ground truth
- Unit tests for Core primitives and node types
- Performance benchmarks comparing Burst vs Rust implementations

## Layout

```
Tests/
├── context.md
├── Tests.asmdef
├── Graph*Tests.cs              # Unit tests for KexGraph library
├── Core*Tests.cs               # Unit tests for Core layer
├── *NodeTests.cs               # Golden tests for node types
├── Build*SystemTests.cs        # Golden tests for ECS layer
├── *TestBuilder.cs             # Build test data from gold fixtures
├── *EntityBuilder.cs           # Build ECS entities from gold fixtures
├── SimPointComparer.cs         # KexEdit.Core.Point comparison
├── PointComparer.cs            # ECS Point comparison
├── PerformanceTests/           # Burst vs Rust benchmarks
├── GoldData/                   # Gold data loading
├── TrackData/                  # Gold test fixtures (JSON)
└── Assets/                     # Test asset files (.kex)
```

## Running Tests

**Headless (CLI)**: `./run-tests.sh` (Burst backend, default)
**Rust backend**: `./run-tests.sh --rust-backend`
**Unity Editor**: Window → General → Test Runner

## Dependencies

- KexGraph
- KexEdit.Core, KexEdit.Nodes.*, KexEdit (ECS)
- KexEdit.Native.RustCore
- Unity.Entities.Tests, Unity.PerformanceTesting, NUnit
