"""Tests for F-Curve to kexengine Keyframe conversion.

These tests cover the pure Python conversion functions that don't require Blender.
The functions are copied here to allow testing without bpy imports.
"""

from __future__ import annotations

from enum import IntEnum
from typing import Optional


# --- Copied from core/types.py ---

class InterpolationType(IntEnum):
    """Keyframe interpolation type matching Rust enum."""
    CONSTANT = 0
    LINEAR = 1
    BEZIER = 2


# --- Copied from integration/fcurve.py (pure functions only) ---

PROPERTY_ID_MAP: dict[str, int] = {
    'roll_speed': 0,
    'normal_force': 1,
    'lateral_force': 2,
    'pitch_speed': 3,
    'yaw_speed': 4,
    'driven_velocity': 5,
    'heart_offset': 6,
    'friction': 7,
    'resistance': 8,
}


def blender_to_kex_interpolation(blender_type: str) -> InterpolationType:
    """Map Blender interpolation type to kexengine InterpolationType."""
    mapping = {
        'CONSTANT': InterpolationType.CONSTANT,
        'LINEAR': InterpolationType.LINEAR,
        'BEZIER': InterpolationType.BEZIER,
    }
    return mapping.get(blender_type, InterpolationType.BEZIER)


def frame_to_time(frame: float) -> float:
    """Convert Blender frame number to kexengine time. 100 frames = 1 second."""
    return frame / 100.0


def calculate_tangent_and_weight(
    keyframe_co: tuple[float, float],
    handle_co: tuple[float, float],
    is_in: bool,
) -> tuple[float, float]:
    """Convert Blender handle position to tangent slope and raw weight."""
    kx, ky = keyframe_co
    hx, hy = handle_co

    dx = hx - kx
    dy = hy - ky

    if abs(dx) < 1e-10:
        tangent = 0.0
    else:
        tangent = dy / dx

    raw_weight = abs(dx)
    return tangent, raw_weight


def normalize_weight(
    raw_weight: float,
    prev_time: Optional[float],
    curr_time: float,
    next_time: Optional[float],
    is_in: bool,
) -> float:
    """Normalize handle weight relative to keyframe interval."""
    if is_in and prev_time is not None:
        interval = curr_time - prev_time
    elif not is_in and next_time is not None:
        interval = next_time - curr_time
    else:
        return 1.0 / 3.0

    if interval < 1e-10:
        return 1.0 / 3.0

    return min(raw_weight / interval, 1.0)


# --- Interpolation Mapping Tests ---

class TestInterpolationMapping:
    """Tests for blender_to_kex_interpolation."""

    def test_constant(self):
        assert blender_to_kex_interpolation('CONSTANT') == InterpolationType.CONSTANT

    def test_linear(self):
        assert blender_to_kex_interpolation('LINEAR') == InterpolationType.LINEAR

    def test_bezier(self):
        assert blender_to_kex_interpolation('BEZIER') == InterpolationType.BEZIER

    def test_unknown_defaults_to_bezier(self):
        assert blender_to_kex_interpolation('UNKNOWN') == InterpolationType.BEZIER
        assert blender_to_kex_interpolation('BEZIER_OPTIMIZED') == InterpolationType.BEZIER


# --- Frame to Time Conversion Tests ---

class TestFrameToTime:
    """Tests for frame_to_time conversion (100 frames = 1 second)."""

    def test_frame_zero(self):
        result = frame_to_time(0)
        assert result == 0.0

    def test_frame_100(self):
        result = frame_to_time(100)
        assert result == 1.0

    def test_frame_500(self):
        result = frame_to_time(500)
        assert result == 5.0

    def test_frame_250(self):
        result = frame_to_time(250)
        assert result == 2.5

    def test_frame_50(self):
        result = frame_to_time(50)
        assert result == 0.5

    def test_fractional_frame(self):
        result = frame_to_time(150)
        assert result == 1.5

    def test_negative_frame(self):
        # Negative frames give negative time (clamped later)
        result = frame_to_time(-100)
        assert result == -1.0


# --- Tangent and Weight Calculation Tests ---

