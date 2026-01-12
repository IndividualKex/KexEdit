---
paths:
  - "**/*.py"
---

# Python Code Style

## Type Hints

Required on all function signatures:

```python
# Good
def build_document(nodes: list[NodeData], edges: list[EdgeData]) -> KexDocument:
    ...

# Bad
def build_document(nodes, edges):
    ...
```

Use `from __future__ import annotations` for forward references.

## Dataclasses

Prefer dataclasses for data structures:

```python
from dataclasses import dataclass

@dataclass
class Keyframe:
    time: float
    value: float
    in_tangent: float = 0.0
    out_tangent: float = 0.0
```

## Imports

Group and order:
1. `__future__` imports
2. Standard library
3. Third-party (bpy, etc.)
4. Local imports

```python
from __future__ import annotations

import ctypes
from dataclasses import dataclass
from typing import Optional

import bpy

from .core.types import Keyframe
```

## Naming

- Classes: `PascalCase`
- Functions/variables: `snake_case`
- Constants: `UPPER_SNAKE_CASE`
- Private: `_leading_underscore`

## ctypes Conventions

For FFI struct definitions:

```python
class KexPoint(ctypes.Structure):
    _fields_ = [
        ("heart_position", Float3),
        ("direction", Float3),
        # ... fields match Rust struct exactly
    ]
```

Field names should match the Rust/C names for clarity.

## Blender Conventions

For operators:

```python
class KEXEDIT_OT_generate_track(bpy.types.Operator):
    bl_idname = "kexengine.generate_track"
    bl_label = "Generate Track"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return context.object is not None

    def execute(self, context):
        # ... implementation
        return {'FINISHED'}
```

- Operator class names: `ADDON_OT_action_name`
- Panel class names: `ADDON_PT_panel_name`
- bl_idname: `addon.action_name`

## Documentation

Docstrings for public APIs only. Code should be self-documenting.

```python
def evaluate_keyframes(keyframes: list[Keyframe], time: float) -> float:
    """Evaluate keyframe curve at given time using appropriate interpolation."""
    ...
```

No docstrings needed for obvious functions like getters/setters.
