"""FFI bindings to kexengine Rust library.

Loads the platform-specific shared library and provides a Python interface
to the kex_build function.
"""

from __future__ import annotations

import ctypes
import os
import platform
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional, Sequence

from .types import (
    Float3,
    Keyframe,
    KexDocument,
    KexOutput,
    NodeType,
    Point,
    PortDataType,
    Section,
    SplinePoint,
    input_key,
    port_spec,
)


class KexError(Exception):
    """Exception raised by kexengine FFI calls."""

    pass


def _get_library_path() -> Path:
    """Get the path to the kexengine shared library for the current platform."""
    lib_dir = Path(__file__).parent.parent / "lib"

    system = platform.system()
    if system == "Windows":
        lib_name = "kexengine.dll"
    elif system == "Darwin":
        lib_name = "libkexengine.dylib"
    else:  # Linux and others
        lib_name = "libkexengine.so"

    return lib_dir / lib_name


def _load_library() -> Optional[ctypes.CDLL]:
    """Load the kexengine shared library."""
    lib_path = _get_library_path()

    if not lib_path.exists():
        return None

    try:
        lib = ctypes.CDLL(str(lib_path))

        # Set up kex_build function signature
        lib.kex_build.argtypes = [
            ctypes.POINTER(KexDocument),  # doc
            ctypes.c_float,  # resolution
            ctypes.c_int,  # default_style_index
            ctypes.POINTER(KexOutput),  # output
        ]
        lib.kex_build.restype = ctypes.c_int

        return lib
    except OSError as e:
        print(f"Failed to load kexengine library: {e}")
        return None


# Global library instance (lazy loaded)
_lib: Optional[ctypes.CDLL] = None


def get_library() -> ctypes.CDLL:
    """Get the loaded library, raising if not available."""
    global _lib
    if _lib is None:
        _lib = _load_library()
    if _lib is None:
        raise KexError(f"kexengine library not found at {_get_library_path()}")
    return _lib


def is_library_available() -> bool:
    """Check if the kexengine library is available."""
    global _lib
    if _lib is None:
        _lib = _load_library()
    return _lib is not None


# --- High-level wrapper ---


@dataclass
class BuildResult:
    """Result of a kex_build call."""

    points: list[Point] = field(default_factory=list)
    sections: list[Section] = field(default_factory=list)
    section_node_ids: list[int] = field(default_factory=list)
    traversal_order: list[int] = field(default_factory=list)
    spline_points: list[SplinePoint] = field(default_factory=list)
    spline_velocities: list[float] = field(default_factory=list)
    spline_normal_forces: list[float] = field(default_factory=list)
    spline_lateral_forces: list[float] = field(default_factory=list)
    spline_roll_speeds: list[float] = field(default_factory=list)


