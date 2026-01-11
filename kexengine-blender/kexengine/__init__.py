"""kexengine - Blender addon for roller coaster design.

This is the main addon entry point for Blender. When not running in Blender,
it provides access to the core library.
"""

from __future__ import annotations

# Version info
__version__ = "0.1.0"

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

# Blender addon info (only used when loaded as addon)
bl_info = {
    "name": "kexengine",
    "author": "KexEdit",
    "version": (0, 1, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > kexengine",
    "description": "Roller coaster track design using Force Vector Design",
    "category": "Object",
}


def register():
    """Register Blender addon classes."""
    # Import Blender modules only when registering
    try:
        from . import ui
        ui.register()
    except ImportError:
        # Not running in Blender
        pass


def unregister():
    """Unregister Blender addon classes."""
    try:
        from . import ui
        ui.unregister()
    except ImportError:
        pass
