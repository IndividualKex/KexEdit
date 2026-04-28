# KexEdit

Roller coaster editor using Force Vector Design (FVD).

## Structure

- `packages/core/` — Rust crate. Physics simulation, node graph, binary format (.kex). Only runtime dep: `approx`
- `plugins/blender/` — Blender 4.2+ addon. `kexedit/` is the addon package (name required by Blender). Flat: ffi.py, types.py, coords.py (no bpy), operators.py, panels.py, properties.py, curve.py, fcurve.py (bpy). Loads core via ctypes
- `app/` — Shallot-based web editor. Depends on Shallot (npm) and core (WASM)

## Architecture

```
app (shallot + UI) → core (rust/wasm)
blender (python)   → core (rust/cdylib via FFI)
```

Core is the shared truth. Frontends never leak into core.

## Core Modules

sim → graph → nodes → track → persistence → ffi

sim is pure math (zero deps). Each layer only depends on layers to its left. FFI is feature-gated.

## Build

```bash
cd packages/core && cargo build --release --features ffi
plugins/blender/scripts/build_lib.sh  # copies lib to addon
```

WASM: `cargo build --target wasm32-unknown-unknown --features wasm`

## Verify

```bash
cd packages/core && cargo test
cd packages/core && cargo clippy
cd plugins/blender && uvx pytest tests/ -v
```