class KexEngine:
    """High-level interface to kexengine.

    Example usage:
        engine = KexEngine()

        # Add an anchor node
        anchor_id = engine.add_anchor(
            position=(0, 3, 0),
            pitch=0, yaw=0, roll=0,
            velocity=10.0
        )

        # Add a force node
        force_id = engine.add_force(anchor_id, duration=5.0)

        # Build the track
        result = engine.build(resolution=0.5)

        for sp in result.spline_points:
            print(sp.position)
    """

    # Default buffer capacities
    DEFAULT_POINTS_CAPACITY = 100000
    DEFAULT_SECTIONS_CAPACITY = 1000
    DEFAULT_TRAVERSAL_CAPACITY = 1000
    DEFAULT_SPLINE_CAPACITY = 500000

    def __init__(self) -> None:
        self._next_node_id = 1
        self._next_port_id = 1
        self._next_edge_id = 1

        # Graph data
        self._node_ids: list[int] = []
        self._node_types: list[int] = []
        self._node_input_counts: list[int] = []
        self._node_output_counts: list[int] = []

        self._port_ids: list[int] = []
        self._port_types: list[int] = []
        self._port_owners: list[int] = []
        self._port_is_input: list[bool] = []

        self._edge_ids: list[int] = []
        self._edge_sources: list[int] = []
        self._edge_targets: list[int] = []

        # Property maps
        self._scalars: dict[int, float] = {}
        self._vectors: dict[int, tuple[float, float, float]] = {}
        self._flags: dict[int, int] = {}

        # Keyframes
        self._keyframes: list[Keyframe] = []
        self._keyframe_ranges: dict[int, tuple[int, int]] = {}  # key -> (start, length)

        # Node output port tracking for connections
        self._node_anchor_output: dict[int, int] = {}  # node_id -> port_id
        self._node_path_output: dict[int, int] = {}  # node_id -> port_id

    def _alloc_node_id(self) -> int:
        node_id = self._next_node_id
        self._next_node_id += 1
        return node_id

    def _alloc_port_id(self) -> int:
        port_id = self._next_port_id
        self._next_port_id += 1
        return port_id

    def _alloc_edge_id(self) -> int:
        edge_id = self._next_edge_id
        self._next_edge_id += 1
        return edge_id

    def _add_node(
        self, node_type: NodeType, input_count: int, output_count: int
    ) -> int:
        """Add a node and return its ID."""
        node_id = self._alloc_node_id()
        self._node_ids.append(node_id)
        self._node_types.append(int(node_type))
        self._node_input_counts.append(input_count)
        self._node_output_counts.append(output_count)
        return node_id

    def _add_port(
        self, owner: int, data_type: PortDataType, local_index: int, is_input: bool
    ) -> int:
        """Add a port and return its ID."""
        port_id = self._alloc_port_id()
        self._port_ids.append(port_id)
        self._port_types.append(port_spec(data_type, local_index))
        self._port_owners.append(owner)
        self._port_is_input.append(is_input)
        return port_id

    def _add_edge(self, source_port: int, target_port: int) -> int:
        """Add an edge and return its ID."""
        edge_id = self._alloc_edge_id()
        self._edge_ids.append(edge_id)
        self._edge_sources.append(source_port)
        self._edge_targets.append(target_port)
        return edge_id

    def _set_scalar(self, node_id: int, input_index: int, value: float) -> None:
        """Set a scalar property on a node input."""
        key = input_key(node_id, input_index)
        self._scalars[key] = value

    def _set_vector(
        self, node_id: int, input_index: int, value: tuple[float, float, float]
    ) -> None:
        """Set a vector property on a node input."""
        key = input_key(node_id, input_index)
        self._vectors[key] = value

    def _set_flag(self, node_id: int, input_index: int, value: int) -> None:
        """Set a flag property on a node input."""
        key = input_key(node_id, input_index)
        self._flags[key] = value

    # --- Node creation methods ---

    def add_anchor(
        self,
        position: tuple[float, float, float] = (0.0, 3.0, 0.0),
        pitch: float = 0.0,
        yaw: float = 0.0,
        roll: float = 0.0,
        velocity: float = 10.0,
        heart_offset: float = 1.1,
        friction: float = 0.021,
        resistance: float = 2e-5,
    ) -> int:
        """Add an Anchor node.

        Returns the node ID.
        """
        # Anchor has 8 inputs (position, pitch, yaw, roll, velocity, heart, friction, resistance)
        # and 1 output (Anchor)
        node_id = self._add_node(NodeType.ANCHOR, 8, 1)

        # Add input ports (Position=Vector at 0, rest are scalars at 1-7)
        self._add_port(node_id, PortDataType.VECTOR, 0, True)  # Position
        for i in range(1, 8):
            self._add_port(node_id, PortDataType.SCALAR, i, True)

        # Add output port (Anchor)
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        self._node_anchor_output[node_id] = anchor_out

        # Set properties
        self._set_vector(node_id, 0, position)
        self._set_scalar(node_id, 1, pitch)
        self._set_scalar(node_id, 2, yaw)
        self._set_scalar(node_id, 3, roll)
        self._set_scalar(node_id, 4, velocity)
        self._set_scalar(node_id, 5, heart_offset)
        self._set_scalar(node_id, 6, friction)
        self._set_scalar(node_id, 7, resistance)

        return node_id

    def add_force(
        self,
        source_node: int,
        duration: float = 5.0,
        priority: float = 0.0,
        rendered: bool = True,
    ) -> int:
        """Add a Force node connected to a source anchor.

        Returns the node ID.
        """
        # Force has 2 inputs (Anchor, Duration) and 2 outputs (Anchor, Path)
        node_id = self._add_node(NodeType.FORCE, 2, 2)

        # Add input ports
        anchor_in = self._add_port(node_id, PortDataType.ANCHOR, 0, True)
        self._add_port(node_id, PortDataType.SCALAR, 1, True)  # Duration

        # Add output ports
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        path_out = self._add_port(node_id, PortDataType.PATH, 0, False)
        self._node_anchor_output[node_id] = anchor_out
        self._node_path_output[node_id] = path_out

        # Connect to source
        if source_node in self._node_anchor_output:
            self._add_edge(self._node_anchor_output[source_node], anchor_in)

        # Set properties (using meta indices from Document.cs)
        self._set_scalar(node_id, 248, duration)  # Duration at meta index 248
        self._set_scalar(node_id, 249, priority)  # Priority at meta index 249
        self._set_flag(node_id, 254, 0 if rendered else 1)  # Render flag

        return node_id

    def add_geometric(
        self,
        source_node: int,
        duration: float = 5.0,
        priority: float = 0.0,
        rendered: bool = True,
    ) -> int:
        """Add a Geometric node connected to a source anchor.

        Returns the node ID.
        """
        # Geometric has 2 inputs (Anchor, Duration) and 2 outputs (Anchor, Path)
        node_id = self._add_node(NodeType.GEOMETRIC, 2, 2)

        # Add input ports
        anchor_in = self._add_port(node_id, PortDataType.ANCHOR, 0, True)
        self._add_port(node_id, PortDataType.SCALAR, 1, True)  # Duration

        # Add output ports
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        path_out = self._add_port(node_id, PortDataType.PATH, 0, False)
        self._node_anchor_output[node_id] = anchor_out
        self._node_path_output[node_id] = path_out

        # Connect to source
        if source_node in self._node_anchor_output:
            self._add_edge(self._node_anchor_output[source_node], anchor_in)

        # Set properties
        self._set_scalar(node_id, 248, duration)
        self._set_scalar(node_id, 249, priority)
        self._set_flag(node_id, 254, 0 if rendered else 1)

        return node_id

    def set_keyframes(
        self,
        node_id: int,
        property_id: int,
        keyframes: Sequence[Keyframe],
    ) -> None:
        """Set keyframes for a node property.

        property_id values (from PropertyId enum):
            0 = RollSpeed
            1 = NormalForce
            2 = LateralForce
            3 = PitchSpeed
            4 = YawSpeed
            5 = DrivenVelocity
            6 = HeartOffset
            7 = Friction
            8 = Resistance
            9 = TrackStyle
        """
        if not keyframes:
            return

        key = input_key(node_id, property_id)
        start_index = len(self._keyframes)
        self._keyframes.extend(keyframes)
        self._keyframe_ranges[key] = (start_index, len(keyframes))

    # --- Build ---

    def build(
        self,
        resolution: float = 0.5,
        default_style_index: int = 0,
        points_capacity: int = DEFAULT_POINTS_CAPACITY,
        sections_capacity: int = DEFAULT_SECTIONS_CAPACITY,
        traversal_capacity: int = DEFAULT_TRAVERSAL_CAPACITY,
        spline_capacity: int = DEFAULT_SPLINE_CAPACITY,
    ) -> BuildResult:
        """Build the track and return results.

        Raises KexError on failure.
        """
        lib = get_library()

        # Build document
        doc = self._build_document()

        # Allocate output buffers
        points_arr = (Point * points_capacity)()
        sections_arr = (Section * sections_capacity)()
        section_node_ids_arr = (ctypes.c_uint32 * sections_capacity)()
        traversal_arr = (ctypes.c_int32 * traversal_capacity)()
        spline_arr = (SplinePoint * spline_capacity)()
        spline_vel_arr = (ctypes.c_float * spline_capacity)()
        spline_nf_arr = (ctypes.c_float * spline_capacity)()
        spline_lf_arr = (ctypes.c_float * spline_capacity)()
        spline_rs_arr = (ctypes.c_float * spline_capacity)()

        points_count = ctypes.c_size_t(0)
        sections_count = ctypes.c_size_t(0)
        traversal_count = ctypes.c_size_t(0)
        spline_count = ctypes.c_size_t(0)

        output = KexOutput(
            points=ctypes.cast(points_arr, ctypes.POINTER(Point)),
            points_capacity=points_capacity,
            sections=ctypes.cast(sections_arr, ctypes.POINTER(Section)),
            sections_capacity=sections_capacity,
            section_node_ids=ctypes.cast(
                section_node_ids_arr, ctypes.POINTER(ctypes.c_uint32)
            ),
            traversal_order=ctypes.cast(traversal_arr, ctypes.POINTER(ctypes.c_int32)),
            traversal_capacity=traversal_capacity,
            spline_points=ctypes.cast(spline_arr, ctypes.POINTER(SplinePoint)),
            spline_capacity=spline_capacity,
            spline_velocities=ctypes.cast(spline_vel_arr, ctypes.POINTER(ctypes.c_float)),
            spline_normal_forces=ctypes.cast(spline_nf_arr, ctypes.POINTER(ctypes.c_float)),
            spline_lateral_forces=ctypes.cast(spline_lf_arr, ctypes.POINTER(ctypes.c_float)),
            spline_roll_speeds=ctypes.cast(spline_rs_arr, ctypes.POINTER(ctypes.c_float)),
            points_count=ctypes.pointer(points_count),
            sections_count=ctypes.pointer(sections_count),
            traversal_count=ctypes.pointer(traversal_count),
            spline_count=ctypes.pointer(spline_count),
        )

        # Call kex_build
        result_code = lib.kex_build(
            ctypes.byref(doc),
            ctypes.c_float(resolution),
            ctypes.c_int(default_style_index),
            ctypes.byref(output),
        )

        if result_code != 0:
            error_messages = {
                -1: "Null pointer passed to kex_build",
                -3: "Buffer overflow - increase capacity",
                -4: "Cycle detected in graph",
            }
            msg = error_messages.get(result_code, f"Unknown error code: {result_code}")
            raise KexError(msg)

        # Extract results
        result = BuildResult()

        for i in range(points_count.value):
            result.points.append(points_arr[i])

        for i in range(sections_count.value):
            result.sections.append(sections_arr[i])
            result.section_node_ids.append(section_node_ids_arr[i])

        for i in range(traversal_count.value):
            result.traversal_order.append(traversal_arr[i])

        for i in range(spline_count.value):
            result.spline_points.append(spline_arr[i])
            result.spline_velocities.append(spline_vel_arr[i])
            result.spline_normal_forces.append(spline_nf_arr[i])
            result.spline_lateral_forces.append(spline_lf_arr[i])
            result.spline_roll_speeds.append(spline_rs_arr[i])

        return result

    def _build_document(self) -> KexDocument:
        """Build the KexDocument structure for FFI."""
        # Convert lists to ctypes arrays
        node_ids = (ctypes.c_uint32 * len(self._node_ids))(*self._node_ids)
        node_types = (ctypes.c_uint32 * len(self._node_types))(*self._node_types)
        node_input_counts = (ctypes.c_int32 * len(self._node_input_counts))(
            *self._node_input_counts
        )
        node_output_counts = (ctypes.c_int32 * len(self._node_output_counts))(
            *self._node_output_counts
        )

        port_ids = (ctypes.c_uint32 * len(self._port_ids))(*self._port_ids)
        port_types = (ctypes.c_uint32 * len(self._port_types))(*self._port_types)
        port_owners = (ctypes.c_uint32 * len(self._port_owners))(*self._port_owners)
        port_is_input = (ctypes.c_uint8 * len(self._port_is_input))(
            *[1 if x else 0 for x in self._port_is_input]
        )

        edge_ids = (ctypes.c_uint32 * len(self._edge_ids))(*self._edge_ids)
        edge_sources = (ctypes.c_uint32 * len(self._edge_sources))(*self._edge_sources)
        edge_targets = (ctypes.c_uint32 * len(self._edge_targets))(*self._edge_targets)

        # Scalars
        scalar_keys = list(self._scalars.keys())
        scalar_values = list(self._scalars.values())
        scalar_keys_arr = (ctypes.c_uint64 * len(scalar_keys))(*scalar_keys)
        scalar_values_arr = (ctypes.c_float * len(scalar_values))(*scalar_values)

        # Vectors
        vector_keys = list(self._vectors.keys())
        vector_values = [Float3.from_tuple(v) for v in self._vectors.values()]
        vector_keys_arr = (ctypes.c_uint64 * len(vector_keys))(*vector_keys)
        vector_values_arr = (Float3 * len(vector_values))(*vector_values)

        # Flags
        flag_keys = list(self._flags.keys())
        flag_values = list(self._flags.values())
        flag_keys_arr = (ctypes.c_uint64 * len(flag_keys))(*flag_keys)
        flag_values_arr = (ctypes.c_int32 * len(flag_values))(*flag_values)

        # Keyframes
        keyframes_arr = (Keyframe * len(self._keyframes))(*self._keyframes)
        range_keys = list(self._keyframe_ranges.keys())
        range_starts = [self._keyframe_ranges[k][0] for k in range_keys]
        range_lengths = [self._keyframe_ranges[k][1] for k in range_keys]
        range_keys_arr = (ctypes.c_uint64 * len(range_keys))(*range_keys)
        range_starts_arr = (ctypes.c_int32 * len(range_starts))(*range_starts)
        range_lengths_arr = (ctypes.c_int32 * len(range_lengths))(*range_lengths)

        # Keep references to prevent garbage collection
        self._arrays = [
            node_ids,
            node_types,
            node_input_counts,
            node_output_counts,
            port_ids,
            port_types,
            port_owners,
            port_is_input,
            edge_ids,
            edge_sources,
            edge_targets,
            scalar_keys_arr,
            scalar_values_arr,
            vector_keys_arr,
            vector_values_arr,
            flag_keys_arr,
            flag_values_arr,
            keyframes_arr,
            range_keys_arr,
            range_starts_arr,
            range_lengths_arr,
        ]

        return KexDocument(
            node_ids=ctypes.cast(node_ids, ctypes.POINTER(ctypes.c_uint32)),
            node_count=len(self._node_ids),
            node_types=ctypes.cast(node_types, ctypes.POINTER(ctypes.c_uint32)),
            node_input_counts=ctypes.cast(
                node_input_counts, ctypes.POINTER(ctypes.c_int32)
            ),
            node_output_counts=ctypes.cast(
                node_output_counts, ctypes.POINTER(ctypes.c_int32)
            ),
            port_ids=ctypes.cast(port_ids, ctypes.POINTER(ctypes.c_uint32)),
            port_count=len(self._port_ids),
            port_types=ctypes.cast(port_types, ctypes.POINTER(ctypes.c_uint32)),
            port_owners=ctypes.cast(port_owners, ctypes.POINTER(ctypes.c_uint32)),
            port_is_input=ctypes.cast(port_is_input, ctypes.POINTER(ctypes.c_uint8)),
            edge_ids=ctypes.cast(edge_ids, ctypes.POINTER(ctypes.c_uint32)),
            edge_count=len(self._edge_ids),
            edge_sources=ctypes.cast(edge_sources, ctypes.POINTER(ctypes.c_uint32)),
            edge_targets=ctypes.cast(edge_targets, ctypes.POINTER(ctypes.c_uint32)),
            scalar_keys=ctypes.cast(scalar_keys_arr, ctypes.POINTER(ctypes.c_uint64)),
            scalar_values=ctypes.cast(scalar_values_arr, ctypes.POINTER(ctypes.c_float)),
            scalar_count=len(scalar_keys),
            vector_keys=ctypes.cast(vector_keys_arr, ctypes.POINTER(ctypes.c_uint64)),
            vector_values=ctypes.cast(vector_values_arr, ctypes.POINTER(Float3)),
            vector_count=len(vector_keys),
            flag_keys=ctypes.cast(flag_keys_arr, ctypes.POINTER(ctypes.c_uint64)),
            flag_values=ctypes.cast(flag_values_arr, ctypes.POINTER(ctypes.c_int32)),
            flag_count=len(flag_keys),
            keyframes=ctypes.cast(keyframes_arr, ctypes.POINTER(Keyframe)),
            keyframe_count=len(self._keyframes),
            keyframe_range_keys=ctypes.cast(
                range_keys_arr, ctypes.POINTER(ctypes.c_uint64)
            ),
            keyframe_range_starts=ctypes.cast(
                range_starts_arr, ctypes.POINTER(ctypes.c_int32)
            ),
            keyframe_range_lengths=ctypes.cast(
                range_lengths_arr, ctypes.POINTER(ctypes.c_int32)
            ),
            keyframe_range_count=len(range_keys),
        )

    def clear(self) -> None:
        """Clear all nodes, edges, and properties."""
        self._next_node_id = 1
        self._next_port_id = 1
        self._next_edge_id = 1
        self._node_ids.clear()
        self._node_types.clear()
        self._node_input_counts.clear()
        self._node_output_counts.clear()
        self._port_ids.clear()
        self._port_types.clear()
        self._port_owners.clear()
        self._port_is_input.clear()
        self._edge_ids.clear()
        self._edge_sources.clear()
        self._edge_targets.clear()
        self._scalars.clear()
        self._vectors.clear()
        self._flags.clear()
        self._keyframes.clear()
        self._keyframe_ranges.clear()
        self._node_anchor_output.clear()
        self._node_path_output.clear()
