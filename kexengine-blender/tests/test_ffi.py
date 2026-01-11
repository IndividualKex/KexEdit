"""Tests for kexengine FFI bindings.

Run with: python -m pytest tests/test_ffi.py -v
Or standalone: python tests/test_ffi.py
"""

from __future__ import annotations

import sys
from pathlib import Path

# Add parent to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from kexengine.core import (
    Float3,
    Keyframe,
    InterpolationType,
    KexEngine,
    KexError,
)
from kexengine.core.ffi import is_library_available, _get_library_path
from kexengine.core.types import NodeType, PortDataType, input_key


def test_float3_creation():
    """Test Float3 struct creation."""
    v = Float3(1.0, 2.0, 3.0)
    assert v.x == 1.0
    assert v.y == 2.0
    assert v.z == 3.0

    v2 = Float3.from_tuple((4.0, 5.0, 6.0))
    assert v2.to_tuple() == (4.0, 5.0, 6.0)


def test_keyframe_creation():
    """Test Keyframe struct creation."""
    kf = Keyframe.simple(1.0, 5.0)
    assert kf.time == 1.0
    assert kf.value == 5.0
    assert kf.in_interpolation == InterpolationType.BEZIER
    assert kf.out_interpolation == InterpolationType.BEZIER

    kf2 = Keyframe.linear(2.0, 10.0)
    assert kf2.in_interpolation == InterpolationType.LINEAR


def test_input_key_encoding():
    """Test input key encoding."""
    key = input_key(1, 5)
    assert key == (1 << 8) | 5
    assert key == 261

    key2 = input_key(100, 248)
    assert key2 == (100 << 8) | 248


def test_engine_node_creation():
    """Test KexEngine node creation."""
    engine = KexEngine()

    anchor_id = engine.add_anchor(
        position=(0.0, 3.0, 0.0),
        velocity=10.0,
    )
    assert anchor_id == 1

    force_id = engine.add_force(anchor_id, duration=5.0)
    assert force_id == 2

    # Check internal state
    assert len(engine._node_ids) == 2
    assert engine._node_types[0] == NodeType.ANCHOR
    assert engine._node_types[1] == NodeType.FORCE


def test_library_path():
    """Test library path detection."""
    lib_path = _get_library_path()
    print(f"Library path: {lib_path}")
    assert lib_path.name in ("kexengine.dll", "libkexengine.so", "libkexengine.dylib")


def test_library_available():
    """Test if library is available."""
    available = is_library_available()
    print(f"Library available: {available}")
    if not available:
        print(f"  Expected at: {_get_library_path()}")


def test_build_simple_track():
    """Test building a simple track (requires library)."""
    if not is_library_available():
        print("SKIP: Library not available")
        return

    engine = KexEngine()

    # Create a simple track: Anchor -> Force
    anchor_id = engine.add_anchor(
        position=(0.0, 3.0, 0.0),
        pitch=0.0,
        yaw=0.0,
        roll=0.0,
        velocity=10.0,
    )

    force_id = engine.add_force(anchor_id, duration=5.0)

    # Build
    result = engine.build(resolution=0.5)

    print(f"Points: {len(result.points)}")
    print(f"Sections: {len(result.sections)}")
    print(f"Spline points: {len(result.spline_points)}")

    assert len(result.spline_points) > 0, "Expected spline points"

    # Print first few spline points
    for i, sp in enumerate(result.spline_points[:5]):
        print(f"  [{i}] arc={sp.arc:.2f} pos=({sp.position.x:.2f}, {sp.position.y:.2f}, {sp.position.z:.2f})")


def test_build_with_keyframes():
    """Test building a track with keyframes (requires library)."""
    if not is_library_available():
        print("SKIP: Library not available")
        return

    engine = KexEngine()

    anchor_id = engine.add_anchor(position=(0.0, 10.0, 0.0), velocity=15.0)
    force_id = engine.add_force(anchor_id, duration=10.0)

    # Add keyframes for normal force (property 1)
    keyframes = [
        Keyframe.simple(0.0, 1.0),  # 1G at start
        Keyframe.simple(5.0, 2.0),  # 2G at midpoint
        Keyframe.simple(10.0, 1.0),  # Back to 1G
    ]
    engine.set_keyframes(force_id, 1, keyframes)

    result = engine.build(resolution=0.5)

    print(f"Points with keyframes: {len(result.points)}")
    print(f"Spline points: {len(result.spline_points)}")

    # Check that forces vary
    if result.spline_normal_forces:
        min_force = min(result.spline_normal_forces)
        max_force = max(result.spline_normal_forces)
        print(f"Normal force range: {min_force:.2f} to {max_force:.2f}")


def main():
    """Run tests manually."""
    print("=== kexengine FFI Tests ===\n")

    print("1. Float3 creation...")
    test_float3_creation()
    print("   OK\n")

    print("2. Keyframe creation...")
    test_keyframe_creation()
    print("   OK\n")

    print("3. Input key encoding...")
    test_input_key_encoding()
    print("   OK\n")

    print("4. Engine node creation...")
    test_engine_node_creation()
    print("   OK\n")

    print("5. Library path...")
    test_library_path()
    print("   OK\n")

    print("6. Library availability...")
    test_library_available()
    print()

    print("7. Build simple track...")
    test_build_simple_track()
    print()

    print("8. Build with keyframes...")
    test_build_with_keyframes()
    print()

    print("=== All tests passed ===")


if __name__ == "__main__":
    main()
