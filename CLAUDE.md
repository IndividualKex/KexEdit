# KexEdit

Roller coaster editor using Force Vector Design (FVD).

## Structure

- `packages/core/` — Rust crate. Physics simulation, node graph, binary format (.kex). Only runtime dep: `approx`
- `plugins/blender/` — Blender 4.2+ addon. `kexedit/` is the addon package (name required by Blender). Flat: ffi.py, types.py, coords.py (no bpy), operators.py, panels.py, properties.py, curve.py, fcurve.py (bpy). Loads core via handle-based FFI (`kex_load` → `kex_build` → `kex_output_read_*`). Python-side `.kex` serializer in `ffi.py` mirrors the format in `packages/core/src/persistence/`
- `app/` — placeholder for the Shallot-based web editor (not yet implemented)
- `kex2d/` — 2D coaster solver prototype (Shallot + Svelte + canvas2D). Augmented-Lagrangian trajectory optimization exploration, parallel to `app/`. See `roadmap.md` "kex2d — 2D coaster solver prototype" + `scratch.md` "kex2d solver — reference + decisions"

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
plugins/blender/scripts/build_lib.sh           # host platform
plugins/blender/scripts/build_lib.sh windows   # cross-compile DLL (mingw, runs from Linux/WSL)
plugins/blender/scripts/build_lib.sh all       # host + Windows
```

`build_lib.sh` builds the Rust crate and copies the artifact + `.kex` fixtures into `plugins/blender/kexedit/{lib,fixtures}/` (both gitignored — single source of truth lives in `packages/core/`).

Set `KEXEDIT_DEV_INSTALL=path1[:path2]` to also rsync the addon dir to a Blender extensions location after building. Useful for syncing into a Windows-side Blender from WSL where cross-filesystem symlinks don't behave.

## Verify

```bash
cd packages/core && cargo test
cd packages/core && cargo clippy
cd plugins/blender && uvx pytest tests/ -v
```
