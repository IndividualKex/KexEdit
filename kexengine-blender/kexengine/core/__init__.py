"""Core module - pure Python, no Blender dependencies."""

# Hot reload support
if "types" in locals():
    import importlib
    types = importlib.reload(types)
    ffi = importlib.reload(ffi)
else:
    from . import types
    from . import ffi

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
]
