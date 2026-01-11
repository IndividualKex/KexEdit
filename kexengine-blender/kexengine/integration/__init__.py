"""Integration layer - Blender-aware adapters."""

# Hot reload support
if "curve" in locals():
    import importlib
    curve = importlib.reload(curve)
else:
    from . import curve

from .curve import (
    create_track_curve,
    create_track_bezier,
    update_track_curve,
)

__all__ = [
    "create_track_curve",
    "create_track_bezier",
    "update_track_curve",
]
