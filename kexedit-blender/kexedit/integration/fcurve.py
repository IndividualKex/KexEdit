"""F-Curve to kexengine Keyframe conversion.

Reads Blender F-Curves and converts them to kexengine Keyframe format
for animated track properties like roll_speed, normal_force, lateral_force.
"""

from __future__ import annotations

from typing import Optional, TYPE_CHECKING

from ..core.types import Keyframe, InterpolationType

if TYPE_CHECKING:
    import bpy
    from bpy.types import FCurve, Object


# Property data path suffix → kexengine property ID
PROPERTY_ID_MAP: dict[str, int] = {
    'roll_speed': 0,       # RollSpeed
    'normal_force': 1,     # NormalForce
    'lateral_force': 2,    # LateralForce
    'pitch_speed': 3,      # PitchSpeed
    'yaw_speed': 4,        # YawSpeed
    'driven_velocity': 5,  # DrivenVelocity
    'heart_offset': 6,     # HeartOffset
    'friction': 7,         # Friction
    'resistance': 8,       # Resistance
}


def blender_to_kex_interpolation(blender_type: str) -> InterpolationType:
    """Map Blender interpolation type to kexengine InterpolationType.

    Args:
        blender_type: Blender's interpolation string.

    Returns:
        The corresponding kexengine InterpolationType.
    """
    mapping = {
        'CONSTANT': InterpolationType.CONSTANT,
        'LINEAR': InterpolationType.LINEAR,
        'BEZIER': InterpolationType.BEZIER,
    }
    return mapping.get(blender_type, InterpolationType.BEZIER)


def frame_to_time(frame: float) -> float:
    """Convert Blender frame number to kexengine time.

    Uses fixed mapping: 100 frames = 1 second of track progress.
    This avoids confusion with Blender's animation timeline.

    Args:
        frame: Blender frame number.

    Returns:
        Time in seconds (frame / 100).
    """
    return frame / 100.0


def calculate_tangent_and_weight(
    keyframe_co: tuple[float, float],
    handle_co: tuple[float, float],
    is_in: bool,
) -> tuple[float, float]:
    """Convert Blender handle position to tangent slope and raw weight.

    Blender handles are absolute positions (frame, value).
    kexengine expects:
      - tangent: slope (dy/dx)
      - weight: raw horizontal distance (normalized later)

    Args:
        keyframe_co: (frame, value) of the keyframe.
        handle_co: (frame, value) of the handle.
        is_in: True for incoming handle (left), False for outgoing (right).

    Returns:
        (tangent, raw_weight) tuple.
    """
    kx, ky = keyframe_co
    hx, hy = handle_co

    dx = hx - kx
    dy = hy - ky

    # Tangent is the slope
    if abs(dx) < 1e-10:
        tangent = 0.0
    else:
        tangent = dy / dx

    # Raw weight is the horizontal distance
    raw_weight = abs(dx)

    return tangent, raw_weight


def normalize_weight(
    raw_weight: float,
    prev_time: Optional[float],
    curr_time: float,
    next_time: Optional[float],
    is_in: bool,
) -> float:
    """Normalize handle weight relative to keyframe interval.

    kexengine weights are normalized to the interval between keyframes.
    A weight of 1/3 means the handle extends 1/3 of the way to the adjacent key.

    Args:
        raw_weight: Absolute horizontal distance to handle (in frames).
        prev_time: Time of previous keyframe (None if first).
        curr_time: Time of current keyframe.
        next_time: Time of next keyframe (None if last).
        is_in: True for incoming handle.

    Returns:
        Normalized weight (typically 0.0 to 1.0).
    """
    if is_in and prev_time is not None:
        interval = curr_time - prev_time
    elif not is_in and next_time is not None:
        interval = next_time - curr_time
    else:
        return 1.0 / 3.0

    if interval < 1e-10:
        return 1.0 / 3.0

    return min(raw_weight / interval, 1.0)


