"""Core data types matching kexengine Rust FFI structs.

These are ctypes Structure definitions that match the C-compatible
Rust structs exactly. Field order and sizes must match.
"""

from __future__ import annotations

import ctypes
from dataclasses import dataclass
from enum import IntEnum
from typing import Sequence


# --- Math primitives ---


class Float3(ctypes.Structure):
    """3D vector matching Rust Float3."""

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
    """Keyframe interpolation type matching Rust enum."""

    CONSTANT = 0
    LINEAR = 1
    BEZIER = 2


class Keyframe(ctypes.Structure):
    """Animation keyframe matching Rust Keyframe."""

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
    """Track simulation point matching Rust Point."""

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
    """Resampled spline point matching Rust SplinePoint."""

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
    """Link to another section matching Rust SectionLink."""

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
    """Track section matching Rust Section."""

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
        ("style_index", ctypes.c_uint8),
    ]

    def is_valid(self) -> bool:
        return self.start_index >= 0

    def is_reversed(self) -> bool:
        return (self.flags & self.FLAG_REVERSED) != 0

    def is_rendered(self) -> bool:
        return (self.flags & self.FLAG_RENDERED) != 0

    def __repr__(self) -> str:
        return f"Section({self.start_index}..{self.end_index}, arc={self.arc_start:.2f}..{self.arc_end:.2f})"


# --- FFI Document/Output structures ---


class KexDocument(ctypes.Structure):
    """FFI input document matching Rust KexDocument."""

    _fields_ = [
        # Graph - nodes
        ("node_ids", ctypes.POINTER(ctypes.c_uint32)),
        ("node_count", ctypes.c_size_t),
        ("node_types", ctypes.POINTER(ctypes.c_uint32)),
        ("node_input_counts", ctypes.POINTER(ctypes.c_int32)),
        ("node_output_counts", ctypes.POINTER(ctypes.c_int32)),
        # Graph - ports
        ("port_ids", ctypes.POINTER(ctypes.c_uint32)),
        ("port_count", ctypes.c_size_t),
        ("port_types", ctypes.POINTER(ctypes.c_uint32)),
        ("port_owners", ctypes.POINTER(ctypes.c_uint32)),
        ("port_is_input", ctypes.POINTER(ctypes.c_uint8)),
        # Graph - edges
        ("edge_ids", ctypes.POINTER(ctypes.c_uint32)),
        ("edge_count", ctypes.c_size_t),
        ("edge_sources", ctypes.POINTER(ctypes.c_uint32)),
        ("edge_targets", ctypes.POINTER(ctypes.c_uint32)),
        # Properties - scalars
        ("scalar_keys", ctypes.POINTER(ctypes.c_uint64)),
        ("scalar_values", ctypes.POINTER(ctypes.c_float)),
        ("scalar_count", ctypes.c_size_t),
        # Properties - vectors
        ("vector_keys", ctypes.POINTER(ctypes.c_uint64)),
        ("vector_values", ctypes.POINTER(Float3)),
        ("vector_count", ctypes.c_size_t),
        # Properties - flags
        ("flag_keys", ctypes.POINTER(ctypes.c_uint64)),
        ("flag_values", ctypes.POINTER(ctypes.c_int32)),
        ("flag_count", ctypes.c_size_t),
        # Keyframes
        ("keyframes", ctypes.POINTER(Keyframe)),
        ("keyframe_count", ctypes.c_size_t),
        ("keyframe_range_keys", ctypes.POINTER(ctypes.c_uint64)),
        ("keyframe_range_starts", ctypes.POINTER(ctypes.c_int32)),
        ("keyframe_range_lengths", ctypes.POINTER(ctypes.c_int32)),
        ("keyframe_range_count", ctypes.c_size_t),
    ]


class KexOutput(ctypes.Structure):
    """FFI output buffers matching Rust KexOutput."""

    _fields_ = [
        # Raw simulation points
        ("points", ctypes.POINTER(Point)),
        ("points_capacity", ctypes.c_size_t),
        # Sections
        ("sections", ctypes.POINTER(Section)),
        ("sections_capacity", ctypes.c_size_t),
        ("section_node_ids", ctypes.POINTER(ctypes.c_uint32)),
        # Traversal order
        ("traversal_order", ctypes.POINTER(ctypes.c_int32)),
        ("traversal_capacity", ctypes.c_size_t),
        # Spline data
        ("spline_points", ctypes.POINTER(SplinePoint)),
        ("spline_capacity", ctypes.c_size_t),
        ("spline_velocities", ctypes.POINTER(ctypes.c_float)),
        ("spline_normal_forces", ctypes.POINTER(ctypes.c_float)),
        ("spline_lateral_forces", ctypes.POINTER(ctypes.c_float)),
        ("spline_roll_speeds", ctypes.POINTER(ctypes.c_float)),
        # Output counts (written by kex_build)
        ("points_count", ctypes.POINTER(ctypes.c_size_t)),
        ("sections_count", ctypes.POINTER(ctypes.c_size_t)),
        ("traversal_count", ctypes.POINTER(ctypes.c_size_t)),
        ("spline_count", ctypes.POINTER(ctypes.c_size_t)),
    ]


# --- Node types (matching C# enum values used in kexengine) ---


class NodeType(IntEnum):
    """Node type IDs matching kexengine conventions."""

    SCALAR = 0
    VECTOR = 1
    FORCE = 2
    GEOMETRIC = 3
    CURVED = 4
    COPY_PATH = 5
    BRIDGE = 6
    ANCHOR = 7
    REVERSE = 8
    REVERSE_PATH = 9


class PortDataType(IntEnum):
    """Port data types matching kexengine."""

    SCALAR = 0
    VECTOR = 1
    ANCHOR = 2
    PATH = 3


# --- Helper for encoding keys ---


def input_key(node_id: int, input_index: int) -> int:
    """Encode a node input key as (node_id << 8) | input_index."""
    return (node_id << 8) | (input_index & 0xFF)


def port_spec(data_type: PortDataType, local_index: int) -> int:
    """Encode a port spec as (data_type << 8) | local_index."""
    return (int(data_type) << 8) | (local_index & 0xFF)
