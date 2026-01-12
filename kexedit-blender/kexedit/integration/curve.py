"""Convert SplinePoints to Blender curves.

This module bridges kexengine output to Blender's curve system.
"""

from __future__ import annotations

import math
from typing import Sequence

import bpy
from mathutils import Vector

from ..core.coords import kex_to_blender_position, kex_to_blender_direction
from ..core.types import Section, SplinePoint, Float3


def kex_to_blender_vector(v: Float3) -> Vector:
    """Convert kexengine Float3 to Blender Vector."""
    pos = kex_to_blender_position(v.x, v.y, v.z)
    return Vector(pos)


def create_track_curve(
    spline_points: Sequence[SplinePoint],
    name: str = "KexTrack",
    collection: bpy.types.Collection | None = None,
) -> bpy.types.Object:
    """Create a Blender curve from SplinePoints.

    Args:
        spline_points: Sequence of SplinePoints from kex_build output.
        name: Name for the curve object.
        collection: Collection to link the object to. Uses scene collection if None.

    Returns:
        The created curve object.
    """
    if not spline_points:
        raise ValueError("No spline points provided")

    # Create curve data
    curve_data = bpy.data.curves.new(name=name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 12
    curve_data.bevel_depth = 0.0  # No bevel by default

    # Create a single spline
    spline = curve_data.splines.new(type='POLY')
    spline.points.add(len(spline_points) - 1)  # Already has 1 point

    # Set point data
    for i, sp in enumerate(spline_points):
        point = spline.points[i]
        # Blender uses XYZW for NURBS/POLY points (W is weight)
        pos = kex_to_blender_vector(sp.position)
        point.co = (pos.x, pos.y, pos.z, 1.0)
        point.tilt = _calculate_tilt(sp)

    # Create object and link to scene
    curve_obj = bpy.data.objects.new(name, curve_data)

    if collection is None:
        collection = bpy.context.scene.collection
    collection.objects.link(curve_obj)

    return curve_obj


def create_track_bezier(
    spline_points: Sequence[SplinePoint],
    name: str = "KexTrack",
    collection: bpy.types.Collection | None = None,
    handle_scale: float = 0.3,
) -> bpy.types.Object:
    """Create a Bezier curve from SplinePoints with proper handles.

    Args:
        spline_points: Sequence of SplinePoints from kex_build output.
        name: Name for the curve object.
        collection: Collection to link the object to.
        handle_scale: Scale factor for handle length based on arc distance.

    Returns:
        The created curve object.
    """
    if not spline_points:
        raise ValueError("No spline points provided")

    # Create curve data
    curve_data = bpy.data.curves.new(name=name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 12

    # Create bezier spline
    spline = curve_data.splines.new(type='BEZIER')
    spline.bezier_points.add(len(spline_points) - 1)

    # Set point data
    for i, sp in enumerate(spline_points):
        bp = spline.bezier_points[i]
        pos = kex_to_blender_vector(sp.position)
        direction = kex_to_blender_vector(sp.direction)

        # Calculate handle offset based on arc distance to neighbors
        if i == 0:
            arc_delta = spline_points[1].arc - sp.arc if len(spline_points) > 1 else 1.0
        elif i == len(spline_points) - 1:
            arc_delta = sp.arc - spline_points[i - 1].arc
        else:
            arc_delta = (spline_points[i + 1].arc - spline_points[i - 1].arc) / 2

        handle_length = arc_delta * handle_scale

        bp.co = pos
        bp.handle_left_type = 'FREE'
        bp.handle_right_type = 'FREE'
        bp.handle_left = pos - direction * handle_length
        bp.handle_right = pos + direction * handle_length
        bp.tilt = _calculate_tilt(sp)

    # Create object and link to scene
    curve_obj = bpy.data.objects.new(name, curve_data)

    if collection is None:
        collection = bpy.context.scene.collection
    collection.objects.link(curve_obj)

    return curve_obj


def _calculate_tilt(sp: SplinePoint) -> float:
    """Calculate tilt angle from SplinePoint orientation.

    Blender's curve tilt rotates the profile around the tangent.
    Tilt=0 means the profile's "up" aligns with Blender's default
    (derived from world Z projected onto the normal plane).

    We compute the angle between that default and our actual normal.
    """
    direction = kex_to_blender_vector(sp.direction).normalized()
    # Negate normal: kexengine normal points toward track, we want away from track
    normal = -kex_to_blender_vector(sp.normal).normalized()

    # Blender's default reference is world Z, or world Y if nearly vertical
    world_up = Vector((0, 0, 1))
    if abs(direction.z) > 0.99:
        world_up = Vector((0, 1, 0))

    # Blender's default "right" and "up" for this curve point
    default_right = direction.cross(world_up).normalized()
    default_up = default_right.cross(direction).normalized()

    # Tilt angle from default_up to our normal, around direction axis
    cos_tilt = default_up.dot(normal)
    sin_tilt = default_right.dot(normal)

    return math.atan2(sin_tilt, cos_tilt)


def create_track_from_sections(
    spline_points: Sequence[SplinePoint],
    sections: Sequence[Section],
    name: str = "KexTrack",
    collection: bpy.types.Collection | None = None,
    handle_scale: float = 0.3,
) -> bpy.types.Object:
    """Create a Blender curve with separate splines per section.

    This prevents false connections between discontinuous track segments
    (e.g., shuttle coasters where the train reverses direction).

    Args:
        spline_points: Sequence of SplinePoints from kex_build output.
        sections: Sequence of Sections defining point ranges.
        name: Name for the curve object.
        collection: Collection to link the object to.
        handle_scale: Scale factor for handle length based on arc distance.

    Returns:
        The created curve object.
    """
    if not spline_points:
        raise ValueError("No spline points provided")

    # Create curve data
    curve_data = bpy.data.curves.new(name=name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 12

    # Create one spline per rendered section
    for section in sections:
        if not section.is_rendered():
            continue

        start = section.spline_start_index
        end = section.spline_end_index
        if start < 0 or end < 0 or start >= end:
            continue

        section_points = spline_points[start:end]
        if len(section_points) < 2:
            continue

        spline = curve_data.splines.new(type='BEZIER')
        spline.bezier_points.add(len(section_points) - 1)

        for i, sp in enumerate(section_points):
            bp = spline.bezier_points[i]
            pos = kex_to_blender_vector(sp.position)
            direction = kex_to_blender_vector(sp.direction)

            # Calculate handle offset based on arc distance to neighbors
            if i == 0:
                arc_delta = section_points[1].arc - sp.arc if len(section_points) > 1 else 1.0
            elif i == len(section_points) - 1:
                arc_delta = sp.arc - section_points[i - 1].arc
            else:
                arc_delta = (section_points[i + 1].arc - section_points[i - 1].arc) / 2

            handle_length = arc_delta * handle_scale

            bp.co = pos
            bp.handle_left_type = 'FREE'
            bp.handle_right_type = 'FREE'
            bp.handle_left = pos - direction * handle_length
            bp.handle_right = pos + direction * handle_length
            bp.tilt = _calculate_tilt(sp)

    # Create object and link to scene
    curve_obj = bpy.data.objects.new(name, curve_data)

    if collection is None:
        collection = bpy.context.scene.collection
    collection.objects.link(curve_obj)

    return curve_obj


def update_track_curve(
    curve_obj: bpy.types.Object,
    spline_points: Sequence[SplinePoint],
) -> None:
    """Update an existing curve with new SplinePoints.

    Preserves the object but replaces spline data.

    Args:
        curve_obj: Existing curve object to update.
        spline_points: New SplinePoints to set.
    """
    if not spline_points:
        return

    curve_data = curve_obj.data
    if not isinstance(curve_data, bpy.types.Curve):
        raise TypeError(f"Object {curve_obj.name} is not a curve")

    # Clear existing splines
    curve_data.splines.clear()

    # Create new spline
    spline = curve_data.splines.new(type='POLY')
    spline.points.add(len(spline_points) - 1)

    for i, sp in enumerate(spline_points):
        point = spline.points[i]
        pos = kex_to_blender_vector(sp.position)
        point.co = (pos.x, pos.y, pos.z, 1.0)
        point.tilt = _calculate_tilt(sp)


def update_track_from_sections(
    curve_obj: bpy.types.Object,
    spline_points: Sequence[SplinePoint],
    sections: Sequence[Section],
    handle_scale: float = 0.3,
) -> None:
    """Update an existing curve with new section-aware SplinePoints.

    Preserves the object identity and custom properties but replaces geometry.

    Args:
        curve_obj: Existing curve object to update.
        spline_points: New SplinePoints to set.
        sections: Section definitions for spline boundaries.
        handle_scale: Scale factor for Bezier handles.
    """
    if not spline_points:
        return

    curve_data = curve_obj.data
    if not isinstance(curve_data, bpy.types.Curve):
        raise TypeError(f"Object {curve_obj.name} is not a curve")

    # Clear existing splines
    curve_data.splines.clear()

    # Recreate splines per section (same logic as create_track_from_sections)
    for section in sections:
        if not section.is_rendered():
            continue

        start = section.spline_start_index
        end = section.spline_end_index
        if start < 0 or end < 0 or start >= end:
            continue

        section_points = spline_points[start:end]
        if len(section_points) < 2:
            continue

        spline = curve_data.splines.new(type='BEZIER')
        spline.bezier_points.add(len(section_points) - 1)

        for i, sp in enumerate(section_points):
            bp = spline.bezier_points[i]
            pos = kex_to_blender_vector(sp.position)
            direction = kex_to_blender_vector(sp.direction)

            # Calculate handle offset based on arc distance to neighbors
            if i == 0:
                arc_delta = section_points[1].arc - sp.arc if len(section_points) > 1 else 1.0
            elif i == len(section_points) - 1:
                arc_delta = sp.arc - section_points[i - 1].arc
            else:
                arc_delta = (section_points[i + 1].arc - section_points[i - 1].arc) / 2

            handle_length = arc_delta * handle_scale

            bp.co = pos
            bp.handle_left_type = 'FREE'
            bp.handle_right_type = 'FREE'
            bp.handle_left = pos - direction * handle_length
            bp.handle_right = pos + direction * handle_length
            bp.tilt = _calculate_tilt(sp)
