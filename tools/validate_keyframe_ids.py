#!/usr/bin/env python3
"""
Validate keyframe serialization in KEXD files.

This script analyzes the DATA chunk to understand how keyframes are stored
and verify the format. Core Keyframe struct has no ID field - IDs are only
used in the Legacy/UI layer and must be generated during deserialization.

Usage:
    python validate_keyframe_ids.py <file.kex>
    python validate_keyframe_ids.py --create-test  # Create test file with keyframes
"""
import struct
import sys
from dataclasses import dataclass, field
from typing import List, Tuple, Optional
from enum import IntEnum


class InterpolationType(IntEnum):
    Constant = 0
    Linear = 1
    Bezier = 2


CHUNK_HEADER_SIZE = 12


@dataclass
class ChunkHeader:
    type: str
    version: int
    length: int


@dataclass
class CoreKeyframe:
    """Core Keyframe - physics only, NO ID field."""
    time: float
    value: float
    in_interpolation: InterpolationType
    out_interpolation: InterpolationType
    in_tangent: float
    out_tangent: float
    in_weight: float
    out_weight: float


@dataclass
class KeyframeRange:
    """Maps (nodeId, propertyId) -> (start, length) in keyframes array."""
    key: int  # (nodeId << 8) | propertyId
    start: int
    length: int


@dataclass
class DataChunkContent:
    """Content from DATA chunk."""
    keyframes: List[CoreKeyframe] = field(default_factory=list)
    ranges: List[KeyframeRange] = field(default_factory=list)
    scalars: dict = field(default_factory=dict)
    vectors: dict = field(default_factory=dict)
    flags: dict = field(default_factory=dict)


