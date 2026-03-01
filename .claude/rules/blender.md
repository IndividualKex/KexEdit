# Blender Plugin Rules

Applies to `plugins/blender/**`

## Flat package

No sub-packages. Pure Python files (types.py, coords.py) vs bpy-dependent files (operators.py, panels.py, etc).

## FFI boundary

Only `ffi.py` touches ctypes. Other files never call ctypes directly.

## Coordinate system

Blender Z-up ↔ core Y-up. All coordinate transforms live in `coords.py` only.

## Testing

`uvx pytest tests/ -v`. Tests run without Blender — test pure Python logic (types, coords, serialization).
