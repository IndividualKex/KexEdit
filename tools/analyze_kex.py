#!/usr/bin/env python3
"""
Analyze .kex files to understand graph structure.
Based on C# serialization format from GraphSerializer.cs and struct definitions.
"""
import struct
import sys
from dataclasses import dataclass
from typing import List, Dict
from enum import IntEnum


class NodeType(IntEnum):
    ForceSection = 0
    GeometricSection = 1
    CurvedSection = 2
    CopyPathSection = 3
    Anchor = 4
    Reverse = 5
    ReversePath = 6
    Bridge = 7
    Mesh = 8
    Append = 9


class PortType(IntEnum):
    Anchor = 0
    Path = 1
    Duration = 2
    Position = 3
    Roll = 4
    Pitch = 5
    Yaw = 6
    Velocity = 7
    Heart = 8
    Friction = 9
    Resistance = 10
    Radius = 11
    Arc = 12
    Axis = 13
    LeadIn = 14
    LeadOut = 15
    InWeight = 16
    OutWeight = 17
    Rotation = 18
    Scale = 19
    Start = 20
    End = 21


class SerializationVersion:
    INITIAL = 1
    PRECISION_MIGRATION = 2
    UI_STATE_SERIALIZATION = 3
    TRACK_STYLE_PROPERTY = 4
    COPY_PATH_TRIM_PORTS = 5
    NODE_ID = 6
    BRIDGE_WEIGHT_PORTS = 7
    CURRENT = BRIDGE_WEIGHT_PORTS


class NodeFieldFlags(IntEnum):
    HasRender = 1 << 0
    HasSelected = 1 << 1
    HasPropertyOverrides = 1 << 2
    HasSelectedProperties = 1 << 3
    HasCurveData = 1 << 4
    HasDuration = 1 << 5
    HasMeshFilePath = 1 << 6
    HasSteering = 1 << 7


class NodeFlags(IntEnum):
    Render = 1 << 0
    Selected = 1 << 1
    Steering = 1 << 2


@dataclass
class PointData:
    heart_position: tuple  # float3
    direction: tuple  # float3
    lateral: tuple  # float3
    normal: tuple  # float3
    roll: float
    velocity: float
    energy: float
    normal_force: float
    lateral_force: float
    spine_advance: float
    heart_advance: float
    angle_from_last: float
    pitch_from_last: float
    yaw_from_last: float
    roll_speed: float
    spine_arc: float
    heart_arc: float
    friction_origin: float
    heart_offset: float
    friction: float
    resistance: float
    facing: int


@dataclass
class Port:
    id: int
    type: int
    is_input: bool


@dataclass
class SerializedPort:
    port: Port
    value: PointData


@dataclass
class Node:
    id: int
    position: tuple  # float2
    type: int
    priority: int
    selected: bool
    next_entity: tuple  # (index, version)
    prev_entity: tuple


@dataclass
class SerializedEdge:
    id: int
    source_id: int
    target_id: int
    selected: bool


@dataclass
class SerializedNode:
    node: Node
    anchor: PointData
    field_flags: int
    boolean_flags: int
    input_ports: List[SerializedPort]
    output_ports: List[SerializedPort]

    @property
    def render(self) -> bool:
        return (self.boolean_flags & NodeFlags.Render) != 0


