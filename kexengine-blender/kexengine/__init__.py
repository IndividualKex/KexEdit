"""kexengine - Blender addon for roller coaster design.

This is the main addon entry point for Blender. When not running in Blender,
it provides access to the core library.
"""

from __future__ import annotations

# Version info
__version__ = "0.1.0"

# Check if bpy is available (for conditional imports)
try:
    import bpy
    _has_bpy = True
except ImportError:
    _has_bpy = False

# Hot reload support
if "core" in locals():
    import importlib
    core = importlib.reload(core)
    if _has_bpy:
        integration = importlib.reload(integration)
        ui = importlib.reload(ui)
else:
    from . import core
    if _has_bpy:
        from . import integration
        from . import ui

# Re-export core types for convenience
from .core import (
    Float3,
    Keyframe,
    InterpolationType,
    Point,
    SplinePoint,
    Section,
    SectionLink,
    KexEngine,
    KexError,
)

__all__ = [
    "__version__",
    "Float3",
    "Keyframe",
    "InterpolationType",
    "Point",
    "SplinePoint",
    "Section",
    "SectionLink",
    "KexEngine",
    "KexError",
]

# Blender addon info (legacy system, also used for in-Blender display)
bl_info = {
    "name": "kexengine",
    "author": "KexEdit",
    "version": (0, 1, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > kexengine",
    "description": "Roller coaster track design using Force Vector Design",
    "category": "Object",
    "doc_url": "",
    "tracker_url": "",
}


def register():
    """Register Blender addon classes."""
    if _has_bpy:
        ui.register()


def unregister():
    """Unregister Blender addon classes."""
    if _has_bpy:
        ui.unregister()
