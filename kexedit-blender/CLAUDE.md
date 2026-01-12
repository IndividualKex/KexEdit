# kexedit-blender

Blender addon (kexedit) for roller coaster design using kexengine (Rust FFI backend).

## Architecture

Hexagonal/onion architecture with pure cores and domain-aware layers:

```
┌─────────────────────────────────────────────────────────────┐
│  UI LAYER — Blender operators, panels, node editor         │
├─────────────────────────────────────────────────────────────┤
│  INTEGRATION — Blender-specific adapters, F-Curve readers  │
├─────────────────────────────────────────────────────────────┤
│  BRIDGE — Python bindings, document builder                │
├─────────────────────────────────────────────────────────────┤
│  CORE — kexengine FFI (Rust library)                       │
└─────────────────────────────────────────────────────────────┘
```

**Dependency rule**: Inward only. Each layer depends only on layers below it.

| Layer | Role | Blender-aware |
|-------|------|---------------|
| **Core** | kexengine Rust FFI via ctypes | No |
| **Bridge** | Document construction, keyframe types | No |
| **Integration** | F-Curve → Keyframe, SplinePoint → Curve | Yes |
| **UI** | Operators, panels, custom nodes | Yes |

## Project Structure

```
kexedit-blender/
├── kexedit/                # Python package (the addon)
│   ├── __init__.py         # Blender addon entry point
│   ├── core/               # Pure Python, no Blender imports
│   │   ├── ffi.py          # ctypes bindings to kexengine
│   │   ├── types.py        # Data structures (Keyframe, Point, etc.)
│   │   └── coords.py       # Coordinate system conversion
│   ├── integration/        # Blender-aware adapters
│   │   ├── curve.py        # SplinePoint → Blender Curve
│   │   └── properties.py   # PropertyGroup definitions
│   ├── ui/                 # Operators and panels
│   │   ├── operators.py    # Blender operators
│   │   ├── panels.py       # UI panels
│   │   └── nodes.py        # Custom node tree (future)
│   └── lib/                # Platform-specific .dll/.so/.dylib
├── tests/                  # Test suite
└── scripts/                # Build and install scripts
```

## Commands

```bash
# Run tests (standalone, no Blender)
python -m pytest tests/

# Build kexengine FFI library
cd ../kexengine && cargo build --release

# Copy library to addon
cp ../kexengine/target/release/kexengine.dll kexedit/lib/

# Install addon (symlink for development)
# Windows: mklink /D "C:\BlenderExtensions\dev\kexedit" "path\to\kexedit"
```

## Code Rules

- **Inward dependencies only** — Core must not import from integration/ui
- **Pure cores** — `core/` has no Blender imports, fully testable standalone
- **Type hints** — Required on all function signatures
- **Single responsibility** — Small files, separated concerns
- **Fail fast** — Validate early, clear error messages

## FFI Contract

Single entry point from kexengine:

```c
int kex_build(
    const KexDocument* doc,
    float resolution,
    int default_style_index,
    KexOutput* output
) -> 0=success, -1=null, -3=buffer overflow, -4=cycle
```

Input: Graph topology + properties + keyframes
Output: SplinePoints (position, orientation, arc-length)

## Key Types

See @kexengine/core/types.py for Python equivalents of:
- `Keyframe` — time, value, interpolation, tangents
- `Point` — position, orientation, velocity, forces
- `SplinePoint` — arc, position, direction, normal, lateral
- `Section` — point range, arc range, links

## Current Phase

See @.claude/rules/progress.md for implementation status.
