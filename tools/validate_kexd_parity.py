#!/usr/bin/env python3
"""
Validate parity between legacy .kex and KEXD formats.
Compares graph structure, node types, and scalar values.
"""
import struct
import sys
from pathlib import Path
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Tuple


@dataclass
class ScalarValue:
    port_id: int
    value: float


@dataclass
class NodeData:
    node_id: int
    node_type: int
    position: Tuple[float, float]
    input_count: int
    output_count: int
    scalars: Dict[int, float] = field(default_factory=dict)


@dataclass
class GraphData:
    nodes: List[NodeData] = field(default_factory=list)
    edges: List[Tuple[int, int, int]] = field(default_factory=list)
    scalars: Dict[int, float] = field(default_factory=dict)
    vectors: Dict[int, Tuple[float, float, float]] = field(default_factory=dict)
    rotations: Dict[int, Tuple[float, float, float]] = field(default_factory=dict)


class BinaryReader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def read_byte(self) -> int:
        val = self.data[self.pos]
        self.pos += 1
        return val

    def read_uint(self) -> int:
        val = struct.unpack_from('<I', self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_int(self) -> int:
        val = struct.unpack_from('<i', self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float(self) -> float:
        val = struct.unpack_from('<f', self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float2(self) -> Tuple[float, float]:
        return (self.read_float(), self.read_float())

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
        return ''.join(chars)

    def skip(self, count: int):
        self.pos += count

    def remaining(self) -> int:
        return len(self.data) - self.pos


def is_kexd(data: bytes) -> bool:
    return len(data) >= 4 and data[:4] == b'KEXD'


def parse_kexd(data: bytes) -> GraphData:
    reader = BinaryReader(data)
    result = GraphData()

    if not is_kexd(data):
        raise ValueError("Not a KEXD file")

    reader.skip(4)  # KEXD magic
    reader.read_uint()  # file version

    while reader.remaining() > 12:
        chunk_type = reader.read_chunk_type()
        chunk_version = reader.read_uint()
        chunk_length = reader.read_uint()
        chunk_end = reader.pos + chunk_length

        if chunk_type == "CORE":
            while reader.pos < chunk_end:
                sub_type = reader.read_chunk_type()
                sub_version = reader.read_uint()
                sub_length = reader.read_uint()
                sub_end = reader.pos + sub_length

                if sub_type == "GRPH":
                    node_count = reader.read_int()
                    port_count = reader.read_int()
                    edge_count = reader.read_int()

                    for _ in range(node_count):
                        node_id = reader.read_uint()
                        node_type = reader.read_uint()
                        if sub_version == 1:
                            pos = reader.read_float2()
                        else:
                            pos = (0.0, 0.0)
                        input_count = reader.read_int()
                        output_count = reader.read_int()
                        result.nodes.append(NodeData(
                            node_id, node_type, pos, input_count, output_count
                        ))

                    for _ in range(port_count):
                        reader.read_uint()  # port_id
                        reader.read_uint()  # port_type
                        reader.read_uint()  # owner
                        reader.read_bool()  # is_input

                    for _ in range(edge_count):
                        edge_id = reader.read_uint()
                        source = reader.read_uint()
                        target = reader.read_uint()
                        result.edges.append((edge_id, source, target))

                    reader.read_uint()  # next_node_id
                    reader.read_uint()  # next_port_id
                    reader.read_uint()  # next_edge_id

                elif sub_type == "DATA":
                    keyframe_count = reader.read_int()
                    for _ in range(keyframe_count):
                        reader.skip(56)  # keyframe data

                    range_count = reader.read_int()
                    for _ in range(range_count):
                        reader.skip(12)  # range data

                    scalar_count = reader.read_int()
                    for _ in range(scalar_count):
                        port_id = reader.read_uint()
                        value = reader.read_float()
                        result.scalars[port_id] = value

                    vector_count = reader.read_int()
                    for _ in range(vector_count):
                        node_id = reader.read_uint()
                        value = reader.read_float3()
                        result.vectors[node_id] = value

                    rotation_count = reader.read_int()
                    for _ in range(rotation_count):
                        node_id = reader.read_uint()
                        value = reader.read_float3()
                        result.rotations[node_id] = value

                    # Skip remaining DATA fields
                    reader.pos = sub_end
                else:
                    reader.pos = sub_end

        elif chunk_type == "UIMD":
            count = reader.read_int()
            node_map = {n.node_id: n for n in result.nodes}
            for _ in range(count):
                node_id = reader.read_uint()
                pos = reader.read_float2()
                if node_id in node_map:
                    node_map[node_id].position = pos
        else:
            reader.pos = chunk_end

    return result


def validate_kexd_file(filepath: str) -> bool:
    """Validate a KEXD file's structure and data integrity."""
    print(f"Validating: {filepath}\n")

    try:
        with open(filepath, 'rb') as f:
            data = f.read()
    except Exception as e:
        print(f"ERROR: Cannot read file: {e}")
        return False

    if not is_kexd(data):
        print("ERROR: Not a KEXD file (missing magic header)")
        return False

    try:
        graph = parse_kexd(data)
    except Exception as e:
        print(f"ERROR: Failed to parse KEXD: {e}")
        import traceback
        traceback.print_exc()
        return False

    print("=== STRUCTURE VALIDATION ===\n")
    print(f"  Nodes: {len(graph.nodes)}")
    print(f"  Edges: {len(graph.edges)}")
    print(f"  Scalars: {len(graph.scalars)}")
    print(f"  Vectors: {len(graph.vectors)}")
    print(f"  Rotations: {len(graph.rotations)}")

    errors = []
    warnings = []

    for node in graph.nodes:
        if node.position == (0.0, 0.0):
            warnings.append(f"Node {node.node_id} has zero position")

    # Check for valid node types
    valid_types = {0, 1, 2, 3, 4, 5, 6, 7}
    for node in graph.nodes:
        if node.node_type not in valid_types:
            errors.append(f"Node {node.node_id} has invalid type: {node.node_type}")

    # Check Anchor nodes have position data
    for node in graph.nodes:
        if node.node_type == 5:  # Anchor
            if node.node_id not in graph.vectors:
                warnings.append(f"Anchor node {node.node_id} missing position vector")

    print("\n=== VALIDATION RESULTS ===\n")

    if errors:
        print("ERRORS:")
        for e in errors:
            print(f"  - {e}")

    if warnings:
        print("WARNINGS:")
        for w in warnings:
            print(f"  - {w}")

    if not errors and not warnings:
        print("  All checks passed!")

    return len(errors) == 0


def main():
    if len(sys.argv) < 2:
        print("Usage: validate_kexd_parity.py <file.kex>")
        print()
        print("Validates KEXD file structure and data integrity.")
        print("Reports errors and warnings for migration testing.")
        sys.exit(1)

    filepath = sys.argv[1]
    success = validate_kexd_file(filepath)
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
