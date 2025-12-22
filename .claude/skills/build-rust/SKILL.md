# Build Rust

Build and deploy the Rust FFI DLL for Unity integration.

## When to Use

- After modifying Rust code in `rust-backend/`
- Before running `--rust-backend` tests
- To update the FFI library for Unity

## Commands

```bash
# Build FFI DLL (copies to Unity Assets)
./build-rust.sh

# Or build manually
cd rust-backend && cargo build --release
```

## Output

The built library (`kexengine.dll` on Windows) is copied to `Assets/Runtime/Plugins/`.

## Troubleshooting

If Unity fails to load the DLL:
1. Ensure `./build-rust.sh` completed successfully
2. Check for Rust compilation errors
3. Restart Unity to reload native plugins
