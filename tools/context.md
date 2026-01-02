# Tools - Development Utilities

## Purpose

Python scripts for validating binary formats, data assumptions, and debugging outside the Unity editor.

## Layout

```
tools/
├── context.md
├── analyze_kex.py           # Legacy .kex binary format analyzer
├── analyze_kexd.py          # KEXD chunk format analyzer
├── obj_debug.py             # OBJ mesh export debugging (uses numpy)
├── validate_coaster_state.py
├── validate_facing.py
├── validate_keyframe_ids.py # Keyframe DATA chunk validation
├── validate_kexd_parity.py
├── validate_property_overrides.py
└── validate_veloci_structure.py
```

## Dependencies

- Python 3.10+
- Most scripts: standard library only (struct, json, pathlib)
- obj_debug.py: numpy (via `uv run`)
