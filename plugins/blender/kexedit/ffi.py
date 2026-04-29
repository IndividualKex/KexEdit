"""FFI bindings to kexengine Rust library.

Loads the platform-specific shared library and exposes a Python `KexEngine`
that mirrors the in-memory document, serializes to `.kex` bytes, and calls
the handle-based C ABI in `packages/core/src/ffi/mod.rs` to build tracks.

The FFI surface is bytes-in / handles-out:

    handle = kex_load(bytes)
    output = kex_build(handle, resolution)
    kex_output_read_*(output, buffers, capacity)
    kex_output_free(output)
    kex_doc_free(handle)

Programmatic document construction lives entirely on the Python side; the
serializer mirrors the format documented in `packages/core/src/persistence/`.
"""

from __future__ import annotations

import ctypes
import platform
import struct
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional, Sequence

from .types import (
    Float3,
    InterpolationType,
    Keyframe,
    NodeMeta,
    NodeType,
    Point,
    PortDataType,
    Section,
    SplinePoint,
    input_key,
    port_spec,
)


# ---------------------------------------------------------------------------
# Library loading
# ---------------------------------------------------------------------------


class KexError(Exception):
    pass


def _get_library_path() -> Path:
    lib_dir = Path(__file__).parent / "lib"
    system = platform.system()
    if system == "Windows":
        lib_name = "kexengine.dll"
    elif system == "Darwin":
        lib_name = "libkexengine.dylib"
    else:
        lib_name = "libkexengine.so"
    return lib_dir / lib_name


KexDoc = ctypes.c_void_p
KexOutput = ctypes.c_void_p


class KexDocCounts(ctypes.Structure):
    _fields_ = [
        ("node_count", ctypes.c_int32),
        ("port_count", ctypes.c_int32),
        ("edge_count", ctypes.c_int32),
        ("scalar_count", ctypes.c_int32),
        ("vector_count", ctypes.c_int32),
        ("flag_count", ctypes.c_int32),
        ("keyframe_count", ctypes.c_int32),
        ("keyframe_range_count", ctypes.c_int32),
        ("next_node_id", ctypes.c_uint32),
        ("next_port_id", ctypes.c_uint32),
        ("next_edge_id", ctypes.c_uint32),
    ]


class KexOutputCounts(ctypes.Structure):
    _fields_ = [
        ("points_count", ctypes.c_int32),
        ("sections_count", ctypes.c_int32),
        ("traversal_count", ctypes.c_int32),
        ("spline_count", ctypes.c_int32),
    ]


