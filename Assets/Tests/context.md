# Tests Context

Unit and integration tests for KexEdit functionality

## Purpose

- Contains test suites for track computation, UI, and systems
- Ensures correctness of physics calculations and track generation
- Validates UI interactions and file operations

## Layout

```
Tests/
├── context.md  # This file, folder context (Tier 2)
├── Editor/  # Editor-only tests
│   └── [Various]Tests.cs  # UI and editor tests
├── Runtime/  # Runtime tests
│   └── [Various]Tests.cs  # ECS and computation tests
├── PlayMode/  # Play mode tests
│   └── [Various]Tests.cs  # Integration tests
└── Tests.asmdef  # Test assembly definition
```

## Scope

- In-scope: All unit tests, integration tests, test utilities
- Out-of-scope: Production code, build scripts, documentation

## Entrypoints

- Unity Test Runner (Window → General → Test Runner)
- Tests run in Edit Mode or Play Mode depending on type
- CI/CD pipelines can invoke tests via command line

## Dependencies

- Unity Test Framework - Test infrastructure
- NUnit - Assertion framework
- Runtime and UI assemblies - Code under test