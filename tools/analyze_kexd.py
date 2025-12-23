#!/usr/bin/env python3
"""
Analyze .kex files in the new KEXD chunk format.
Validates chunk structure and extension data for migration testing.
"""
import struct
import sys
from pathlib import Path
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Tuple
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


CHUNK_HEADER_SIZE = 12  # 4 (type) + 4 (version) + 4 (length)


@dataclass
class ChunkHeader:
    type: str
    version: int
    length: int


@dataclass
class NodeUIMetadata:
    node_id: int
    position: Tuple[float, float]


@dataclass
class GraphData:
    node_ids: List[int] = field(default_factory=list)
    node_types: List[int] = field(default_factory=list)
    node_positions: List[Tuple[float, float]] = field(default_factory=list)
    node_input_counts: List[int] = field(default_factory=list)
    node_output_counts: List[int] = field(default_factory=list)
    port_ids: List[int] = field(default_factory=list)
    port_types: List[int] = field(default_factory=list)
    port_owners: List[int] = field(default_factory=list)
    port_is_input: List[bool] = field(default_factory=list)
    edge_ids: List[int] = field(default_factory=list)
    edge_sources: List[int] = field(default_factory=list)
    edge_targets: List[int] = field(default_factory=list)
    next_node_id: int = 0
    next_port_id: int = 0
    next_edge_id: int = 0


@dataclass
class KexdFile:
    file_version: int = 0
    core_version: int = 0
    graph_version: int = 0
    data_version: int = 0
    graph: GraphData = field(default_factory=GraphData)
    ui_metadata: List[NodeUIMetadata] = field(default_factory=list)
    unknown_chunks: List[str] = field(default_factory=list)


class KexdReader:
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

    def read_bool(self) -> bool:
        return self.read_byte() != 0

    def read_chunk_type(self) -> str:
        chars = []
        for _ in range(4):
            b = self.read_byte()
            if b != 0:
                chars.append(chr(b))
        return ''.join(chars)

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

    if chr(k) != 'K' or chr(e) != 'E' or chr(x) != 'X' or chr(d) != 'D':
        raise ValueError(f"Invalid magic: {chr(k)}{chr(e)}{chr(x)}{chr(d)}")

    return reader.read_uint()


def read_graph_chunk(reader: KexdReader, header: ChunkHeader) -> GraphData:
    graph = GraphData()

    node_count = reader.read_int()
    port_count = reader.read_int()
    edge_count = reader.read_int()

    for _ in range(node_count):
        node_id = reader.read_uint()
        node_type = reader.read_uint()

        if header.version == 1:
            position = reader.read_float2()
        else:
            position = (0.0, 0.0)

        input_count = reader.read_int()
        output_count = reader.read_int()

        graph.node_ids.append(node_id)
        graph.node_types.append(node_type)
        graph.node_positions.append(position)
        graph.node_input_counts.append(input_count)
        graph.node_output_counts.append(output_count)

    for _ in range(port_count):
        port_id = reader.read_uint()
        port_type = reader.read_uint()
        port_owner = reader.read_uint()
        is_input = reader.read_bool()

        graph.port_ids.append(port_id)
        graph.port_types.append(port_type)
        graph.port_owners.append(port_owner)
        graph.port_is_input.append(is_input)

    for _ in range(edge_count):
        edge_id = reader.read_uint()
        source = reader.read_uint()
        target = reader.read_uint()

        graph.edge_ids.append(edge_id)
        graph.edge_sources.append(source)
        graph.edge_targets.append(target)

    graph.next_node_id = reader.read_uint()
    graph.next_port_id = reader.read_uint()
    graph.next_edge_id = reader.read_uint()

    return graph


def read_data_chunk(reader: KexdReader, header: ChunkHeader):
    # Skip DATA chunk for now - just need structure validation
    pass