def _load_library() -> Optional[ctypes.CDLL]:
    lib_path = _get_library_path()
    if not lib_path.exists():
        return None

    try:
        lib = ctypes.CDLL(str(lib_path))
    except OSError as e:
        print(f"Failed to load kexengine library: {e}")
        return None

    lib.kex_load.argtypes = [ctypes.POINTER(ctypes.c_uint8), ctypes.c_size_t]
    lib.kex_load.restype = KexDoc

    lib.kex_save_size.argtypes = [KexDoc]
    lib.kex_save_size.restype = ctypes.c_int64

    lib.kex_save.argtypes = [
        KexDoc,
        ctypes.POINTER(ctypes.c_uint8),
        ctypes.c_size_t,
        ctypes.POINTER(ctypes.c_size_t),
    ]
    lib.kex_save.restype = ctypes.c_int

    lib.kex_doc_free.argtypes = [KexDoc]
    lib.kex_doc_free.restype = None

    lib.kex_doc_get_counts.argtypes = [KexDoc, ctypes.POINTER(KexDocCounts)]
    lib.kex_doc_get_counts.restype = ctypes.c_int

    lib.kex_doc_read_graph.argtypes = [
        KexDoc,
        ctypes.POINTER(ctypes.c_uint32),  # node_ids
        ctypes.POINTER(ctypes.c_uint8),  # node_types
        ctypes.POINTER(ctypes.c_int32),  # node_input_counts
        ctypes.POINTER(ctypes.c_int32),  # node_output_counts
        ctypes.POINTER(ctypes.c_uint32),  # port_ids
        ctypes.POINTER(ctypes.c_uint32),  # port_types
        ctypes.POINTER(ctypes.c_uint32),  # port_owners
        ctypes.POINTER(ctypes.c_uint8),  # port_is_input
        ctypes.POINTER(ctypes.c_uint32),  # edge_ids
        ctypes.POINTER(ctypes.c_uint32),  # edge_sources
        ctypes.POINTER(ctypes.c_uint32),  # edge_targets
    ]
    lib.kex_doc_read_graph.restype = ctypes.c_int

    lib.kex_doc_read_properties.argtypes = [
        KexDoc,
        ctypes.POINTER(ctypes.c_uint64),  # scalar_keys
        ctypes.POINTER(ctypes.c_float),  # scalar_values
        ctypes.POINTER(ctypes.c_uint64),  # vector_keys
        ctypes.POINTER(Float3),  # vector_values
        ctypes.POINTER(ctypes.c_uint64),  # flag_keys
        ctypes.POINTER(ctypes.c_int32),  # flag_values
    ]
    lib.kex_doc_read_properties.restype = ctypes.c_int

    lib.kex_doc_read_keyframes.argtypes = [
        KexDoc,
        ctypes.POINTER(Keyframe),
        ctypes.POINTER(ctypes.c_uint64),
        ctypes.POINTER(ctypes.c_int32),
        ctypes.POINTER(ctypes.c_int32),
    ]
    lib.kex_doc_read_keyframes.restype = ctypes.c_int

    lib.kex_build.argtypes = [KexDoc, ctypes.c_float, ctypes.POINTER(ctypes.c_int32)]
    lib.kex_build.restype = KexOutput

    lib.kex_output_free.argtypes = [KexOutput]
    lib.kex_output_free.restype = None

    lib.kex_output_get_counts.argtypes = [KexOutput, ctypes.POINTER(KexOutputCounts)]
    lib.kex_output_get_counts.restype = ctypes.c_int

    lib.kex_output_read_points.argtypes = [
        KexOutput,
        ctypes.POINTER(Point),
        ctypes.c_size_t,
    ]
    lib.kex_output_read_points.restype = ctypes.c_int

    lib.kex_output_read_sections.argtypes = [
        KexOutput,
        ctypes.POINTER(Section),
        ctypes.POINTER(ctypes.c_uint32),
        ctypes.c_size_t,
    ]
    lib.kex_output_read_sections.restype = ctypes.c_int

    lib.kex_output_read_traversal.argtypes = [
        KexOutput,
        ctypes.POINTER(ctypes.c_int32),
        ctypes.c_size_t,
    ]
    lib.kex_output_read_traversal.restype = ctypes.c_int

    lib.kex_output_read_spline.argtypes = [
        KexOutput,
        ctypes.POINTER(SplinePoint),
        ctypes.POINTER(ctypes.c_float),
        ctypes.POINTER(ctypes.c_float),
        ctypes.POINTER(ctypes.c_float),
        ctypes.POINTER(ctypes.c_float),
        ctypes.c_size_t,
    ]
    lib.kex_output_read_spline.restype = ctypes.c_int

    return lib


_lib: Optional[ctypes.CDLL] = None


def get_library() -> ctypes.CDLL:
    global _lib
    if _lib is None:
        _lib = _load_library()
    if _lib is None:
        raise KexError(f"kexengine library not found at {_get_library_path()}")
    return _lib


def is_library_available() -> bool:
    global _lib
    if _lib is None:
        _lib = _load_library()
    return _lib is not None


# ---------------------------------------------------------------------------
# .kex serializer (mirrors packages/core/src/persistence/mod.rs)
# ---------------------------------------------------------------------------

_MAGIC = b"KEX\0"
_VERSION = 1
_TAG_GRPH = b"GRPH"
_TAG_DATA = b"DATA"


def _interp_to_byte(i: InterpolationType) -> int:
    return int(i)


