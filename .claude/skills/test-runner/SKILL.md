# Test Runner

Run tests for both C# (Unity) and Rust backends.

## When to Use

- After modifying any Sim, Graph, or Spline code
- Before committing changes
- To verify Rust/C# parity

## Commands

```bash
# C# tests (Burst backend)
./run-tests.sh [TestName]

# C# tests (Rust FFI backend)
./run-tests.sh --rust-backend [TestName]

# Rust tests only
cargo test -p kexedit-sim
cargo test -p kexedit-track

# All Rust tests
cd rust-backend && cargo test
```

## Test Results

After running tests, check these files instead of re-running:
- `test-results.xml` — Summary
- `test-log.txt` — Details

## Parity Testing

The `--rust-backend` flag runs the same tests using FFI calls to the Rust backend, verifying output matches the C# implementation.