def read_uimd_chunk(reader: KexdReader, header: ChunkHeader) -> List[NodeUIMetadata]:
    metadata = []
    count = reader.read_int()

    for _ in range(count):
        node_id = reader.read_uint()
        position = reader.read_float2()
        metadata.append(NodeUIMetadata(node_id, position))

    return metadata


def parse_kexd(filepath: str) -> KexdFile:
    with open(filepath, 'rb') as f:
        data = f.read()

    reader = KexdReader(data)
    result = KexdFile()

    result.file_version = read_file_header(reader)

    while reader.remaining() > 0:
        header = reader.read_chunk_header()
        if header is None:
            break

        chunk_end = reader.pos + header.length

        if header.type == "CORE":
            result.core_version = header.version
            # Read sub-chunks within CORE
            while reader.pos < chunk_end:
                sub_header = reader.read_chunk_header()
                if sub_header is None:
                    break

                if sub_header.type == "GRPH":
                    result.graph_version = sub_header.version
                    result.graph = read_graph_chunk(reader, sub_header)
                elif sub_header.type == "DATA":
                    result.data_version = sub_header.version
                    reader.skip(sub_header.length)
                else:
                    reader.skip(sub_header.length)

        elif header.type == "UIMD":
            result.ui_metadata = read_uimd_chunk(reader, header)

        else:
            result.unknown_chunks.append(header.type)
            reader.skip(header.length)

    return result


def get_node_type_name(t: int) -> str:
    try:
        return NodeType(t).name
    except ValueError:
        return f"Unknown({t})"


def analyze(filepath: str):
    print(f"Analyzing KEXD: {filepath}\n")

    try:
        kexd = parse_kexd(filepath)
    except Exception as e:
        print(f"Error parsing file: {e}")
        import traceback
        traceback.print_exc()
        return

    print("=== FILE STRUCTURE ===\n")
    print(f"File Version: {kexd.file_version}")
    print(f"Core Version: {kexd.core_version}")
    print(f"Graph Version: {kexd.graph_version}")
    print(f"Data Version: {kexd.data_version}")

    if kexd.unknown_chunks:
        print(f"Unknown Chunks: {kexd.unknown_chunks}")

    print(f"\n=== GRAPH ===\n")
    print(f"Nodes: {len(kexd.graph.node_ids)}")
    print(f"Ports: {len(kexd.graph.port_ids)}")
    print(f"Edges: {len(kexd.graph.edge_ids)}")
    print(f"Next IDs: node={kexd.graph.next_node_id}, port={kexd.graph.next_port_id}, edge={kexd.graph.next_edge_id}")

    print(f"\n=== NODES ===\n")
    for i, (nid, ntype, pos, inp, outp) in enumerate(zip(
        kexd.graph.node_ids,
        kexd.graph.node_types,
        kexd.graph.node_positions,
        kexd.graph.node_input_counts,
        kexd.graph.node_output_counts
    )):
        pos_str = f"({pos[0]:.1f}, {pos[1]:.1f})" if pos != (0.0, 0.0) else "(from UIMD)"
        print(f"  [{i}] Node {nid}: {get_node_type_name(ntype)} @ {pos_str} (in={inp}, out={outp})")

    if kexd.ui_metadata:
        print(f"\n=== UI METADATA (UIMD) ===\n")
        print(f"Entries: {len(kexd.ui_metadata)}")
        for meta in kexd.ui_metadata:
            print(f"  Node {meta.node_id}: ({meta.position[0]:.1f}, {meta.position[1]:.1f})")

        # Validate: check if all nodes have UI metadata
        node_ids_set = set(kexd.graph.node_ids)
        ui_node_ids = set(m.node_id for m in kexd.ui_metadata)
        missing = node_ids_set - ui_node_ids
        extra = ui_node_ids - node_ids_set

        if missing:
            print(f"\n  WARNING: Nodes without UI metadata: {missing}")
        if extra:
            print(f"\n  WARNING: UI metadata for non-existent nodes: {extra}")