def _serialize_document(
    *,
    node_ids: Sequence[int],
    node_types: Sequence[int],
    node_input_counts: Sequence[int],
    node_output_counts: Sequence[int],
    port_ids: Sequence[int],
    port_types: Sequence[int],
    port_owners: Sequence[int],
    port_is_input: Sequence[bool],
    edge_ids: Sequence[int],
    edge_sources: Sequence[int],
    edge_targets: Sequence[int],
    scalars: dict,
    vectors: dict,
    flags: dict,
    keyframes: Sequence[Keyframe],
    keyframe_ranges: dict,
    next_node_id: int,
    next_port_id: int,
    next_edge_id: int,
) -> bytes:
    grph = bytearray()
    grph.extend(struct.pack("<I", len(node_ids)))
    for i in range(len(node_ids)):
        grph.extend(struct.pack("<I", node_ids[i]))
        grph.append(node_types[i])
        grph.extend(struct.pack("<i", node_input_counts[i]))
        grph.extend(struct.pack("<i", node_output_counts[i]))

    grph.extend(struct.pack("<I", len(port_ids)))
    for i in range(len(port_ids)):
        grph.extend(struct.pack("<III", port_ids[i], port_types[i], port_owners[i]))
        grph.append(1 if port_is_input[i] else 0)

    grph.extend(struct.pack("<I", len(edge_ids)))
    for i in range(len(edge_ids)):
        grph.extend(struct.pack("<III", edge_ids[i], edge_sources[i], edge_targets[i]))

    grph.extend(struct.pack("<III", next_node_id, next_port_id, next_edge_id))

    data = bytearray()
    data.extend(struct.pack("<I", len(keyframes)))
    for kf in keyframes:
        data.extend(struct.pack("<ff", kf.time, kf.value))
        data.append(_interp_to_byte(kf.in_interpolation))
        data.append(_interp_to_byte(kf.out_interpolation))
        data.extend(
            struct.pack("<ffff", kf.in_tangent, kf.out_tangent, kf.in_weight, kf.out_weight)
        )

    data.extend(struct.pack("<I", len(keyframe_ranges)))
    for key, (start, length) in keyframe_ranges.items():
        data.extend(struct.pack("<QII", key, start, length))

    data.extend(struct.pack("<I", len(scalars)))
    for key, value in scalars.items():
        data.extend(struct.pack("<Qf", key, value))

    data.extend(struct.pack("<I", len(vectors)))
    for key, (x, y, z) in vectors.items():
        data.extend(struct.pack("<Qfff", key, x, y, z))

    data.extend(struct.pack("<I", len(flags)))
    for key, value in flags.items():
        data.extend(struct.pack("<Qi", key, value))

    out = bytearray()
    out.extend(_MAGIC)
    out.extend(struct.pack("<I", _VERSION))
    out.extend(_TAG_GRPH)
    out.extend(struct.pack("<I", len(grph)))
    out.extend(grph)
    out.extend(_TAG_DATA)
    out.extend(struct.pack("<I", len(data)))
    out.extend(data)
    return bytes(out)


# ---------------------------------------------------------------------------
# High-level wrapper
# ---------------------------------------------------------------------------


@dataclass
class BuildResult:
    points: list[Point] = field(default_factory=list)
    sections: list[Section] = field(default_factory=list)
    section_node_ids: list[int] = field(default_factory=list)
    traversal_order: list[int] = field(default_factory=list)
    spline_points: list[SplinePoint] = field(default_factory=list)
    spline_velocities: list[float] = field(default_factory=list)
    spline_normal_forces: list[float] = field(default_factory=list)
    spline_lateral_forces: list[float] = field(default_factory=list)
    spline_roll_speeds: list[float] = field(default_factory=list)


_BUILD_ERRORS = {
    -1: "null pointer",
    -3: "buffer overflow",
    -4: "cycle detected",
}


def _check(rc: int, fn: str) -> None:
    if rc != 0:
        raise KexError(f"{fn} failed: {_BUILD_ERRORS.get(rc, str(rc))}")


def _arr(ctype, n: int):
    return (ctype * max(n, 1))()


