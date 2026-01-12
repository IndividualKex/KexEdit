"""Coordinate system conversion between kexengine and Blender.

kexengine (Unity conventions):
    - Y-up, left-handed
    - X = right, Y = up, Z = forward

Blender:
    - Z-up, right-handed
    - X = right, Y = forward, Z = up

The position/vector conversion is symmetric (its own inverse):
    kex(x, y, z) <-> blender(x, z, y)

Orientation angles need careful handling due to handedness difference.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .types import Float3


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
    """Convert Euler angles from kexengine to Blender conventions.

    In kexengine (Unity, left-handed Y-up):
        - Pitch: rotation around X axis (nose up/down)
        - Yaw: rotation around Y axis (turn left/right)
        - Roll: rotation around Z axis (bank left/right)

    In Blender (right-handed Z-up):
        - The axes are remapped: kex Y -> blender Z, kex Z -> blender Y
        - Handedness flip negates rotation direction around certain axes

    Returns (pitch, yaw, roll) in Blender conventions.
    """
    # Axis remapping: kex rotations around Y become rotations around Z in Blender
    # Handedness: left-handed to right-handed negates the rotation direction
    #
    # kex pitch (around X) -> blender pitch (around X), but sign flips due to handedness
    # kex yaw (around Y/up) -> blender yaw (around Z/up), sign flips
    # kex roll (around Z/forward) -> blender roll (around Y/forward), sign flips
    return (-pitch, -roll, -yaw)


def blender_to_kex_angles(
    pitch: float, yaw: float, roll: float
) -> tuple[float, float, float]:
    """Convert Euler angles from Blender to kexengine conventions.

    Inverse of kex_to_blender_angles.

    Returns (pitch, yaw, roll) in kexengine conventions.
    """
    # Inverse transformation
    return (-pitch, -roll, -yaw)
