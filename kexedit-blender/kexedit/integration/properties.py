"""PropertyGroup definitions for kexengine tracks.

Stores track parameters on curve objects for live editing.
All positions and angles are in Blender coordinates (Z-up, right-handed).
"""

from __future__ import annotations

import bpy
from bpy.props import (
    FloatProperty,
    FloatVectorProperty,
    PointerProperty,
)

# Global flag to prevent update loops during regeneration
_updating = False


def _on_track_property_update(self, context):
    """Called when any track property changes. Triggers regeneration."""
    global _updating
    if _updating:
        return

    obj = context.object
    if obj is None or not obj.get("kex_is_track"):
        return

    _updating = True
    try:
        from ..ui.operators import regenerate_track
        regenerate_track(obj)
    finally:
        _updating = False


class KexAnchorSettings(bpy.types.PropertyGroup):
    """Anchor node parameters stored on the track object."""

    position: FloatVectorProperty(
        name="Position",
        description="Starting position of the track (Blender coordinates)",
        subtype='XYZ',
        default=(0.0, 0.0, 3.0),  # 3 meters up in Blender Z-up coords
        update=_on_track_property_update,
    )

    pitch: FloatProperty(
        name="Pitch",
        description="Initial pitch angle in degrees",
        default=0.0,
        min=-90.0,
        max=90.0,
        update=_on_track_property_update,
    )

    yaw: FloatProperty(
        name="Yaw",
        description="Initial yaw angle in degrees",
        default=0.0,
        min=-180.0,
        max=180.0,
        update=_on_track_property_update,
    )

    roll: FloatProperty(
        name="Roll",
        description="Initial roll angle in degrees",
        default=0.0,
        min=-180.0,
        max=180.0,
        update=_on_track_property_update,
    )

    velocity: FloatProperty(
        name="Velocity",
        description="Initial velocity in m/s",
        default=10.0,
        min=0.1,
        max=100.0,
        update=_on_track_property_update,
    )

    heart_offset: FloatProperty(
        name="Heart Offset",
        description="Distance from spine to heart line in meters",
        default=1.1,
        min=0.0,
        max=5.0,
        update=_on_track_property_update,
    )

    friction: FloatProperty(
        name="Friction",
        description="Track friction coefficient",
        default=0.021,
        min=0.0,
        max=1.0,
        precision=4,
        update=_on_track_property_update,
    )

    resistance: FloatProperty(
        name="Resistance",
        description="Air resistance coefficient",
        default=2e-5,
        min=0.0,
        max=0.01,
        precision=6,
        update=_on_track_property_update,
    )


class KexForceSettings(bpy.types.PropertyGroup):
    """Force node parameters stored on the track object."""

    duration: FloatProperty(
        name="Duration",
        description="Duration of the force segment in seconds",
        default=5.0,
        min=0.1,
        max=60.0,
        update=_on_track_property_update,
    )

    # Animated FVD properties (base values when not animated via F-Curves)
    roll_speed: FloatProperty(
        name="Roll Speed",
        description="Roll rate in degrees per second",
        default=0.0,
        soft_min=-180.0,
        soft_max=180.0,
        update=_on_track_property_update,
    )

    normal_force: FloatProperty(
        name="Normal Force",
        description="Normal force in G (1.0 = gravity)",
        default=1.0,
        soft_min=-5.0,
        soft_max=10.0,
        update=_on_track_property_update,
    )

    lateral_force: FloatProperty(
        name="Lateral Force",
        description="Lateral force in G (positive = right)",
        default=0.0,
        soft_min=-5.0,
        soft_max=5.0,
        update=_on_track_property_update,
    )


class KexBuildSettings(bpy.types.PropertyGroup):
    """Build parameters stored on the track object."""

    resolution: FloatProperty(
        name="Resolution",
        description="Spline resolution in meters (lower = more points)",
        default=0.5,
        min=0.1,
        max=5.0,
        update=_on_track_property_update,
    )


class KexTrackSettings(bpy.types.PropertyGroup):
    """Main track settings container, attached to curve objects."""

    anchor: PointerProperty(type=KexAnchorSettings)
    force: PointerProperty(type=KexForceSettings)
    build: PointerProperty(type=KexBuildSettings)


# Class list for registration order (nested groups first)
classes = [
    KexAnchorSettings,
    KexForceSettings,
    KexBuildSettings,
    KexTrackSettings,
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)

    # Attach to Object type
    bpy.types.Object.kex_settings = PointerProperty(type=KexTrackSettings)


def unregister():
    del bpy.types.Object.kex_settings

    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