class KexEngine:
    """High-level wrapper. Accumulates document state in Python, serializes
    to `.kex` bytes, and uses the handle-based FFI to build tracks.
    """

    def __init__(self) -> None:
        self._next_node_id = 1
        self._next_port_id = 1
        self._next_edge_id = 1

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

        self._scalars: dict[int, float] = {}
        self._vectors: dict[int, tuple[float, float, float]] = {}
        self._flags: dict[int, int] = {}

        self._keyframes: list[Keyframe] = []
        self._keyframe_ranges: dict[int, tuple[int, int]] = {}

        self._node_anchor_output: dict[int, int] = {}
        self._node_path_output: dict[int, int] = {}

    # --- ID allocation ---

    def _alloc_node_id(self) -> int:
        i = self._next_node_id
        self._next_node_id += 1
        return i

    def _alloc_port_id(self) -> int:
        i = self._next_port_id
        self._next_port_id += 1
        return i

    def _alloc_edge_id(self) -> int:
        i = self._next_edge_id
        self._next_edge_id += 1
        return i

    def _add_node(self, node_type: NodeType, input_count: int, output_count: int) -> int:
        node_id = self._alloc_node_id()
        self._node_ids.append(node_id)
        self._node_types.append(int(node_type))
        self._node_input_counts.append(input_count)
        self._node_output_counts.append(output_count)
        return node_id

    def _add_port(
        self, owner: int, data_type: PortDataType, local_index: int, is_input: bool
    ) -> int:
        port_id = self._alloc_port_id()
        self._port_ids.append(port_id)
        self._port_types.append(port_spec(data_type, local_index))
        self._port_owners.append(owner)
        self._port_is_input.append(is_input)
        return port_id

    def _add_edge(self, source_port: int, target_port: int) -> int:
        edge_id = self._alloc_edge_id()
        self._edge_ids.append(edge_id)
        self._edge_sources.append(source_port)
        self._edge_targets.append(target_port)
        return edge_id

    def _set_scalar(self, node_id: int, slot: int, value: float) -> None:
        self._scalars[input_key(node_id, slot)] = value

    def _set_vector(
        self, node_id: int, slot: int, value: tuple[float, float, float]
    ) -> None:
        self._vectors[input_key(node_id, slot)] = value

    def _set_flag(self, node_id: int, slot: int, value: int) -> None:
        self._flags[input_key(node_id, slot)] = value

    # --- Node creation ---

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
        node_id = self._add_node(NodeType.ANCHOR, 8, 1)
        self._add_port(node_id, PortDataType.VECTOR, 0, True)
        for i in range(1, 8):
            self._add_port(node_id, PortDataType.SCALAR, i, True)
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        self._node_anchor_output[node_id] = anchor_out

        self._set_vector(node_id, 0, position)
        self._set_scalar(node_id, 1, roll)
        self._set_scalar(node_id, 2, pitch)
        self._set_scalar(node_id, 3, yaw)
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
        node_id = self._add_node(NodeType.FORCE, 2, 2)
        anchor_in = self._add_port(node_id, PortDataType.ANCHOR, 0, True)
        self._add_port(node_id, PortDataType.SCALAR, 1, True)
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        path_out = self._add_port(node_id, PortDataType.PATH, 0, False)
        self._node_anchor_output[node_id] = anchor_out
        self._node_path_output[node_id] = path_out

        if source_node in self._node_anchor_output:
            self._add_edge(self._node_anchor_output[source_node], anchor_in)

        self._set_scalar(node_id, NodeMeta.DURATION, duration)
        self._set_scalar(node_id, NodeMeta.PRIORITY, priority)
        self._set_flag(node_id, NodeMeta.RENDER, 0 if rendered else 1)
        return node_id

    def add_geometric(
        self,
        source_node: int,
        duration: float = 5.0,
        priority: float = 0.0,
        rendered: bool = True,
    ) -> int:
        node_id = self._add_node(NodeType.GEOMETRIC, 2, 2)
        anchor_in = self._add_port(node_id, PortDataType.ANCHOR, 0, True)
        self._add_port(node_id, PortDataType.SCALAR, 1, True)
        anchor_out = self._add_port(node_id, PortDataType.ANCHOR, 0, False)
        path_out = self._add_port(node_id, PortDataType.PATH, 0, False)
        self._node_anchor_output[node_id] = anchor_out
        self._node_path_output[node_id] = path_out

        if source_node in self._node_anchor_output:
            self._add_edge(self._node_anchor_output[source_node], anchor_in)

        self._set_scalar(node_id, NodeMeta.DURATION, duration)
        self._set_scalar(node_id, NodeMeta.PRIORITY, priority)
        self._set_flag(node_id, NodeMeta.RENDER, 0 if rendered else 1)
        return node_id

    def set_keyframes(
        self, node_id: int, property_id: int, keyframes: Sequence[Keyframe]
    ) -> None:
        if not keyframes:
            return
        key = input_key(node_id, property_id)
        start = len(self._keyframes)
        self._keyframes.extend(keyframes)
        self._keyframe_ranges[key] = (start, len(keyframes))

    # --- Build ---

    def to_bytes(self) -> bytes:
        """Serialize the engine state to `.kex` bytes."""
        return _serialize_document(
            node_ids=self._node_ids,
            node_types=self._node_types,
            node_input_counts=self._node_input_counts,
            node_output_counts=self._node_output_counts,
            port_ids=self._port_ids,
            port_types=self._port_types,
            port_owners=self._port_owners,
            port_is_input=self._port_is_input,
            edge_ids=self._edge_ids,
            edge_sources=self._edge_sources,
            edge_targets=self._edge_targets,
            scalars=self._scalars,
            vectors=self._vectors,
            flags=self._flags,
            keyframes=self._keyframes,
            keyframe_ranges=self._keyframe_ranges,
            next_node_id=self._next_node_id,
            next_port_id=self._next_port_id,
            next_edge_id=self._next_edge_id,
        )

    def build(self, resolution: float = 0.5) -> BuildResult:
        """Build the track. Raises KexError on failure."""
        return build_from_bytes(self.to_bytes(), resolution=resolution)

    def clear(self) -> None:
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

    @classmethod
    def from_bytes(cls, data: bytes) -> "KexEngine":
        """Load a KexEngine by reading a `.kex` byte buffer through FFI."""
        lib = get_library()
        data_arr = (ctypes.c_uint8 * len(data))(*data)
        handle = lib.kex_load(
            ctypes.cast(data_arr, ctypes.POINTER(ctypes.c_uint8)), len(data)
        )
        if not handle:
            raise KexError("kex_load failed — invalid format or empty buffer")

        try:
            counts = KexDocCounts()
            _check(lib.kex_doc_get_counts(handle, ctypes.byref(counts)), "kex_doc_get_counts")

            node_ids = _arr(ctypes.c_uint32, counts.node_count)
            node_types = _arr(ctypes.c_uint8, counts.node_count)
            node_input_counts = _arr(ctypes.c_int32, counts.node_count)
            node_output_counts = _arr(ctypes.c_int32, counts.node_count)
            port_ids = _arr(ctypes.c_uint32, counts.port_count)
            port_types = _arr(ctypes.c_uint32, counts.port_count)
            port_owners = _arr(ctypes.c_uint32, counts.port_count)
            port_is_input = _arr(ctypes.c_uint8, counts.port_count)
            edge_ids = _arr(ctypes.c_uint32, counts.edge_count)
            edge_sources = _arr(ctypes.c_uint32, counts.edge_count)
            edge_targets = _arr(ctypes.c_uint32, counts.edge_count)
            _check(
                lib.kex_doc_read_graph(
                    handle,
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
                ),
                "kex_doc_read_graph",
            )

            scalar_keys = _arr(ctypes.c_uint64, counts.scalar_count)
            scalar_values = _arr(ctypes.c_float, counts.scalar_count)
            vector_keys = _arr(ctypes.c_uint64, counts.vector_count)
            vector_values = _arr(Float3, counts.vector_count)
            flag_keys = _arr(ctypes.c_uint64, counts.flag_count)
            flag_values = _arr(ctypes.c_int32, counts.flag_count)
            _check(
                lib.kex_doc_read_properties(
                    handle,
                    scalar_keys,
                    scalar_values,
                    vector_keys,
                    vector_values,
                    flag_keys,
                    flag_values,
                ),
                "kex_doc_read_properties",
            )

            keyframes = _arr(Keyframe, counts.keyframe_count)
            range_keys = _arr(ctypes.c_uint64, counts.keyframe_range_count)
            range_starts = _arr(ctypes.c_int32, counts.keyframe_range_count)
            range_lengths = _arr(ctypes.c_int32, counts.keyframe_range_count)
            _check(
                lib.kex_doc_read_keyframes(
                    handle, keyframes, range_keys, range_starts, range_lengths
                ),
                "kex_doc_read_keyframes",
            )

            engine = cls()
            engine._next_node_id = counts.next_node_id
            engine._next_port_id = counts.next_port_id
            engine._next_edge_id = counts.next_edge_id

            engine._node_ids = list(node_ids)[: counts.node_count]
            engine._node_types = list(node_types)[: counts.node_count]
            engine._node_input_counts = list(node_input_counts)[: counts.node_count]
            engine._node_output_counts = list(node_output_counts)[: counts.node_count]

            engine._port_ids = list(port_ids)[: counts.port_count]
            engine._port_types = list(port_types)[: counts.port_count]
            engine._port_owners = list(port_owners)[: counts.port_count]
            engine._port_is_input = [bool(x) for x in port_is_input[: counts.port_count]]

            engine._edge_ids = list(edge_ids)[: counts.edge_count]
            engine._edge_sources = list(edge_sources)[: counts.edge_count]
            engine._edge_targets = list(edge_targets)[: counts.edge_count]

            engine._scalars = {
                int(scalar_keys[i]): float(scalar_values[i])
                for i in range(counts.scalar_count)
            }
            engine._vectors = {
                int(vector_keys[i]): vector_values[i].to_tuple()
                for i in range(counts.vector_count)
            }
            engine._flags = {
                int(flag_keys[i]): int(flag_values[i]) for i in range(counts.flag_count)
            }

            engine._keyframes = list(keyframes)[: counts.keyframe_count]
            engine._keyframe_ranges = {
                int(range_keys[i]): (int(range_starts[i]), int(range_lengths[i]))
                for i in range(counts.keyframe_range_count)
            }
            return engine
        finally:
            lib.kex_doc_free(handle)