class KexdReader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def read_byte(self) -> int:
        val = self.data[self.pos]
        self.pos += 1
        return val

    def read_uint(self) -> int:
        val = struct.unpack_from("<I", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_ulong(self) -> int:
        val = struct.unpack_from("<Q", self.data, self.pos)[0]
        self.pos += 8
        return val

    def read_int(self) -> int:
        val = struct.unpack_from("<i", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float(self) -> float:
        val = struct.unpack_from("<f", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float3(self) -> Tuple[float, float, float]:
        return (self.read_float(), self.read_float(), self.read_float())

    def read_chunk_type(self) -> str:
        chars = []
        for _ in range(4):
            b = self.read_byte()
            if b != 0:
                chars.append(chr(b))
        return "".join(chars)

    def read_chunk_header(self) -> Optional[ChunkHeader]:
        if self.pos + CHUNK_HEADER_SIZE > len(self.data):
            return None
        chunk_type = self.read_chunk_type()
        version = self.read_uint()
        length = self.read_uint()
        return ChunkHeader(chunk_type, version, length)

    def skip(self, count: int):
        self.pos += count

    def remaining(self) -> int:
        return len(self.data) - self.pos


def read_file_header(reader: KexdReader) -> int:
    k = reader.read_byte()
    e = reader.read_byte()
    x = reader.read_byte()
    d = reader.read_byte()

    if chr(k) != "K" or chr(e) != "E" or chr(x) != "X" or chr(d) != "D":
        raise ValueError(f"Invalid magic: {chr(k)}{chr(e)}{chr(x)}{chr(d)}")

    return reader.read_uint()


def read_data_chunk(reader: KexdReader, header: ChunkHeader) -> DataChunkContent:
    """Parse DATA chunk - contains keyframes, scalars, vectors, flags."""
    result = DataChunkContent()
    chunk_end = reader.pos + header.length

    # Read keyframes (flat array)
    keyframe_count = reader.read_int()
    for _ in range(keyframe_count):
        kf = CoreKeyframe(
            time=reader.read_float(),
            value=reader.read_float(),
            in_interpolation=InterpolationType(reader.read_byte()),
            out_interpolation=InterpolationType(reader.read_byte()),
            in_tangent=reader.read_float(),
            out_tangent=reader.read_float(),
            in_weight=reader.read_float(),
            out_weight=reader.read_float(),
        )
        result.keyframes.append(kf)

    # Read range mappings
    range_count = reader.read_int()
    for _ in range(range_count):
        key = reader.read_ulong()
        start = reader.read_int()
        length = reader.read_int()
        result.ranges.append(KeyframeRange(key, start, length))

    # Read scalars (key -> float)
    scalar_count = reader.read_int()
    for _ in range(scalar_count):
        key = reader.read_ulong()
        value = reader.read_float()
        result.scalars[key] = value

    # Read vectors (key -> float3)
    vector_count = reader.read_int()
    for _ in range(vector_count):
        key = reader.read_ulong()
        value = reader.read_float3()
        result.vectors[key] = value

    # Read flags (key -> int)
    flag_count = reader.read_int()
    for _ in range(flag_count):
        key = reader.read_ulong()
        value = reader.read_int()
        result.flags[key] = value

    return result


def unpack_range_key(key: int) -> Tuple[int, int]:
    """Unpack composite key into (nodeId, propertyId)."""
    node_id = (key >> 8) & 0xFFFFFFFF
    property_id = key & 0xFF
    return node_id, property_id


def get_property_name(property_id: int) -> str:
    """Map PropertyId enum to name."""
    names = {
        0: "RollSpeed",
        1: "NormalForce",
        2: "LateralForce",
        3: "PitchSpeed",
        4: "YawSpeed",
        5: "DrivenVelocity",
        6: "HeartOffset",
        7: "Friction",
        8: "Resistance",
        9: "TrackStyle",
    }
    return names.get(property_id, f"Unknown({property_id})")


def analyze_keyframes(filepath: str):
    """Analyze keyframe data in a KEXD file."""
    with open(filepath, "rb") as f:
        data = f.read()

    reader = KexdReader(data)
    file_version = read_file_header(reader)

    print(f"Analyzing KEXD keyframes: {filepath}")
    print(f"File Version: {file_version}\n")

    data_content = None

    while reader.remaining() > 0:
        header = reader.read_chunk_header()
        if header is None:
            break

        chunk_end = reader.pos + header.length

        if header.type == "CORE":
            # Read sub-chunks within CORE
            while reader.pos < chunk_end:
                sub_header = reader.read_chunk_header()
                if sub_header is None:
                    break

                if sub_header.type == "DATA":
                    data_content = read_data_chunk(reader, sub_header)
                else:
                    reader.skip(sub_header.length)
        else:
            reader.skip(header.length)

    if data_content is None:
        print("No DATA chunk found!")
        return

    print("=== KEYFRAMES ===\n")
    print(f"Total keyframes: {len(data_content.keyframes)}")
    print(f"Range mappings: {len(data_content.ranges)}")

    if data_content.keyframes:
        print("\n--- Keyframe Data (note: NO ID field in Core Keyframe) ---")
        for i, kf in enumerate(data_content.keyframes):
            print(f"  [{i}] time={kf.time:.3f}, value={kf.value:.3f}, "
                  f"in={kf.in_interpolation.name}, out={kf.out_interpolation.name}")

    if data_content.ranges:
        print("\n--- Range Mappings ---")
        for r in data_content.ranges:
            node_id, prop_id = unpack_range_key(r.key)
            prop_name = get_property_name(prop_id)
            print(f"  Node {node_id} / {prop_name}: keyframes[{r.start}:{r.start + r.length}]")

    print("\n=== ANALYSIS ===\n")
    print("Core Keyframe struct does NOT contain an ID field.")
    print("IDs must be generated during deserialization (in KexdAdapter).")
    print("")
    print("BUG: KexdAdapter.CoreToLegacyKeyframe() sets Id=0 for ALL keyframes.")
    print("FIX: Call Uuid.Create() to generate unique IDs during conversion.")


def create_test_file(filepath: str):
    """Create a test KEXD file with keyframes for validation."""
    from io import BytesIO

    def write_uint(f, val):
        f.write(struct.pack("<I", val))

    def write_ulong(f, val):
        f.write(struct.pack("<Q", val))

    def write_int(f, val):
        f.write(struct.pack("<i", val))

    def write_float(f, val):
        f.write(struct.pack("<f", val))

    def write_byte(f, val):
        f.write(struct.pack("B", val))

    def write_float2(f, x, y):
        write_float(f, x)
        write_float(f, y)

    def write_bool(f, val):
        f.write(struct.pack("B", 1 if val else 0))

    def write_chunk_type(f, t):
        for c in t.ljust(4, "\0")[:4]:
            f.write(struct.pack("B", ord(c)))

    def begin_chunk(f, chunk_type, version):
        start = f.tell()
        write_chunk_type(f, chunk_type)
        write_uint(f, version)
        write_uint(f, 0)  # Placeholder
        return start

    def end_chunk(f, start):
        end = f.tell()
        content_len = end - start - CHUNK_HEADER_SIZE
        f.seek(start + 8)
        write_uint(f, content_len)
        f.seek(end)

    def write_keyframe(f, time, value, in_interp=2, out_interp=2,
                       in_tangent=0.0, out_tangent=0.0,
                       in_weight=0.333, out_weight=0.333):
        """Write a Core Keyframe (no ID)."""
        write_float(f, time)
        write_float(f, value)
        write_byte(f, in_interp)  # Bezier
        write_byte(f, out_interp)  # Bezier
        write_float(f, in_tangent)
        write_float(f, out_tangent)
        write_float(f, in_weight)
        write_float(f, out_weight)

    buf = BytesIO()

    # File header
    buf.write(b"KEXD")
    write_uint(buf, 1)

    # CORE chunk
    core_start = begin_chunk(buf, "CORE", 1)

    # GRPH sub-chunk
    grph_start = begin_chunk(buf, "GRPH", 2)

    # 2 nodes: Anchor (id=1) and Force (id=2)
    write_int(buf, 2)  # node count
    write_int(buf, 4)  # port count
    write_int(buf, 1)  # edge count

    # Node 1: Anchor
    write_uint(buf, 1)  # id
    write_uint(buf, 5)  # type (Anchor)
    write_int(buf, 8)   # input count (Anchor has 8 inputs)
    write_int(buf, 1)   # output count

    # Node 2: Force
    write_uint(buf, 2)  # id
    write_uint(buf, 0)  # type (Force)
    write_int(buf, 2)   # input count (Anchor + Duration)
    write_int(buf, 2)   # output count (Anchor + Path)

    # Ports for Anchor node (simplified - just output)
    write_uint(buf, 1)  # id
    write_uint(buf, 0)  # type
    write_uint(buf, 1)  # owner
    write_bool(buf, False)  # is_input

    # Ports for Force node
    write_uint(buf, 2)  # input - Anchor
    write_uint(buf, 0)
    write_uint(buf, 2)
    write_bool(buf, True)

    write_uint(buf, 3)  # input - Duration
    write_uint(buf, 1)
    write_uint(buf, 2)
    write_bool(buf, True)

    write_uint(buf, 4)  # output - Anchor
    write_uint(buf, 0)
    write_uint(buf, 2)
    write_bool(buf, False)

    # Edge: Anchor output -> Force input
    write_uint(buf, 1)  # edge id
    write_uint(buf, 1)  # source port
    write_uint(buf, 2)  # target port

    write_uint(buf, 3)  # next_node_id
    write_uint(buf, 5)  # next_port_id
    write_uint(buf, 2)  # next_edge_id

    end_chunk(buf, grph_start)

    # DATA sub-chunk with 2 NormalForce keyframes for node 2
    data_start = begin_chunk(buf, "DATA", 2)

    # Write keyframes (2 keyframes for NormalForce)
    write_int(buf, 2)  # keyframe count

    # Keyframe 1: time=0.0, value=1.0
    write_keyframe(buf, time=0.0, value=1.0)

    # Keyframe 2: time=1.0, value=2.0
    write_keyframe(buf, time=1.0, value=2.0)

    # Write range mappings
    write_int(buf, 1)  # range count
    # Key for node 2, PropertyId.NormalForce (1)
    key = (2 << 8) | 1  # nodeId=2, propertyId=1
    write_ulong(buf, key)
    write_int(buf, 0)  # start
    write_int(buf, 2)  # length

    # Scalars, vectors, flags (empty for simplicity)
    write_int(buf, 0)  # scalars count
    write_int(buf, 0)  # vectors count
    write_int(buf, 0)  # flags count

    end_chunk(buf, data_start)

    end_chunk(buf, core_start)

    # UIST chunk (unified UI state)
    uist_start = begin_chunk(buf, "UIST", 1)

    # Node positions
    write_int(buf, 2)  # count
    write_uint(buf, 1)  # node id
    write_float2(buf, 100.0, 50.0)
    write_uint(buf, 2)  # node id
    write_float2(buf, 300.0, 50.0)

    # View state (defaults)
    write_float(buf, 0.0)   # timeline_offset
    write_float(buf, 1.0)   # timeline_zoom
    write_float(buf, 0.0)   # graph_pan_x
    write_float(buf, 0.0)   # graph_pan_y
    write_float(buf, 1.0)   # graph_zoom
    write_float(buf, 0.0)   # camera_position x
    write_float(buf, 0.0)   # camera_position y
    write_float(buf, 0.0)   # camera_position z
    write_float(buf, 0.0)   # camera_target_position x
    write_float(buf, 0.0)   # camera_target_position y
    write_float(buf, 0.0)   # camera_target_position z
    write_float(buf, 50.0)  # camera_distance
    write_float(buf, 50.0)  # camera_target_distance
    write_float(buf, 30.0)  # camera_pitch
    write_float(buf, 30.0)  # camera_target_pitch
    write_float(buf, 0.0)   # camera_yaw
    write_float(buf, 0.0)   # camera_target_yaw
    write_float(buf, 1.0)   # camera_speed_multiplier

    # Keyframe states (empty)
    write_int(buf, 0)

    end_chunk(buf, uist_start)

    with open(filepath, "wb") as f:
        f.write(buf.getvalue())

    print(f"Created test file: {filepath}")
    print("Contains: Anchor + Force node with 2 NormalForce keyframes")
    print("This file can be used to test keyframe ID generation during deserialization.")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: validate_keyframe_ids.py <file.kex>")
        print("       validate_keyframe_ids.py --create-test")
        sys.exit(1)

    if sys.argv[1] == "--create-test":
        create_test_file("test_keyframes.kex")
    else:
        analyze_keyframes(sys.argv[1])
