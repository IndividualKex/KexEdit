#!/usr/bin/env python3
"""
Validate property override flags in KEXD files.
Checks that PropertyOverrides (Heart, Friction, Resistance, TrackStyle) and Driven
(FixedVelocity) flags are correctly stored in Coaster.Flags.

NodeMeta indices (from Coaster.cs):
- 240: OverrideHeart
- 241: OverrideFriction
- 242: OverrideResistance
- 243: OverrideTrackStyle
- 253: Driven (for FixedVelocity)
"""
import struct
import sys
from dataclasses import dataclass, field
from typing import List, Dict, Tuple
from enum import IntEnum


class NodeType(IntEnum):
    Force = 0
    Geometric = 1
    Curved = 2
    CopyPath = 3
    Bridge = 4
    Anchor = 5
    Reverse = 6
    ReversePath = 7


class NodeMeta(IntEnum):
    OverrideHeart = 240
    OverrideFriction = 241
    OverrideResistance = 242
    OverrideTrackStyle = 243
    Duration = 248
    Priority = 249
    DurationType = 250
    Facing = 251
    Steering = 252
    Driven = 253  # Used for FixedVelocity
    Render = 254


class PropertyId(IntEnum):
    RollSpeed = 0
    NormalForce = 1
    LateralForce = 2
    PitchSpeed = 3
    YawSpeed = 4
    DrivenVelocity = 5
    HeartOffset = 6
    Friction = 7
    Resistance = 8
    TrackStyle = 9


CHUNK_HEADER_SIZE = 12  # 4 (type) + 4 (version) + 4 (length)


@dataclass
class NodeInfo:
    node_id: int
    node_type: NodeType


@dataclass
class PropertyOverrides:
    fixed_velocity: bool = False
    heart: bool = False
    friction: bool = False
    resistance: bool = False
    track_style: bool = False