def read_fcurve_keyframes(
    fcurve: FCurve,
    duration: float,
) -> list[Keyframe]:
    """Convert a Blender F-Curve to kexengine Keyframes.

    Uses fixed mapping: 100 frames = 1 second of track progress.
    Keyframes outside [0, duration] are clamped.

    Args:
        fcurve: The Blender F-Curve to read.
        duration: Node duration in seconds (for clamping).

    Returns:
        List of kexengine Keyframe structures.
    """
    if fcurve is None or len(fcurve.keyframe_points) == 0:
        return []

    keyframes: list[Keyframe] = []
    points = fcurve.keyframe_points

    # First pass: convert times (100 frames = 1 second)
    times: list[float] = []
    for kp in points:
        frame = kp.co[0]
        time = frame_to_time(frame)
        # Clamp to valid duration range
        time = max(0.0, min(time, duration))
        times.append(time)

    # Second pass: build keyframes with normalized weights
    for i, kp in enumerate(points):
        frame, value = kp.co
        time = times[i]

        # Get interpolation type
        interp = blender_to_kex_interpolation(kp.interpolation)

        # Get tangents and weights from handles
        # Convert Blender Vectors to tuples for calculation
        if interp == InterpolationType.BEZIER:
            co_tuple = (kp.co[0], kp.co[1])
            handle_left_tuple = (kp.handle_left[0], kp.handle_left[1])
            handle_right_tuple = (kp.handle_right[0], kp.handle_right[1])

            in_tangent, in_weight_raw = calculate_tangent_and_weight(
                co_tuple, handle_left_tuple, is_in=True
            )
            out_tangent, out_weight_raw = calculate_tangent_and_weight(
                co_tuple, handle_right_tuple, is_in=False
            )

            # Normalize weights using frame intervals converted to time
            frame_interval_in = abs(kp.co[0] - kp.handle_left[0]) if i > 0 else 0
            frame_interval_out = abs(kp.handle_right[0] - kp.co[0]) if i < len(points) - 1 else 0

            prev_time = times[i - 1] if i > 0 else None
            next_time = times[i + 1] if i < len(points) - 1 else None

            in_weight = normalize_weight(
                frame_interval_in, prev_time, time, next_time, is_in=True
            )
            out_weight = normalize_weight(
                frame_interval_out, prev_time, time, next_time, is_in=False
            )
        else:
            in_tangent = 0.0
            out_tangent = 0.0
            in_weight = 1.0 / 3.0
            out_weight = 1.0 / 3.0

        kf = Keyframe(
            time=time,
            value=value,
            in_interpolation=int(interp),
            out_interpolation=int(interp),
            in_tangent=in_tangent,
            out_tangent=out_tangent,
            in_weight=in_weight,
            out_weight=out_weight,
        )
        keyframes.append(kf)

    return keyframes


def get_fcurves_for_object(obj: Object) -> dict[str, FCurve]:
    """Get F-Curves for animated kexedit properties on an object.

    Args:
        obj: Blender object with animation data.

    Returns:
        Dict mapping property name to FCurve.
    """
    result: dict[str, FCurve] = {}

    if obj.animation_data is None or obj.animation_data.action is None:
        return result

    action = obj.animation_data.action
    fcurves = []

    # Blender 4.4+ uses layered actions - need to access fcurves differently
    if hasattr(action, 'is_action_layered') and action.is_action_layered:
        # New system: action.layers[].strips[].fcurves
        for layer in action.layers:
            if hasattr(layer, 'strips'):
                for strip in layer.strips:
                    if hasattr(strip, 'fcurves'):
                        fcurves.extend(strip.fcurves)
                    elif hasattr(strip, 'channelbags'):
                        for bag in strip.channelbags:
                            if hasattr(bag, 'fcurves'):
                                fcurves.extend(bag.fcurves)
    # Legacy action (Blender < 4.4)
    elif hasattr(action, 'fcurves'):
        fcurves = list(action.fcurves)

    for fcurve in fcurves:
        data_path = fcurve.data_path
        # Look for kex_settings.force.* properties
        if 'kex_settings.force.' in data_path:
            prop_name = data_path.split('.')[-1]
            if prop_name in PROPERTY_ID_MAP:
                result[prop_name] = fcurve

    return result


def get_animated_property_names(obj: Object) -> set[str]:
    """Get names of properties that have F-Curves.

    Args:
        obj: Blender object with animation data.

    Returns:
        Set of property names like 'roll_speed', 'normal_force', etc.
    """
    return set(get_fcurves_for_object(obj).keys())


def extract_keyframes_for_node(
    obj: Object,
    duration: float,
) -> dict[int, list[Keyframe]]:
    """Extract all animated keyframes for a track node.

    Uses fixed mapping: 100 frames = 1 second of track progress.

    Args:
        obj: Blender object with kex_settings.
        duration: Node duration in seconds (for clamping).

    Returns:
        Dict mapping property_id to list of Keyframes.
    """
    fcurves = get_fcurves_for_object(obj)
    result: dict[int, list[Keyframe]] = {}

    for prop_name, fcurve in fcurves.items():
        prop_id = PROPERTY_ID_MAP.get(prop_name)
        if prop_id is None:
            continue

        keyframes = read_fcurve_keyframes(fcurve, duration)
        if keyframes:
            result[prop_id] = keyframes

    return result
