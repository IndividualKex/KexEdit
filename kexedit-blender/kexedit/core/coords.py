"""Coordinate system conversion between kexengine and Blender.

kexengine (Unity conventions):
    - Y-up, left-handed
    - X = right, Y = up, Z = forward
    - Angles in radians

Blender:
    - Z-up, right-handed
    - X = right, Y = forward, Z = up
    - Angles in degrees (UI convention)

The position/vector conversion is symmetric (its own inverse):
    kex(x, y, z) <-> blender(x, z, y)

Angle conversion includes degrees <-> radians transformation.
"""

from __future__ import annotations

import math
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .types import Float3


def degrees_to_radians(deg: float) -> float:
    """Convert degrees to radians."""
    return deg * (math.pi / 180.0)


def radians_to_degrees(rad: float) -> float:
    """Convert radians to degrees."""
    return rad * (180.0 / math.pi)


def kex_to_blender_position(x: float, y: float, z: float) -> tuple[float, float, float]:
    """Convert position from kexengine to Blender coordinates.

    kex(x, y, z) -> blender(x, z, y)
    """
    return (x, z, y)


def blender_to_kex_position(x: float, y: float, z: float) -> tuple[float, float, float]:
    """Convert position from Blender to kexengine coordinates.

    blender(x, y, z) -> kex(x, z, y)

    Note: This is the same transformation as kex_to_blender (symmetric).
    """
    return (x, z, y)


def kex_to_blender_direction(x: float, y: float, z: float) -> tuple[float, float, float]:
    """Convert direction vector from kexengine to Blender coordinates."""
    return (x, z, y)


def blender_to_kex_direction(x: float, y: float, z: float) -> tuple[float, float, float]:
    """Convert direction vector from Blender to kexengine coordinates."""
    return (x, z, y)


def kex_to_blender_angles(
    pitch: float, yaw: float, roll: float
) -> tuple[float, float, float]:
    """Convert Euler angles from kexengine (radians) to Blender (degrees).

    Args:
        pitch: Pitch in radians (nose up/down)
        yaw: Yaw in radians (turn left/right)
        roll: Roll in radians (bank left/right)

    Returns:
        (pitch, yaw, roll) in degrees for Blender UI.
    """
    return (
        radians_to_degrees(pitch),
        radians_to_degrees(yaw),
        radians_to_degrees(roll),
    )


def blender_to_kex_angles(
    pitch: float, yaw: float, roll: float
) -> tuple[float, float, float]:
    """Convert Euler angles from Blender (degrees) to kexengine (radians).

    Args:
        pitch: Pitch in degrees (nose up/down)
        yaw: Yaw in degrees (turn left/right)
        roll: Roll in degrees (bank left/right)

    Returns:
        (pitch, yaw, roll) in radians for kexengine.
    """
    return (
        degrees_to_radians(pitch),
        degrees_to_radians(yaw),
        degrees_to_radians(roll),
    )