class TestTangentCalculation:
    """Tests for calculate_tangent_and_weight."""

    def test_horizontal_handle(self):
        tangent, weight = calculate_tangent_and_weight((0, 0), (10, 0), is_in=False)
        assert tangent == 0.0
        assert weight == 10.0

    def test_45_degree_handle(self):
        tangent, weight = calculate_tangent_and_weight((0, 0), (1, 1), is_in=False)
        assert abs(tangent - 1.0) < 0.001
        assert weight == 1.0

    def test_negative_45_degree_handle(self):
        tangent, weight = calculate_tangent_and_weight((0, 0), (1, -1), is_in=False)
        assert abs(tangent - (-1.0)) < 0.001

    def test_steep_handle(self):
        tangent, weight = calculate_tangent_and_weight((0, 0), (1, 2), is_in=False)
        assert abs(tangent - 2.0) < 0.001

    def test_in_handle(self):
        tangent, weight = calculate_tangent_and_weight((10, 5), (5, 3), is_in=True)
        assert abs(tangent - 0.4) < 0.001
        assert weight == 5.0

    def test_nearly_vertical_handle(self):
        # Very small dx results in very large tangent (not 0)
        tangent, weight = calculate_tangent_and_weight((0, 0), (0.0000001, 1), is_in=False)
        # slope = 1 / 0.0000001 = 10000000
        assert tangent > 1000000

    def test_truly_vertical_handle(self):
        # Zero dx results in 0 tangent (fallback)
        tangent, weight = calculate_tangent_and_weight((0, 0), (0.0, 1), is_in=False)
        assert tangent == 0.0


# --- Weight Normalization Tests ---

class TestWeightNormalization:
    """Tests for normalize_weight."""

    def test_default_weight(self):
        weight = normalize_weight(1.0, None, 5.0, None, is_in=True)
        assert abs(weight - 1.0 / 3.0) < 0.001

    def test_in_weight_normalized(self):
        weight = normalize_weight(2.0, 0.0, 6.0, 12.0, is_in=True)
        assert abs(weight - 2.0 / 6.0) < 0.001

    def test_out_weight_normalized(self):
        weight = normalize_weight(3.0, 0.0, 6.0, 15.0, is_in=False)
        assert abs(weight - 3.0 / 9.0) < 0.001

    def test_weight_clamped_to_one(self):
        weight = normalize_weight(20.0, 0.0, 5.0, 10.0, is_in=False)
        assert weight == 1.0

    def test_zero_interval(self):
        weight = normalize_weight(1.0, 5.0, 5.0, 10.0, is_in=True)
        assert abs(weight - 1.0 / 3.0) < 0.001


# --- Property ID Mapping Tests ---

class TestPropertyIdMapping:
    """Tests for PROPERTY_ID_MAP."""

    def test_roll_speed(self):
        assert PROPERTY_ID_MAP['roll_speed'] == 0

    def test_normal_force(self):
        assert PROPERTY_ID_MAP['normal_force'] == 1

    def test_lateral_force(self):
        assert PROPERTY_ID_MAP['lateral_force'] == 2

    def test_all_properties_have_unique_ids(self):
        ids = list(PROPERTY_ID_MAP.values())
        assert len(ids) == len(set(ids))


# --- Run standalone ---

def run_tests():
    """Simple test runner for standalone execution."""
    import traceback

    test_classes = [
        TestInterpolationMapping,
        TestFrameToTime,
        TestTangentCalculation,
        TestWeightNormalization,
        TestPropertyIdMapping,
    ]

    passed = 0
    failed = 0

    for test_class in test_classes:
        instance = test_class()
        for method_name in dir(instance):
            if method_name.startswith('test_'):
                method = getattr(instance, method_name)
                try:
                    method()
                    print(f"  PASS: {test_class.__name__}.{method_name}")
                    passed += 1
                except AssertionError as e:
                    print(f"  FAIL: {test_class.__name__}.{method_name}")
                    traceback.print_exc()
                    failed += 1
                except Exception as e:
                    print(f"  ERROR: {test_class.__name__}.{method_name}: {e}")
                    traceback.print_exc()
                    failed += 1

    print(f"\n{passed} passed, {failed} failed")
    return failed == 0


if __name__ == '__main__':
    success = run_tests()
    exit(0 if success else 1)
