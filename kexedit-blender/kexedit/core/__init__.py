"""Core module - pure Python, no Blender dependencies."""

# Hot reload support
if "types" in locals():
    import importlib
    types = importlib.reload(types)
    ffi = importlib.reload(ffi)
    coords = importlib.reload(coords)
else:
    from . import types
    from . import ffi
    from . import coords

from .types import (
    Float3,
    Keyframe,
    InterpolationType,
    Point,
    SplinePoint,
    Section,
    SectionLink,
)
from .ffi import KexEngine, KexError, is_library_available
from .coords import (
    kex_to_blender_position,
    blender_to_kex_position,
    kex_to_blender_direction,
    blender_to_kex_direction,
    kex_to_blender_angles,
    blender_to_kex_angles,
)

__all__ = [
    "Float3",
    "Keyframe",
    "InterpolationType",
    "Point",
    "SplinePoint",
    "Section",
    "SectionLink",
    "KexEngine",
    "KexError",
    "is_library_available",
    "kex_to_blender_position",
    "blender_to_kex_position",
    "kex_to_blender_direction",
    "blender_to_kex_direction",
    "kex_to_blender_angles",
    "blender_to_kex_angles",
]
