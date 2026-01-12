"""Tests for coordinate system conversion between kexengine and Blender.

These tests verify the correctness of coordinate transformations and
ensure round-trip conversions are identity operations.
"""

import math
import pytest

from kexedit.core.coords import (
    kex_to_blender_position,
    blender_to_kex_position,
    kex_to_blender_direction,
    blender_to_kex_direction,
    kex_to_blender_angles,
    blender_to_kex_angles,
)


class TestPositionConversion:
    """Test position/vector coordinate conversion."""

    def test_kex_up_becomes_blender_up(self):
        """kexengine Y-up should become Blender Z-up."""
        # kex: 3 meters up (Y=3)
        kex_pos = (0.0, 3.0, 0.0)
        blender_pos = kex_to_blender_position(*kex_pos)
        # blender: 3 meters up (Z=3)
        assert blender_pos == (0.0, 0.0, 3.0)

    def test_blender_up_becomes_kex_up(self):
        """Blender Z-up should become kexengine Y-up."""
        # blender: 3 meters up (Z=3)
        blender_pos = (0.0, 0.0, 3.0)
        kex_pos = blender_to_kex_position(*blender_pos)
        # kex: 3 meters up (Y=3)
        assert kex_pos == (0.0, 3.0, 0.0)

    def test_kex_forward_becomes_blender_forward(self):
        """kexengine Z-forward should become Blender Y-forward."""
        # kex: 5 meters forward (Z=5)
        kex_pos = (0.0, 0.0, 5.0)
        blender_pos = kex_to_blender_position(*kex_pos)
        # blender: 5 meters forward (Y=5)
        assert blender_pos == (0.0, 5.0, 0.0)

    def test_blender_forward_becomes_kex_forward(self):
        """Blender Y-forward should become kexengine Z-forward."""
        # blender: 5 meters forward (Y=5)
        blender_pos = (0.0, 5.0, 0.0)
        kex_pos = blender_to_kex_position(*blender_pos)
        # kex: 5 meters forward (Z=5)
        assert kex_pos == (0.0, 0.0, 5.0)

    def test_right_axis_unchanged(self):
        """X-axis (right) should be unchanged in both systems."""
        kex_pos = (2.0, 0.0, 0.0)
        blender_pos = kex_to_blender_position(*kex_pos)
        assert blender_pos == (2.0, 0.0, 0.0)

        blender_pos2 = (2.0, 0.0, 0.0)
        kex_pos2 = blender_to_kex_position(*blender_pos2)
        assert kex_pos2 == (2.0, 0.0, 0.0)

    def test_position_round_trip(self):
        """Round-trip conversion should return original value."""
        original = (1.5, 2.5, 3.5)

        # kex -> blender -> kex
        blender = kex_to_blender_position(*original)
        back_to_kex = blender_to_kex_position(*blender)
        assert back_to_kex == original

        # blender -> kex -> blender
        kex = blender_to_kex_position(*original)
        back_to_blender = kex_to_blender_position(*kex)
        assert back_to_blender == original

    def test_direction_same_as_position(self):
        """Direction vectors use same transformation as positions."""
        vec = (1.0, 2.0, 3.0)
        assert kex_to_blender_direction(*vec) == kex_to_blender_position(*vec)
        assert blender_to_kex_direction(*vec) == blender_to_kex_position(*vec)


class TestAngleConversion:
    """Test Euler angle conversion between coordinate systems."""

    def test_zero_angles_stay_zero(self):
        """Zero angles should remain zero after conversion."""
        kex_angles = (0.0, 0.0, 0.0)
        blender_angles = kex_to_blender_angles(*kex_angles)
        assert blender_angles == (0.0, 0.0, 0.0)

        blender_angles2 = (0.0, 0.0, 0.0)
        kex_angles2 = blender_to_kex_angles(*blender_angles2)
        assert kex_angles2 == (0.0, 0.0, 0.0)

    def test_angle_round_trip(self):
        """Round-trip angle conversion should return original value."""
        original = (15.0, 30.0, 45.0)

        # kex -> blender -> kex
        blender = kex_to_blender_angles(*original)
        back_to_kex = blender_to_kex_angles(*blender)
        assert back_to_kex == pytest.approx(original, abs=1e-10)

        # blender -> kex -> blender
        kex = blender_to_kex_angles(*original)
        back_to_blender = kex_to_blender_angles(*kex)
        assert back_to_blender == pytest.approx(original, abs=1e-10)

    def test_pitch_conversion(self):
        """Pitch (nose up/down) should convert with sign change."""
        # Positive pitch in kex = nose up
        kex_pitch = (30.0, 0.0, 0.0)
        blender_angles = kex_to_blender_angles(*kex_pitch)
        # Due to handedness flip, pitch sign changes
        assert blender_angles[0] == -30.0

    def test_yaw_becomes_roll_axis(self):
        """Kex yaw (around Y/up) becomes rotation around Blender Z/up."""
        kex_yaw = (0.0, 45.0, 0.0)
        blender_angles = kex_to_blender_angles(*kex_yaw)
        # Yaw in kex becomes the third component in blender (around Z)
        assert blender_angles[2] == -45.0

    def test_roll_becomes_yaw_axis(self):
        """Kex roll (around Z/forward) becomes rotation around Blender Y/forward."""
        kex_roll = (0.0, 0.0, 60.0)
        blender_angles = kex_to_blender_angles(*kex_roll)
        # Roll in kex becomes the second component in blender (around Y)
        assert blender_angles[1] == -60.0


class TestEdgeCases:
    """Test edge cases and special values."""

    def test_negative_positions(self):
        """Negative positions should convert correctly."""
        kex_pos = (-1.0, -2.0, -3.0)
        blender_pos = kex_to_blender_position(*kex_pos)
        assert blender_pos == (-1.0, -3.0, -2.0)

    def test_large_values(self):
        """Large values should convert correctly."""
        kex_pos = (1000.0, 2000.0, 3000.0)
        blender_pos = kex_to_blender_position(*kex_pos)
        back = blender_to_kex_position(*blender_pos)
        assert back == kex_pos

    def test_small_values(self):
        """Small floating point values should convert correctly."""
        kex_pos = (0.001, 0.002, 0.003)
        blender_pos = kex_to_blender_position(*kex_pos)
        back = blender_to_kex_position(*blender_pos)
        assert back == pytest.approx(kex_pos, abs=1e-10)


class TestConversionSymmetry:
    """Verify that conversions are symmetric/self-inverse."""

    def test_position_conversion_is_symmetric(self):
        """Position conversion should be its own inverse."""
        # The transformation (x, y, z) -> (x, z, y) is symmetric
        pos = (1.0, 2.0, 3.0)

        # Applying the same transform twice should give original
        once = kex_to_blender_position(*pos)
        twice = kex_to_blender_position(*once)
        assert twice == pos

        # Same for the other direction
        once2 = blender_to_kex_position(*pos)
        twice2 = blender_to_kex_position(*once2)
        assert twice2 == pos

    def test_angle_inverse_property(self):
        """Angle conversions should be proper inverses."""
        angles = (10.0, 20.0, 30.0)

        # kex_to_blender followed by blender_to_kex = identity
        converted = kex_to_blender_angles(*angles)
        restored = blender_to_kex_angles(*converted)
        assert restored == pytest.approx(angles, abs=1e-10)