@dataclass
class KexdData:
    nodes: List[NodeInfo] = field(default_factory=list)
    scalars: Dict[int, float] = field(default_factory=dict)
    vectors: Dict[int, Tuple[float, float, float]] = field(default_factory=dict)
    flags: Dict[int, int] = field(default_factory=dict)
    keyframe_ranges: Dict[int, Tuple[int, int]] = field(default_factory=dict)


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

    def read_int(self) -> int:
        val = struct.unpack_from("<i", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_ulong(self) -> int:
        val = struct.unpack_from("<Q", self.data, self.pos)[0]
        self.pos += 8
        return val

    def read_float(self) -> float:
        val = struct.unpack_from("<f", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float3(self) -> Tuple[float, float, float]:
        return (self.read_float(), self.read_float(), self.read_float())

    def read_bool(self) -> bool:
        return self.read_byte() != 0

    def read_chunk_type(self) -> str:
        chars = []
        for _ in range(4):
            b = self.read_byte()
            if b != 0:
                chars.append(chr(b))
        return "".join(chars)

    def skip(self, count: int):
        self.pos += count

    def remaining(self) -> int:
        return len(self.data) - self.pos


def input_key(node_id: int, meta_index: int) -> int:
    """Compute the key for a node's meta input."""
    return (node_id << 8) | (meta_index & 0xFF)


def unpack_input_key(key: int) -> Tuple[int, int]:
    """Unpack a key into (node_id, meta_index)."""
    node_id = key >> 8
    meta_index = key & 0xFF
    return node_id, meta_index


def keyframe_key(node_id: int, property_id: int) -> int:
    """Compute the key for a node's keyframe range."""
    return (node_id << 8) | (property_id & 0xFF)


def parse_kexd(filepath: str) -> KexdData:
    with open(filepath, "rb") as f:
        data = f.read()

    reader = KexdReader(data)
    result = KexdData()

    # Read file header
    magic = "".join([chr(reader.read_byte()) for _ in range(4)])
    if magic != "KEXD":
        raise ValueError(f"Invalid magic: {magic}")

    _file_version = reader.read_uint()

    while reader.remaining() > 0:
        chunk_type = reader.read_chunk_type()
        _version = reader.read_uint()
        length = reader.read_uint()
        chunk_end = reader.pos + length

        if chunk_type == "CORE":
            while reader.pos < chunk_end:
                sub_type = reader.read_chunk_type()
                sub_version = reader.read_uint()
                sub_length = reader.read_uint()
                sub_end = reader.pos + sub_length

                if sub_type == "GRPH":
                    # Read graph
                    node_count = reader.read_int()
                    port_count = reader.read_int()
                    edge_count = reader.read_int()

                    for _ in range(node_count):
                        node_id = reader.read_uint()
                        node_type = reader.read_uint()
                        # Version 2 has no positions in graph
                        if sub_version == 1:
                            reader.skip(8)  # position
                        _input_count = reader.read_int()
                        _output_count = reader.read_int()
                        result.nodes.append(NodeInfo(node_id, NodeType(node_type)))

                    # Skip ports
                    for _ in range(port_count):
                        reader.skip(4 + 4 + 4 + 1)  # id, type, owner, is_input

                    # Skip edges
                    for _ in range(edge_count):
                        reader.skip(4 + 4 + 4)  # id, source, target

                    # Skip next IDs
                    reader.skip(4 + 4 + 4)

                elif sub_type == "DATA":
                    # Read keyframes
                    kf_count = reader.read_int()
                    for _ in range(kf_count):
                        reader.skip(
                            4 + 4 + 1 + 1 + 4 + 4 + 4 + 4
                        )  # time, value, in_interp, out_interp, tangents, weights

                    # Read ranges
                    range_count = reader.read_int()
                    for _ in range(range_count):
                        key = reader.read_ulong()
                        start = reader.read_int()
                        length = reader.read_int()
                        result.keyframe_ranges[key] = (start, length)

                    # Read scalars
                    scalar_count = reader.read_int()
                    for _ in range(scalar_count):
                        key = reader.read_ulong()
                        value = reader.read_float()
                        result.scalars[key] = value

                    # Read vectors
                    vector_count = reader.read_int()
                    for _ in range(vector_count):
                        key = reader.read_ulong()
                        value = reader.read_float3()
                        result.vectors[key] = value

                    # Read flags
                    flag_count = reader.read_int()
                    for _ in range(flag_count):
                        key = reader.read_ulong()
                        value = reader.read_int()
                        result.flags[key] = value

                else:
                    reader.pos = sub_end

        else:
            reader.pos = chunk_end

    return result


def get_property_overrides(kexd: KexdData, node_id: int) -> PropertyOverrides:
    """Extract PropertyOverrides for a node from Coaster.Flags."""
    overrides = PropertyOverrides()

    driven_key = input_key(node_id, NodeMeta.Driven)
    if kexd.flags.get(driven_key, 0) == 1:
        overrides.fixed_velocity = True

    heart_key = input_key(node_id, NodeMeta.OverrideHeart)
    if kexd.flags.get(heart_key, 0) == 1:
        overrides.heart = True

    friction_key = input_key(node_id, NodeMeta.OverrideFriction)
    if kexd.flags.get(friction_key, 0) == 1:
        overrides.friction = True

    resistance_key = input_key(node_id, NodeMeta.OverrideResistance)
    if kexd.flags.get(resistance_key, 0) == 1:
        overrides.resistance = True

    track_style_key = input_key(node_id, NodeMeta.OverrideTrackStyle)
    if kexd.flags.get(track_style_key, 0) == 1:
        overrides.track_style = True

    return overrides


def has_keyframes(kexd: KexdData, node_id: int, property_id: PropertyId) -> bool:
    """Check if a node has keyframes for a specific property."""
    key = keyframe_key(node_id, property_id)
    return key in kexd.keyframe_ranges


def analyze(filepath: str):
    print(f"Analyzing Property Overrides: {filepath}\n")

    try:
        kexd = parse_kexd(filepath)
    except Exception as e:
        print(f"Error parsing file: {e}")
        import traceback

        traceback.print_exc()
        return

    print(f"=== NODES ({len(kexd.nodes)}) ===\n")

    for node in kexd.nodes:
        # Only check section nodes (Force, Geometric, Curved, CopyPath, Bridge)
        if node.node_type not in [
            NodeType.Force,
            NodeType.Geometric,
            NodeType.Curved,
            NodeType.CopyPath,
            NodeType.Bridge,
        ]:
            continue

        overrides = get_property_overrides(kexd, node.node_id)

        print(f"Node {node.node_id} ({node.node_type.name}):")

        # Check FixedVelocity
        has_fv_kf = has_keyframes(kexd, node.node_id, PropertyId.DrivenVelocity)
        print(
            f"  FixedVelocity: override={overrides.fixed_velocity}, has_keyframes={has_fv_kf}"
        )
        if has_fv_kf and not overrides.fixed_velocity:
            print(
                "    WARNING: Has keyframes but Driven flag not set - velocity override won't work!"
            )

        # Check Heart
        has_heart_kf = has_keyframes(kexd, node.node_id, PropertyId.HeartOffset)
        print(f"  Heart: override={overrides.heart}, has_keyframes={has_heart_kf}")

        # Check Friction
        has_friction_kf = has_keyframes(kexd, node.node_id, PropertyId.Friction)
        print(
            f"  Friction: override={overrides.friction}, has_keyframes={has_friction_kf}"
        )

        # Check Resistance
        has_resistance_kf = has_keyframes(kexd, node.node_id, PropertyId.Resistance)
        print(
            f"  Resistance: override={overrides.resistance}, has_keyframes={has_resistance_kf}"
        )

        # Check TrackStyle
        has_style_kf = has_keyframes(kexd, node.node_id, PropertyId.TrackStyle)
        print(
            f"  TrackStyle: override={overrides.track_style}, has_keyframes={has_style_kf}"
        )

        print()

    print("=== ALL FLAGS ===\n")
    for key, value in sorted(kexd.flags.items()):
        node_id, meta = unpack_input_key(key)
        try:
            meta_name = NodeMeta(meta).name
        except ValueError:
            meta_name = f"Unknown({meta})"
        print(f"  Node {node_id}.{meta_name} = {value}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: validate_property_overrides.py <file.kex>")
        print()
        print(
            "Validates that property override flags are correctly stored in KEXD files."
        )
        print(
            "This helps diagnose issues where property overrides are lost on save/load."
        )
        sys.exit(1)

    analyze(sys.argv[1])
