"""kexedit - Blender addon for roller coaster design.

This is the main addon entry point for Blender. When not running in Blender,
it provides access to the core library via the kexengine backend.
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
if "ffi" in locals():
    import importlib
    ffi = importlib.reload(ffi)
    types = importlib.reload(types)
    coords = importlib.reload(coords)
    if _has_bpy:
        properties = importlib.reload(properties)
        curve = importlib.reload(curve)
        fcurve = importlib.reload(fcurve)
        operators = importlib.reload(operators)
        panels = importlib.reload(panels)
else:
    from . import ffi, types, coords
    if _has_bpy:
        from . import properties, curve, fcurve, operators, panels

# Re-export core types for convenience
from .types import (
    Float3,
    Keyframe,
    InterpolationType,
    Point,
    SplinePoint,
    Section,
    SectionLink,
)
from .ffi import KexEngine, KexError

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
    "name": "kexedit",
    "author": "KexEdit",
    "version": (0, 1, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > kexedit",
    "description": "Roller coaster track design using Force Vector Design",
    "category": "Object",
    "doc_url": "",
    "tracker_url": "",
}


_depsgraph_handler = None


def _on_depsgraph_update(scene, depsgraph):
    """Handle depsgraph updates to detect F-Curve changes."""
    from .operators import regenerate_track

    for update in depsgraph.updates:
        # Check if an Action was updated (F-Curve editing)
        if update.id and isinstance(update.id, bpy.types.Action):
            updated_action_name = update.id.name
            # Find objects using this action and regenerate their tracks
            for obj in scene.objects:
                if not obj.get("kex_is_track"):
                    continue
                if obj.animation_data and obj.animation_data.action:
                    if obj.animation_data.action.name == updated_action_name:
                        regenerate_track(obj)
                        break


def register():
    """Register Blender addon classes."""
    global _depsgraph_handler
    if _has_bpy:
        properties.register()
        operators.register()
        panels.register()

        # Register depsgraph handler for F-Curve updates
        _depsgraph_handler = _on_depsgraph_update
        if _depsgraph_handler not in bpy.app.handlers.depsgraph_update_post:
            bpy.app.handlers.depsgraph_update_post.append(_depsgraph_handler)


def unregister():
    """Unregister Blender addon classes."""
    global _depsgraph_handler
    if _has_bpy:
        # Remove depsgraph handler
        if _depsgraph_handler and _depsgraph_handler in bpy.app.handlers.depsgraph_update_post:
            bpy.app.handlers.depsgraph_update_post.remove(_depsgraph_handler)
        _depsgraph_handler = None

        panels.unregister()
        operators.unregister()
        properties.unregister()
