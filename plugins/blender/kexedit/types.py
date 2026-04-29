"""ctypes structures mirroring the kexengine FFI ABI.

Field order and sizes mirror the Rust `#[repr(C)]` structs in
`packages/core/src/ffi/mod.rs`. Keep them in sync.
"""

from __future__ import annotations

import ctypes
from dataclasses import dataclass
from enum import IntEnum
from typing import Sequence


# --- Math primitives ---


class Float3(ctypes.Structure):
    """3D vector — mirrors Rust Float3."""

    _fields_ = [
        ("x", ctypes.c_float),
        ("y", ctypes.c_float),
        ("z", ctypes.c_float),
    ]

    def __repr__(self) -> str:
        return f"Float3({self.x:.3f}, {self.y:.3f}, {self.z:.3f})"

    @classmethod
    def from_tuple(cls, t: tuple[float, float, float]) -> Float3:
        return cls(t[0], t[1], t[2])

    def to_tuple(self) -> tuple[float, float, float]:
        return (self.x, self.y, self.z)


# --- Keyframes ---


class InterpolationType(IntEnum):
    """Keyframe interpolation type — mirrors Rust enum."""

    CONSTANT = 0
    LINEAR = 1
    BEZIER = 2


class Keyframe(ctypes.Structure):
    """Animation keyframe — mirrors Rust Keyframe."""

    _fields_ = [
        ("time", ctypes.c_float),
        ("value", ctypes.c_float),
        ("in_interpolation", ctypes.c_int),  # InterpolationType
        ("out_interpolation", ctypes.c_int),  # InterpolationType
        ("in_tangent", ctypes.c_float),
        ("out_tangent", ctypes.c_float),
        ("in_weight", ctypes.c_float),
        ("out_weight", ctypes.c_float),
    ]

    def __repr__(self) -> str:
        return f"Keyframe(t={self.time:.3f}, v={self.value:.3f})"

    @classmethod
    def simple(cls, time: float, value: float) -> Keyframe:
        """Create a keyframe with default Bezier interpolation."""
        return cls(
            time=time,
            value=value,
            in_interpolation=InterpolationType.BEZIER,
            out_interpolation=InterpolationType.BEZIER,
            in_tangent=0.0,
            out_tangent=0.0,
            in_weight=1.0 / 3.0,
            out_weight=1.0 / 3.0,
        )

    @classmethod
    def linear(cls, time: float, value: float) -> Keyframe:
        """Create a keyframe with linear interpolation."""
        return cls(
            time=time,
            value=value,
            in_interpolation=InterpolationType.LINEAR,
            out_interpolation=InterpolationType.LINEAR,
            in_tangent=0.0,
            out_tangent=0.0,
            in_weight=1.0 / 3.0,
            out_weight=1.0 / 3.0,
        )


# --- Simulation Point ---


class Point(ctypes.Structure):
    """Track simulation point — mirrors Rust Point."""

    _fields_ = [
        ("heart_position", Float3),
        ("direction", Float3),
        ("normal", Float3),
        ("lateral", Float3),
        ("velocity", ctypes.c_float),
        ("normal_force", ctypes.c_float),
        ("lateral_force", ctypes.c_float),
        ("heart_arc", ctypes.c_float),
        ("spine_arc", ctypes.c_float),
        ("heart_advance", ctypes.c_float),
        ("friction_origin", ctypes.c_float),
        ("roll_speed", ctypes.c_float),
        ("heart_offset", ctypes.c_float),
        ("friction", ctypes.c_float),
        ("resistance", ctypes.c_float),
    ]

    def __repr__(self) -> str:
        return f"Point(pos={self.heart_position}, arc={self.spine_arc:.2f})"

    def spine_position(self) -> Float3:
        """Calculate spine position from heart position and offset."""
        return Float3(
            self.heart_position.x + self.normal.x * self.heart_offset,
            self.heart_position.y + self.normal.y * self.heart_offset,
            self.heart_position.z + self.normal.z * self.heart_offset,
        )


# --- Spline Output ---


class SplinePoint(ctypes.Structure):
    """Resampled spline point — mirrors Rust SplinePoint."""

    _fields_ = [
        ("arc", ctypes.c_float),
        ("position", Float3),
        ("direction", Float3),
        ("normal", Float3),
        ("lateral", Float3),
    ]

    def __repr__(self) -> str:
        return f"SplinePoint(arc={self.arc:.2f}, pos={self.position})"


# --- Sections ---


class SectionLink(ctypes.Structure):
    """Link to another section — mirrors Rust SectionLink."""

    FLAG_AT_START = 0x01
    FLAG_FLIP = 0x02

    _fields_ = [
        ("index", ctypes.c_int32),
        ("flags", ctypes.c_uint8),
    ]

    def is_valid(self) -> bool:
        return self.index >= 0

    def at_start(self) -> bool:
        return (self.flags & self.FLAG_AT_START) != 0

    def flip(self) -> bool:
        return (self.flags & self.FLAG_FLIP) != 0


class Section(ctypes.Structure):
    """Track section — mirrors Rust Section."""

    FLAG_REVERSED = 0x01
    FLAG_RENDERED = 0x02

    _fields_ = [
        ("start_index", ctypes.c_int32),
        ("end_index", ctypes.c_int32),
        ("arc_start", ctypes.c_float),
        ("arc_end", ctypes.c_float),
        ("flags", ctypes.c_uint8),
        ("next", SectionLink),
        ("prev", SectionLink),
        ("spline_start_index", ctypes.c_int32),
        ("spline_end_index", ctypes.c_int32),
    ]

    def is_valid(self) -> bool:
        return self.start_index >= 0

    def is_reversed(self) -> bool:
        return (self.flags & self.FLAG_REVERSED) != 0

    def is_rendered(self) -> bool:
        return (self.flags & self.FLAG_RENDERED) != 0

    def __repr__(self) -> str:
        return f"Section({self.start_index}..{self.end_index}, arc={self.arc_start:.2f}..{self.arc_end:.2f})"


# --- Node types (Rust enum order; see packages/core/src/nodes/schema.rs) ---


class NodeType(IntEnum):
    """Node type IDs — mirrors Rust `NodeType`."""

    FORCE = 0
    GEOMETRIC = 1
    CURVED = 2
    COPY_PATH = 3
    BRIDGE = 4
    ANCHOR = 5
    REVERSE = 6
    REVERSE_PATH = 7


class PortDataType(IntEnum):
    """Port data types — mirrors Rust `PortDataType`."""

    SCALAR = 0
    VECTOR = 1
    ANCHOR = 2
    PATH = 3


class NodeMeta(IntEnum):
    """Per-node metadata slots — mirrors Rust `NodeMeta`.

    Stored in the same `(node_id << 8) | slot` map as port inputs, with
    values in the high range so they don't collide with port indices.
    """

    DURATION = 240
    DURATION_TYPE = 241
    DRIVEN = 242
    STEERING = 243
    PRIORITY = 244
    FACING = 245
    RENDER = 246


# --- Helper for encoding keys ---


def input_key(node_id: int, slot: int) -> int:
    """Encode a `(node_id << 8) | slot` key. `slot` is a port index or NodeMeta."""
    return (node_id << 8) | (slot & 0xFF)


def port_spec(data_type: PortDataType, local_index: int) -> int:
    """Encode a port spec as (data_type << 8) | local_index."""
    return (int(data_type) << 8) | (local_index & 0xFF)