class KexReader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def read_int(self) -> int:
        val = struct.unpack_from("<i", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_uint(self) -> int:
        val = struct.unpack_from("<I", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_float(self) -> float:
        val = struct.unpack_from("<f", self.data, self.pos)[0]
        self.pos += 4
        return val

    def read_bool(self) -> bool:
        val = struct.unpack_from("<B", self.data, self.pos)[0]
        self.pos += 1
        return val != 0

    def read_byte(self) -> int:
        val = struct.unpack_from("<B", self.data, self.pos)[0]
        self.pos += 1
        return val

    def read_float2(self) -> tuple:
        x = self.read_float()
        y = self.read_float()
        return (x, y)

    def read_float3(self) -> tuple:
        x = self.read_float()
        y = self.read_float()
        z = self.read_float()
        return (x, y, z)

    def read_entity(self) -> tuple:
        index = self.read_int()
        version = self.read_int()
        return (index, version)

    def read_port(self) -> Port:
        id = self.read_uint()
        type_val = self.read_int()
        is_input = self.read_bool()
        # Padding to 4-byte alignment
        self.pos += 3
        return Port(id, type_val, is_input)

    def read_point_data(self) -> PointData:
        return PointData(
            heart_position=self.read_float3(),
            direction=self.read_float3(),
            lateral=self.read_float3(),
            normal=self.read_float3(),
            roll=self.read_float(),
            velocity=self.read_float(),
            energy=self.read_float(),
            normal_force=self.read_float(),
            lateral_force=self.read_float(),
            spine_advance=self.read_float(),
            heart_advance=self.read_float(),
            angle_from_last=self.read_float(),
            pitch_from_last=self.read_float(),
            yaw_from_last=self.read_float(),
            roll_speed=self.read_float(),
            spine_arc=self.read_float(),
            heart_arc=self.read_float(),
            friction_origin=self.read_float(),
            heart_offset=self.read_float(),
            friction=self.read_float(),
            resistance=self.read_float(),
            facing=self.read_int(),
        )

    def read_serialized_port(self) -> SerializedPort:
        port = self.read_port()
        value = self.read_point_data()
        return SerializedPort(port, value)

    def read_node(self, version: int, counter: int) -> Node:
        if version < SerializationVersion.NODE_ID:
            # NodeV1 format: position, type, priority, selected, next, prev
            pos = self.read_float2()
            type_val = self.read_int()
            priority = self.read_int()
            selected = self.read_bool()
            self.pos += 3  # padding
            next_entity = self.read_entity()
            prev_entity = self.read_entity()
            return Node(
                counter, pos, type_val, priority, selected, next_entity, prev_entity
            )
        else:
            # Current format with node ID
            id = self.read_uint()
            pos = self.read_float2()
            type_val = self.read_int()
            priority = self.read_int()
            selected = self.read_bool()
            self.pos += 3  # padding
            next_entity = self.read_entity()
            prev_entity = self.read_entity()
            return Node(id, pos, type_val, priority, selected, next_entity, prev_entity)

    def read_edge(self) -> SerializedEdge:
        id = self.read_uint()
        source = self.read_uint()
        target = self.read_uint()
        selected = self.read_bool()
        self.pos += 3  # padding
        return SerializedEdge(id, source, target, selected)

    def read_keyframe_array(self, version: int):
        count = self.read_int()
        if version < SerializationVersion.PRECISION_MIGRATION:
            # Legacy keyframe: 48 bytes each
            self.pos += count * 48
        else:
            # Current keyframe: 48 bytes each
            self.pos += count * 48
        return count


def parse_kex(filepath: str):
    with open(filepath, "rb") as f:
        data = f.read()

    reader = KexReader(data)

    version = reader.read_int()
    print(f"File version: {version}")

    # UI State (12 floats if version >= 3)
    if version >= SerializationVersion.UI_STATE_SERIALIZATION:
        for _ in range(12):
            reader.read_float()

    node_count = reader.read_int()
    print(f"Node count: {node_count}")

    nodes = []
    counter = 1
    for i in range(node_count):
        node = reader.read_node(version, counter)
        counter += 1
        anchor = reader.read_point_data()
        field_flags = reader.read_uint()

        boolean_flags = 0
        if field_flags & (
            NodeFieldFlags.HasRender
            | NodeFieldFlags.HasSelected
            | NodeFieldFlags.HasSteering
        ):
            boolean_flags = reader.read_byte()

        if field_flags & NodeFieldFlags.HasPropertyOverrides:
            reader.pos += 1  # PropertyOverrides: byte (PropertyOverrideFlags)
        if field_flags & NodeFieldFlags.HasSelectedProperties:
            reader.pos += 4  # SelectedProperties: int
        if field_flags & NodeFieldFlags.HasCurveData:
            reader.pos += 20  # CurveData: 5 floats (Radius, Arc, Axis, LeadIn, LeadOut)
        if field_flags & NodeFieldFlags.HasDuration:
            reader.pos += 8  # Duration: DurationType (int) + float Value
        if field_flags & NodeFieldFlags.HasMeshFilePath:
            reader.pos += 514  # FixedString512Bytes

        # Input ports
        input_port_count = reader.read_int()
        input_ports = []
        for _ in range(input_port_count):
            input_ports.append(reader.read_serialized_port())

        # Bridge migration for old files
        if (
            version < SerializationVersion.BRIDGE_WEIGHT_PORTS
            and node.type == NodeType.Bridge
        ):
            has_in = any(p.port.type == PortType.InWeight for p in input_ports)
            has_out = any(p.port.type == PortType.OutWeight for p in input_ports)
            # Note: actual migration adds ports, we just log the issue
            if not has_in or not has_out:
                print(
                    f"  Note: Bridge node {node.id} missing weight ports (migrated on load)"
                )

        # Output ports
        output_port_count = reader.read_int()
        output_ports = []
        for _ in range(output_port_count):
            output_ports.append(reader.read_serialized_port())

        # Keyframes (9 arrays, or 10 if version >= 4)
        num_keyframe_arrays = (
            10 if version >= SerializationVersion.TRACK_STYLE_PROPERTY else 9
        )
        for _ in range(num_keyframe_arrays):
            reader.read_keyframe_array(version)

        serialized_node = SerializedNode(
            node=node,
            anchor=anchor,
            field_flags=field_flags,
            boolean_flags=boolean_flags,
            input_ports=input_ports,
            output_ports=output_ports,
        )
        nodes.append(serialized_node)

    # Edges
    edge_count = reader.read_int()
    print(f"Edge count: {edge_count}")

    edges = []
    for _ in range(edge_count):
        edges.append(reader.read_edge())

    return version, nodes, edges


def get_node_type_name(t: int) -> str:
    try:
        return NodeType(t).name
    except ValueError:
        return f"Unknown({t})"


def get_port_type_name(t: int) -> str:
    try:
        return PortType(t).name
    except ValueError:
        return f"Unknown({t})"


def analyze(filepath: str):
    print(f"Analyzing: {filepath}\n")
    version, nodes, edges = parse_kex(filepath)

    # Build port-to-node map
    port_to_node: Dict[int, tuple] = {}
    for sn in nodes:
        for i, port in enumerate(sn.input_ports):
            port_to_node[port.port.id] = (
                sn.node.id,
                sn.node.type,
                i,
                True,
                port.port.type,
            )
        for i, port in enumerate(sn.output_ports):
            port_to_node[port.port.id] = (
                sn.node.id,
                sn.node.type,
                i,
                False,
                port.port.type,
            )

    # Check for duplicate node IDs
    seen_ids = {}
    duplicates = []
    for i, sn in enumerate(nodes):
        if sn.node.id in seen_ids:
            duplicates.append((i, sn.node.id, seen_ids[sn.node.id]))
        else:
            seen_ids[sn.node.id] = i

    if duplicates:
        print("=== DUPLICATE NODE IDS FOUND ===\n")
        for idx, node_id, first_idx in duplicates:
            first = nodes[first_idx]
            dup = nodes[idx]
            print(f"Node ID {node_id} appears at indices {first_idx} and {idx}:")
            print(
                f"  First [{first_idx}]: {get_node_type_name(first.node.type)} at {first.node.position}"
            )
            print(
                f"  Dup   [{idx}]: {get_node_type_name(dup.node.type)} at {dup.node.position}"
            )
            print(f"  First output ports: {[p.port.id for p in first.output_ports]}")
            print(f"  Dup output ports: {[p.port.id for p in dup.output_ports]}")
            print()

    # Print all nodes
    print("=== ALL NODES ===\n")
    for i, sn in enumerate(nodes):
        print(f"[{i}] Node {sn.node.id}: {get_node_type_name(sn.node.type)}")
        print(f"    Position: ({sn.node.position[0]:.1f}, {sn.node.position[1]:.1f})")
        print(f"    Priority: {sn.node.priority}")
        print(f"    Render: {'Yes' if sn.render else 'No'}")

        if sn.input_ports:
            print(f"    Input ports ({len(sn.input_ports)}):")
            for j, p in enumerate(sn.input_ports):
                print(
                    f"      [{j}] Port {p.port.id}: {get_port_type_name(p.port.type)}"
                )

        if sn.output_ports:
            print(f"    Output ports ({len(sn.output_ports)}):")
            for j, p in enumerate(sn.output_ports):
                print(
                    f"      [{j}] Port {p.port.id}: {get_port_type_name(p.port.type)}"
                )
        print()

    # Priority/Render analysis
    print("=== PRIORITY/RENDER ANALYSIS ===\n")
    priorities = set(sn.node.priority for sn in nodes)
    render_counts = {
        True: sum(1 for sn in nodes if sn.render),
        False: sum(1 for sn in nodes if not sn.render),
    }
    print(f"Priority variation: {sorted(priorities)}")
    print(
        f"Render: {render_counts[True]} nodes with render=true, {render_counts[False]} nodes with render=false"
    )
    print()

    # Analyze Bridge nodes specifically
    bridges = [sn for sn in nodes if sn.node.type == NodeType.Bridge]
    if bridges:
        print("=== BRIDGE NODE ANALYSIS ===\n")
        for bridge in bridges:
            print(f"Bridge Node {bridge.node.id}:")
            print(f"  Position: {bridge.node.position}")
            print("  Expected schema: [Anchor, Target, OutWeight, InWeight]")
            print("  Input ports:")
            for j, p in enumerate(bridge.input_ports):
                conn = "NOT CONNECTED"
                for edge in edges:
                    if edge.target_id == p.port.id:
                        src_info = port_to_node.get(edge.source_id)
                        if src_info:
                            src_node_id, src_type, src_idx, _, src_port_type = src_info
                            conn = f"Node {src_node_id} ({get_node_type_name(src_type)}) port[{src_idx}]"
                        else:
                            conn = f"Port {edge.source_id} (node not found!)"
                        break
                print(
                    f"    [{j}] {get_port_type_name(p.port.type)} (id={p.port.id}) <- {conn}"
                )
            print()

    # Print edges
    print("=== ALL EDGES ===\n")
    for edge in edges:
        src_info = port_to_node.get(edge.source_id)
        tgt_info = port_to_node.get(edge.target_id)

        if src_info:
            src_str = f"Node {src_info[0]} ({get_node_type_name(src_info[1])}) port[{src_info[2]}]"
        else:
            src_str = f"Port {edge.source_id} (ORPHAN)"

        if tgt_info:
            tgt_str = f"Node {tgt_info[0]} ({get_node_type_name(tgt_info[1])}) port[{tgt_info[2]}]"
        else:
            tgt_str = f"Port {edge.target_id} (ORPHAN)"

        orphan_marker = ""
        if src_info is None or tgt_info is None:
            orphan_marker = " [BROKEN]"

        print(f"Edge {edge.id}: {src_str} -> {tgt_str}{orphan_marker}")


if __name__ == "__main__":
    filepath = sys.argv[1] if len(sys.argv) > 1 else "Assets/Tests/Assets/veloci.kex"
    try:
        analyze(filepath)
    except Exception as e:
        print(f"Error at position: {e}")
        import traceback

        traceback.print_exc()
