# Architecture Rules

These rules enforce the hexagonal/onion architecture.

## Layer Boundaries

### core/ — Pure Python, No External Dependencies

```
ALLOWED imports in core/:
- Standard library (ctypes, dataclasses, typing, enum, etc.)
- Other core/ modules

FORBIDDEN imports in core/:
- bpy (Blender)
- Any Blender-specific types
- integration/ or ui/ modules
```

The core must be:
- Testable with standard pytest (no Blender runtime)
- Portable to other environments (CLI tool, other 3D apps)
- Unaware of how it's used

### integration/ — Blender-Aware Adapters

```
ALLOWED imports in integration/:
- Standard library
- bpy, mathutils, bmesh
- core/ modules

FORBIDDEN imports in integration/:
- ui/ modules
```

Integration adapts between:
- Blender types ↔ Core types
- F-Curves ↔ Keyframes
- SplinePoints ↔ Blender Curves

### ui/ — Blender Operators and Panels

```
ALLOWED imports in ui/:
- Standard library
- bpy
- core/ modules
- integration/ modules
```

UI orchestrates the pipeline but contains no business logic.

## Dependency Direction

```
ui/ ──────► integration/ ──────► core/
                                   │
                                   ▼
                              kexengine FFI
```

Arrows point toward dependencies. Never create reverse dependencies.

## Data Flow

```
User Input (Blender UI)
         │
         ▼
    ui/operators.py
         │
         ▼
integration/fcurve.py ──► Read F-Curves, convert to Keyframes
integration/properties.py ──► Read PropertyGroups, extract values
         │
         ▼
    core/document.py ──► Build KexDocument
         │
         ▼
    core/ffi.py ──► Call kex_build()
         │
         ▼
    core/types.py ◄── Parse output (SplinePoints, Sections)
         │
         ▼
integration/curve.py ──► Create Blender Curve objects
         │
         ▼
    Blender Scene
```

## Testing Strategy

| Layer | Test Method |
|-------|-------------|
| core/ | pytest, standalone Python |
| integration/ | pytest with Blender Python, or mock bpy |
| ui/ | Manual testing in Blender |

Core tests must run without Blender installed.

## Error Handling

- **core/**: Raise exceptions with clear messages
- **integration/**: Catch core exceptions, translate to Blender-friendly errors
- **ui/**: Report errors via `self.report({'ERROR'}, message)`

Never let FFI errors propagate as crashes. Catch and report.
