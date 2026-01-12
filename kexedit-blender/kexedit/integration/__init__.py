"""Integration layer - Blender-aware adapters."""

# Hot reload support
if "curve" in locals():
    import importlib
    curve = importlib.reload(curve)
    properties = importlib.reload(properties)
else:
    from . import curve
    from . import properties

from .curve import (
    create_track_curve,
    create_track_bezier,
    create_track_from_sections,
    update_track_curve,
    update_track_from_sections,
)

__all__ = [
    "create_track_curve",
    "create_track_bezier",
    "create_track_from_sections",
    "update_track_curve",
    "update_track_from_sections",
    "properties",
]
