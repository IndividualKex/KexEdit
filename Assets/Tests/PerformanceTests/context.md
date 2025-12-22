# Performance Tests

Lightweight benchmarks comparing Burst-compiled C# vs Rust native implementations.

## Purpose

- Measure relative performance of force section builds
- Compare Burst (Unity LLVM) vs Rust (native FFI) implementations
- Verify performance characteristics remain acceptable

## Layout

```
PerformanceTests/
├── context.md

└── ForceSectionPerformanceTests.cs     # Force section build benchmarks (Rust vs Burst)
```

## Running

**Unity Editor**: Window → General → Test Runner → Performance category

**Headless**: `./run-tests.sh` (includes performance tests)

## Compilation Flags

The `ForceSectionPerformanceTests` use conditional compilation based on the `USE_RUST_BACKEND` flag:

- **Burst tests**: Run when `USE_RUST_BACKEND` is **not** defined (default)
- **Rust tests**: Run when `USE_RUST_BACKEND` **is** defined

To toggle: Project Settings → Player → Scripting Define Symbols (`ProjectSettings/ProjectSettings.asset`)

The test suite includes a `VerifyCompilationFlags` test that confirms which implementation is active.

## Results

Burst consistently outperforms Rust by 20-100% due to zero FFI overhead. Both implementations use LLVM optimizations.

## Dependencies

- KexEdit.Core (Burst implementation)
- KexEdit.Native.RustCore (Rust implementation)
- Unity.Burst, Unity.PerformanceTesting
