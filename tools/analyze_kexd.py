#!/usr/bin/env python3
"""
Analyze .kex files in the KEXD chunk format.
Validates chunk structure and extension data for migration testing.
"""
import struct
import sys
from dataclasses import dataclass, field
from typing import List, Optional, Tuple
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
class KeyframeUIState:
    node_id: int
    property_id: int
    keyframe_index: int
    id: int
    handle_type: int
    flags: int


@dataclass
class UIStateData:
    node_positions: dict = field(default_factory=dict)
    timeline_offset: float = 0.0
    timeline_zoom: float = 1.0
    graph_pan_x: float = 0.0
    graph_pan_y: float = 0.0
    graph_zoom: float = 1.0
    camera_position: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    camera_target_position: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    camera_distance: float = 50.0
    camera_target_distance: float = 50.0
    camera_pitch: float = 30.0
    camera_target_pitch: float = 30.0
    camera_yaw: float = 0.0
    camera_target_yaw: float = 0.0
    camera_speed_multiplier: float = 1.0
    keyframe_states: List[KeyframeUIState] = field(default_factory=list)


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
    ui_state: Optional[UIStateData] = None
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
        val = struct.unpack_from("<I", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_int(self) -> int:
        val = struct.unpack_from("<i", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float(self) -> float:
        val = struct.unpack_from("<f", self.data, self.pos)[0]
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


def read_uist_chunk(reader: KexdReader, header: ChunkHeader) -> UIStateData:
    ui_state = UIStateData()

    position_count = reader.read_int()
    for _ in range(position_count):
        node_id = reader.read_uint()
        position = reader.read_float2()
        ui_state.node_positions[node_id] = position

    ui_state.timeline_offset = reader.read_float()
    ui_state.timeline_zoom = reader.read_float()
    ui_state.graph_pan_x = reader.read_float()
    ui_state.graph_pan_y = reader.read_float()
    ui_state.graph_zoom = reader.read_float()
    ui_state.camera_position = reader.read_float3()
    ui_state.camera_target_position = reader.read_float3()
    ui_state.camera_distance = reader.read_float()
    ui_state.camera_target_distance = reader.read_float()
    ui_state.camera_pitch = reader.read_float()
    ui_state.camera_target_pitch = reader.read_float()
    ui_state.camera_yaw = reader.read_float()
    ui_state.camera_target_yaw = reader.read_float()
    ui_state.camera_speed_multiplier = reader.read_float()

    keyframe_count = reader.read_int()
    for _ in range(keyframe_count):
        state = KeyframeUIState(
            node_id=reader.read_uint(),
            property_id=reader.read_byte(),
            keyframe_index=reader.read_int(),
            id=reader.read_uint(),
            handle_type=reader.read_byte(),
            flags=reader.read_byte(),
        )
        ui_state.keyframe_states.append(state)

    return ui_state


def parse_kexd(filepath: str) -> KexdFile:
    with open(filepath, "rb") as f:
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

        elif header.type == "UIST":
            result.ui_state = read_uist_chunk(reader, header)

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

    print("\n=== GRAPH ===\n")
    print(f"Nodes: {len(kexd.graph.node_ids)}")
    print(f"Ports: {len(kexd.graph.port_ids)}")
    print(f"Edges: {len(kexd.graph.edge_ids)}")
    print(
        f"Next IDs: node={kexd.graph.next_node_id}, port={kexd.graph.next_port_id}, edge={kexd.graph.next_edge_id}"
    )

    print("\n=== NODES ===\n")
    for i, (nid, ntype, pos, inp, outp) in enumerate(
        zip(
            kexd.graph.node_ids,
            kexd.graph.node_types,
            kexd.graph.node_positions,
            kexd.graph.node_input_counts,
            kexd.graph.node_output_counts,
        )
    ):
        pos_str = (
            f"({pos[0]:.1f}, {pos[1]:.1f})" if pos != (0.0, 0.0) else "(from UIST)"
        )
        print(
            f"  [{i}] Node {nid}: {get_node_type_name(ntype)} @ {pos_str} (in={inp}, out={outp})"
        )

    if kexd.ui_state:
        ui = kexd.ui_state
        print("\n=== UI STATE (UIST) ===\n")

        print(f"Node Positions: {len(ui.node_positions)}")
        for node_id, pos in ui.node_positions.items():
            print(f"  Node {node_id}: ({pos[0]:.1f}, {pos[1]:.1f})")

        node_ids_set = set(kexd.graph.node_ids)
        ui_node_ids = set(ui.node_positions.keys())
        missing = node_ids_set - ui_node_ids
        extra = ui_node_ids - node_ids_set

        if missing:
            print(f"\n  WARNING: Nodes without UI position: {missing}")
        if extra:
            print(f"\n  WARNING: UI positions for non-existent nodes: {extra}")

        print("\nTimeline:")
        print(f"  Offset: {ui.timeline_offset:.2f}")
        print(f"  Zoom: {ui.timeline_zoom:.2f}")
        print("NodeGraph:")
        print(f"  Pan: ({ui.graph_pan_x:.1f}, {ui.graph_pan_y:.1f})")
        print(f"  Zoom: {ui.graph_zoom:.2f}")
        print("Camera:")
        print(
            f"  Position: ({ui.camera_position[0]:.1f}, {ui.camera_position[1]:.1f}, {ui.camera_position[2]:.1f})"
        )
        print(
            f"  Target: ({ui.camera_target_position[0]:.1f}, {ui.camera_target_position[1]:.1f}, {ui.camera_target_position[2]:.1f})"
        )
        print(
            f"  Distance: {ui.camera_distance:.1f} (target: {ui.camera_target_distance:.1f})"
        )
        print(f"  Pitch: {ui.camera_pitch:.1f} (target: {ui.camera_target_pitch:.1f})")
        print(f"  Yaw: {ui.camera_yaw:.1f} (target: {ui.camera_target_yaw:.1f})")
        print(f"  Speed: {ui.camera_speed_multiplier:.2f}")

        if ui.keyframe_states:
            print(f"\nKeyframe States: {len(ui.keyframe_states)}")
            for kf in ui.keyframe_states:
                print(
                    f"  Node {kf.node_id}, Prop {kf.property_id}, KF {kf.keyframe_index}: "
                    f"id={kf.id}, handle={kf.handle_type}, flags={kf.flags}"
                )


def create_test_file(filepath: str, include_extension: bool = True):
    """Create a test KEXD file for validation."""
    from io import BytesIO

    def write_uint(f, val):
        f.write(struct.pack("<I", val))

    def write_int(f, val):
        f.write(struct.pack("<i", val))

    def write_float(f, val):
        f.write(struct.pack("<f", val))

    def write_float2(f, x, y):
        write_float(f, x)
        write_float(f, y)

    def write_float3(f, x, y, z):
        write_float(f, x)
        write_float(f, y)
        write_float(f, z)

    def write_bool(f, val):
        f.write(struct.pack("B", 1 if val else 0))

    def write_byte(f, val):
        f.write(struct.pack("B", val))

    def write_chunk_type(f, t):
        for c in t.ljust(4, "\0")[:4]:
            f.write(struct.pack("B", ord(c)))

    def begin_chunk(f, chunk_type, version):
        start = f.tell()
        write_chunk_type(f, chunk_type)
        write_uint(f, version)
        write_uint(f, 0)
        return start

    def end_chunk(f, start):
        end = f.tell()
        content_len = end - start - CHUNK_HEADER_SIZE
        f.seek(start + 8)
        write_uint(f, content_len)
        f.seek(end)

    buf = BytesIO()

    buf.write(b"KEXD")
    write_uint(buf, 1)

    core_start = begin_chunk(buf, "CORE", 1)

    grph_start = begin_chunk(buf, "GRPH", 2)

    write_int(buf, 2)
    write_int(buf, 2)
    write_int(buf, 1)

    write_uint(buf, 1)
    write_uint(buf, 5)
    write_int(buf, 0)
    write_int(buf, 1)

    write_uint(buf, 2)
    write_uint(buf, 0)
    write_int(buf, 1)
    write_int(buf, 1)

    write_uint(buf, 1)
    write_uint(buf, 0)
    write_uint(buf, 1)
    write_bool(buf, False)

    write_uint(buf, 2)
    write_uint(buf, 0)
    write_uint(buf, 2)
    write_bool(buf, True)

    write_uint(buf, 1)
    write_uint(buf, 1)
    write_uint(buf, 2)

    write_uint(buf, 3)
    write_uint(buf, 3)
    write_uint(buf, 2)

    end_chunk(buf, grph_start)

    data_start = begin_chunk(buf, "DATA", 2)
    write_int(buf, 0)
    write_int(buf, 0)
    write_int(buf, 0)
    write_int(buf, 0)
    write_int(buf, 0)
    end_chunk(buf, data_start)

    end_chunk(buf, core_start)

    if include_extension:
        uist_start = begin_chunk(buf, "UIST", 1)

        write_int(buf, 2)
        write_uint(buf, 1)
        write_float2(buf, 100.0, 50.0)
        write_uint(buf, 2)
        write_float2(buf, 300.0, 50.0)

        write_float(buf, 10.0)
        write_float(buf, 2.0)
        write_float(buf, -150.0)
        write_float(buf, 75.0)
        write_float(buf, 1.5)
        write_float3(buf, 25.0, 15.0, -40.0)
        write_float3(buf, 0.0, 5.0, 0.0)
        write_float(buf, 50.0)
        write_float(buf, 50.0)
        write_float(buf, 30.0)
        write_float(buf, 30.0)
        write_float(buf, 45.0)
        write_float(buf, 45.0)
        write_float(buf, 1.0)

        write_int(buf, 0)

        end_chunk(buf, uist_start)

    with open(filepath, "wb") as f:
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