def create_test_file(filepath: str, include_extension: bool = True):
    """Create a test KEXD file for validation."""
    from io import BytesIO

    def write_uint(f, val):
        f.write(struct.pack('<I', val))

    def write_int(f, val):
        f.write(struct.pack('<i', val))

    def write_float(f, val):
        f.write(struct.pack('<f', val))

    def write_float2(f, x, y):
        write_float(f, x)
        write_float(f, y)

    def write_bool(f, val):
        f.write(struct.pack('B', 1 if val else 0))

    def write_chunk_type(f, t):
        for c in t.ljust(4, '\0')[:4]:
            f.write(struct.pack('B', ord(c)))

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

    buf = BytesIO()

    # File header
    buf.write(b'KEXD')
    write_uint(buf, 1)

    # CORE chunk
    core_start = begin_chunk(buf, 'CORE', 1)

    # GRPH sub-chunk (version 2 - no positions in graph)
    grph_start = begin_chunk(buf, 'GRPH', 2)

    # 2 nodes
    write_int(buf, 2)  # node count
    write_int(buf, 2)  # port count
    write_int(buf, 1)  # edge count

    # Node 1: Anchor
    write_uint(buf, 1)  # id
    write_uint(buf, 5)  # type (Anchor)
    write_int(buf, 0)   # input count
    write_int(buf, 1)   # output count

    # Node 2: Force
    write_uint(buf, 2)  # id
    write_uint(buf, 0)  # type (Force)
    write_int(buf, 1)   # input count
    write_int(buf, 1)   # output count

    # Port 1: output from Anchor
    write_uint(buf, 1)  # id
    write_uint(buf, 0)  # type (Anchor port)
    write_uint(buf, 1)  # owner (node 1)
    write_bool(buf, False)  # is_input

    # Port 2: input to Force
    write_uint(buf, 2)  # id
    write_uint(buf, 0)  # type (Anchor port)
    write_uint(buf, 2)  # owner (node 2)
    write_bool(buf, True)  # is_input

    # Edge
    write_uint(buf, 1)  # id
    write_uint(buf, 1)  # source (port 1)
    write_uint(buf, 2)  # target (port 2)

    write_uint(buf, 3)  # next_node_id
    write_uint(buf, 3)  # next_port_id
    write_uint(buf, 2)  # next_edge_id

    end_chunk(buf, grph_start)

    # DATA sub-chunk (minimal)
    data_start = begin_chunk(buf, 'DATA', 1)
    write_int(buf, 0)  # keyframe count
    write_int(buf, 0)  # range count
    write_int(buf, 0)  # scalars count
    write_int(buf, 0)  # vectors count
    write_int(buf, 0)  # rotations count
    write_int(buf, 0)  # durations count
    write_int(buf, 0)  # steering count
    write_int(buf, 0)  # driven count
    end_chunk(buf, data_start)

    end_chunk(buf, core_start)

    # UIMD extension chunk
    if include_extension:
        uimd_start = begin_chunk(buf, 'UIMD', 1)
        write_int(buf, 2)  # count

        write_uint(buf, 1)  # node id
        write_float2(buf, 100.0, 50.0)  # position

        write_uint(buf, 2)  # node id
        write_float2(buf, 300.0, 50.0)  # position

        end_chunk(buf, uimd_start)

    with open(filepath, 'wb') as f:
        f.write(buf.getvalue())

    print(f"Created test file: {filepath}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: analyze_kexd.py <file.kex> [--create-test]")
        print()
        print("Options:")
        print("  --create-test    Create a test KEXD file")
        print("  --create-test-no-ext  Create test file without extension")
        sys.exit(1)

    if sys.argv[1] == "--create-test":
        create_test_file("test_kexd.kex", include_extension=True)
    elif sys.argv[1] == "--create-test-no-ext":
        create_test_file("test_kexd_no_ext.kex", include_extension=False)
    else:
        analyze(sys.argv[1])