def build_from_bytes(data: bytes, resolution: float = 0.5) -> BuildResult:
    """Load `.kex` bytes and build a track in one call. Raises KexError on failure."""
    lib = get_library()

    data_arr = (ctypes.c_uint8 * len(data))(*data)
    handle = lib.kex_load(ctypes.cast(data_arr, ctypes.POINTER(ctypes.c_uint8)), len(data))
    if not handle:
        raise KexError("kex_load failed — invalid format or empty buffer")

    try:
        err = ctypes.c_int32(0)
        out_handle = lib.kex_build(handle, ctypes.c_float(resolution), ctypes.byref(err))
        if not out_handle:
            raise KexError(
                f"kex_build failed: {_BUILD_ERRORS.get(err.value, str(err.value))}"
            )

        try:
            counts = KexOutputCounts()
            _check(lib.kex_output_get_counts(out_handle, ctypes.byref(counts)), "kex_output_get_counts")

            points = _arr(Point, counts.points_count)
            _check(
                lib.kex_output_read_points(out_handle, points, max(counts.points_count, 1)),
                "kex_output_read_points",
            )

            sections = _arr(Section, counts.sections_count)
            section_node_ids = _arr(ctypes.c_uint32, counts.sections_count)
            _check(
                lib.kex_output_read_sections(
                    out_handle, sections, section_node_ids, max(counts.sections_count, 1)
                ),
                "kex_output_read_sections",
            )

            traversal = _arr(ctypes.c_int32, counts.traversal_count)
            _check(
                lib.kex_output_read_traversal(
                    out_handle, traversal, max(counts.traversal_count, 1)
                ),
                "kex_output_read_traversal",
            )

            spline_points = _arr(SplinePoint, counts.spline_count)
            spline_vel = _arr(ctypes.c_float, counts.spline_count)
            spline_nf = _arr(ctypes.c_float, counts.spline_count)
            spline_lf = _arr(ctypes.c_float, counts.spline_count)
            spline_rs = _arr(ctypes.c_float, counts.spline_count)
            _check(
                lib.kex_output_read_spline(
                    out_handle,
                    spline_points,
                    spline_vel,
                    spline_nf,
                    spline_lf,
                    spline_rs,
                    max(counts.spline_count, 1),
                ),
                "kex_output_read_spline",
            )

            result = BuildResult()
            for i in range(counts.points_count):
                result.points.append(points[i])
            for i in range(counts.sections_count):
                result.sections.append(sections[i])
                result.section_node_ids.append(int(section_node_ids[i]))
            for i in range(counts.traversal_count):
                result.traversal_order.append(int(traversal[i]))
            for i in range(counts.spline_count):
                result.spline_points.append(spline_points[i])
                result.spline_velocities.append(float(spline_vel[i]))
                result.spline_normal_forces.append(float(spline_nf[i]))
                result.spline_lateral_forces.append(float(spline_lf[i]))
                result.spline_roll_speeds.append(float(spline_rs[i]))
            return result
        finally:
            lib.kex_output_free(out_handle)
    finally:
        lib.kex_doc_free(handle)
