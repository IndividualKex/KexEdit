---
paths:
    - "plugins/blender/**/*"
---

# Blender Plugin Rules

## Flat package

No sub-packages. Pure Python files (types.py, coords.py) vs bpy-dependent files (operators.py, panels.py, etc).

## FFI boundary

Only `ffi.py` touches ctypes. Other files never call ctypes directly.

## Coordinate system

Blender Z-up ↔ core Y-up. All coordinate transforms live in `coords.py` only.

## Testing

`uvx pytest tests/ -v`. Tests run without Blender — test pure Python logic (types, coords, serialization).

## Native lib loading on Windows

`kexengine.dll` is loaded by basename, not full path. If a previous Blender session loaded a different copy of `kexengine.dll` (e.g. from an older install in another extensions repo), `LoadLibraryW` returns the cached handle and ignores any path passed to `ctypes.CDLL`. Symbol lookups against the stale module then fail with `function 'kex_X' not found` for any new-API names.

After swapping the DLL, fully restart Blender — close every window, the file mapping persists for the lifetime of the process. F3 → Reload Scripts is not enough.

## Test fixtures

Operators read fixtures from `kexedit/fixtures/`, populated by `scripts/build_lib.sh` from `packages/core/test-data/`. Don't commit anything to `kexedit/fixtures/` — the Rust crate is the single source of truth.
