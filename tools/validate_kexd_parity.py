#!/usr/bin/env python3
"""
Validate parity between legacy .kex and KEXD formats.
Compares graph structure, node types, port types, scalar values, and UI positions.
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


@dataclass
class PortData:
    port_id: int
    port_type: int
    owner_id: int
    is_input: bool


@dataclass
class NodeData:
    node_id: int
    node_type: int
    position: Tuple[float, float]
    input_ports: List[PortData] = field(default_factory=list)
    output_ports: List[PortData] = field(default_factory=list)


@dataclass
class GraphData:
    nodes: List[NodeData] = field(default_factory=list)
    edges: List[Tuple[int, int, int]] = field(default_factory=list)
    scalars: Dict[int, float] = field(default_factory=dict)
    vectors: Dict[int, Tuple[float, float, float]] = field(default_factory=dict)
    rotations: Dict[int, Tuple[float, float, float]] = field(default_factory=dict)
    durations: Dict[int, Tuple[int, float]] = field(default_factory=dict)


class BinaryReader:
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

    def skip(self, count: int):
        self.pos += count

    def remaining(self) -> int:
        return len(self.data) - self.pos


def is_kexd(data: bytes) -> bool:
    return len(data) >= 4 and data[:4] == b"KEXD"


def parse_kexd(data: bytes) -> GraphData:
    reader = BinaryReader(data)
    result = GraphData()

    if not is_kexd(data):
        raise ValueError("Not a KEXD file")

    reader.skip(4)  # KEXD magic
    reader.read_uint()  # file version

    node_map = {}
    ports_list = []

    while reader.remaining() > 12:
        chunk_type = reader.read_chunk_type()
        _chunk_version = reader.read_uint()
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
                        _input_count = reader.read_int()
                        _output_count = reader.read_int()
                        node = NodeData(node_id, node_type, pos)
                        result.nodes.append(node)
                        node_map[node_id] = node

                    for _ in range(port_count):
                        port_id = reader.read_uint()
                        port_type = reader.read_uint()
                        owner = reader.read_uint()
                        is_input = reader.read_bool()
                        ports_list.append(PortData(port_id, port_type, owner, is_input))

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

                    duration_count = reader.read_int()
                    for _ in range(duration_count):
                        node_id = reader.read_uint()
                        duration_type = reader.read_int()
                        value = reader.read_float()
                        result.durations[node_id] = (duration_type, value)

                    # Skip remaining DATA fields
                    reader.pos = sub_end
                else:
                    reader.pos = sub_end

        elif chunk_type == "UIST":
            count = reader.read_int()
            for _ in range(count):
                node_id = reader.read_uint()
                pos = reader.read_float2()
                if node_id in node_map:
                    node_map[node_id].position = pos
            # Skip remaining UIST fields (view state, keyframe states)
            reader.pos = chunk_end
        else:
            reader.pos = chunk_end

    # Assign ports to nodes
    for port in ports_list:
        if port.owner_id in node_map:
            node = node_map[port.owner_id]
            if port.is_input:
                node.input_ports.append(port)
            else:
                node.output_ports.append(port)

    return result


def validate_kexd_file(filepath: str) -> bool:
    """Validate a KEXD file's structure and data integrity."""
    print(f"Validating: {filepath}\n")

    try:
        with open(filepath, "rb") as f:
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

    # Check for duplicate node IDs
    node_ids = [n.node_id for n in graph.nodes]
    node_id_counts = {}
    for nid in node_ids:
        node_id_counts[nid] = node_id_counts.get(nid, 0) + 1
    for nid, count in node_id_counts.items():
        if count > 1:
            errors.append(f"DUPLICATE NODE ID: {nid} appears {count} times")

    # Check for duplicate port IDs
    all_port_ids = []
    for node in graph.nodes:
        for port in node.input_ports:
            all_port_ids.append(port.port_id)
        for port in node.output_ports:
            all_port_ids.append(port.port_id)
    port_id_counts = {}
    for pid in all_port_ids:
        port_id_counts[pid] = port_id_counts.get(pid, 0) + 1
    for pid, count in port_id_counts.items():
        if count > 1:
            errors.append(f"DUPLICATE PORT ID: {pid} appears {count} times")

    # Check for duplicate edge IDs
    edge_ids = [e[0] for e in graph.edges]
    edge_id_counts = {}
    for eid in edge_ids:
        edge_id_counts[eid] = edge_id_counts.get(eid, 0) + 1
    for eid, count in edge_id_counts.items():
        if count > 1:
            errors.append(f"DUPLICATE EDGE ID: {eid} appears {count} times")

    # Check for overlapping node positions (nodes at same UI position)
    pos_to_nodes: Dict[Tuple[float, float], List[int]] = {}
    for node in graph.nodes:
        pos_key = (round(node.position[0], 1), round(node.position[1], 1))
        if pos_key not in pos_to_nodes:
            pos_to_nodes[pos_key] = []
        pos_to_nodes[pos_key].append(node.node_id)
    for pos, node_ids_at_pos in pos_to_nodes.items():
        if len(node_ids_at_pos) > 1:
            warnings.append(
                f"OVERLAPPING NODES at ({pos[0]}, {pos[1]}): "
                f"node IDs {node_ids_at_pos}"
            )

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


POINTDATA_SIZE = 120  # 4 float3 (48) + 17 floats (68) + 1 int (4) = 120 bytes
KEYFRAME_SIZE = 48  # KeyframeV1 struct size


def read_point_data(
    reader: BinaryReader,
) -> Tuple[Tuple[float, float, float], float, float]:
    """Read PointData and return (position, roll, velocity)."""
    position = reader.read_float3()  # HeartPosition (12 bytes)
    reader.skip(36)  # Direction, Lateral, Normal (3 * 12 bytes)
    roll = reader.read_float()  # 4 bytes
    velocity = reader.read_float()  # 4 bytes
    # Remaining: 15 floats (Energy..Resistance) + 1 int (Facing) = 64 bytes
    reader.skip(64)
    return position, roll, velocity


def skip_point_data(reader: BinaryReader):
    """Skip a PointData struct."""
    reader.skip(POINTDATA_SIZE)


def parse_legacy(data: bytes) -> GraphData:
    """Parse legacy .kex format and extract graph data."""
    reader = BinaryReader(data)
    result = GraphData()

    version = reader.read_int()

    # UI State (12 floats if version >= 3)
    if version >= 3:
        for _ in range(12):
            reader.read_float()

    node_count = reader.read_int()
    node_id_counter = 1

    for _ in range(node_count):
        # Read node header
        if version < 6:
            node_id = node_id_counter
            node_id_counter += 1
        else:
            node_id = reader.read_uint()

        pos = reader.read_float2()
        node_type = reader.read_int()
        _priority = reader.read_int()
        _selected = reader.read_bool()
        reader.skip(3)  # padding
        reader.skip(16)  # next + prev entities (2 * Entity = 2 * 8)

        # Read anchor point data
        skip_point_data(reader)

        field_flags = reader.read_uint()

        # Boolean flags
        if field_flags & (1 | 2 | 128):  # HasRender | HasSelected | HasSteering
            reader.skip(1)

        # Optional fields
        if field_flags & 4:  # HasPropertyOverrides
            reader.skip(1)
        if field_flags & 8:  # HasSelectedProperties
            reader.skip(4)
        if field_flags & 16:  # HasCurveData
            reader.skip(20)
        if field_flags & 32:  # HasDuration
            duration_type = reader.read_int()
            duration_value = reader.read_float()
            result.durations[node_id] = (duration_type, duration_value)
        if field_flags & 64:  # HasMeshFilePath
            reader.skip(514)

        # Input ports (array format: count + count * element_size)
        input_port_count = reader.read_int()
        input_ports = []
        for _ in range(input_port_count):
            port_id = reader.read_uint()
            port_type = reader.read_int()
            _is_input = reader.read_bool()
            reader.skip(3)  # padding

            # Read port value PointData
            position, roll, velocity = read_point_data(reader)

            # Store scalar values based on port type
            if port_type == PortType.Velocity:
                result.scalars[port_id] = velocity
            elif port_type == PortType.Roll:
                result.scalars[port_id] = roll
            elif port_type == PortType.Position:
                result.vectors[node_id] = position

            input_ports.append(PortData(port_id, port_type, node_id, True))

        # Output ports
        output_port_count = reader.read_int()
        output_ports = []
        for _ in range(output_port_count):
            port_id = reader.read_uint()
            port_type = reader.read_int()
            _is_input = reader.read_bool()
            reader.skip(3)  # padding
            skip_point_data(reader)
            output_ports.append(PortData(port_id, port_type, node_id, False))

        # Keyframes (9 or 10 arrays depending on version)
        num_keyframe_arrays = 10 if version >= 4 else 9
        for _ in range(num_keyframe_arrays):
            count = reader.read_int()
            reader.skip(count * KEYFRAME_SIZE)

        node = NodeData(node_id, node_type, pos, input_ports, output_ports)
        result.nodes.append(node)

    # Edges (array format: count + count * element_size)
    edge_count = reader.read_int()
    for _ in range(edge_count):
        edge_id = reader.read_uint()
        source = reader.read_uint()
        target = reader.read_uint()
        _selected = reader.read_bool()
        reader.skip(3)  # padding
        result.edges.append((edge_id, source, target))

    return result


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


def compare_graphs(
    legacy: GraphData, kexd: GraphData, tolerance: float = 0.001
) -> List[str]:
    """Compare two graphs and return list of differences."""
    differences = []

    # First check index integrity of both graphs
    legacy_errors = validate_index_integrity(legacy, "Legacy")
    kexd_errors = validate_index_integrity(kexd, "KEXD")
    differences.extend(legacy_errors)
    differences.extend(kexd_errors)

    # Node count
    if len(legacy.nodes) != len(kexd.nodes):
        differences.append(
            f"Node count mismatch: legacy={len(legacy.nodes)}, kexd={len(kexd.nodes)}"
        )
        return differences  # Can't continue comparison

    # Build node maps
    legacy_nodes = {n.node_id: n for n in legacy.nodes}
    kexd_nodes = {n.node_id: n for n in kexd.nodes}

    # Compare nodes
    for node_id in sorted(legacy_nodes.keys()):
        if node_id not in kexd_nodes:
            differences.append(f"Node {node_id} missing in KEXD")
            continue

        l_node = legacy_nodes[node_id]
        k_node = kexd_nodes[node_id]

        # Node type
        if l_node.node_type != k_node.node_type:
            differences.append(
                f"Node {node_id}: type mismatch: "
                f"legacy={get_node_type_name(l_node.node_type)}, "
                f"kexd={get_node_type_name(k_node.node_type)}"
            )

        # UI Position
        l_pos, k_pos = l_node.position, k_node.position
        pos_diff = ((l_pos[0] - k_pos[0]) ** 2 + (l_pos[1] - k_pos[1]) ** 2) ** 0.5
        if pos_diff > tolerance:
            differences.append(
                f"Node {node_id}: position drift: "
                f"legacy=({l_pos[0]:.2f}, {l_pos[1]:.2f}), "
                f"kexd=({k_pos[0]:.2f}, {k_pos[1]:.2f}), "
                f"distance={pos_diff:.3f}"
            )

        # Port counts
        if len(l_node.input_ports) != len(k_node.input_ports):
            differences.append(
                f"Node {node_id}: input port count mismatch: "
                f"legacy={len(l_node.input_ports)}, kexd={len(k_node.input_ports)}"
            )

        if len(l_node.output_ports) != len(k_node.output_ports):
            differences.append(
                f"Node {node_id}: output port count mismatch: "
                f"legacy={len(l_node.output_ports)}, kexd={len(k_node.output_ports)}"
            )

        # Port types (check if rotation ports were consolidated)
        l_input_types = [p.port_type for p in l_node.input_ports]
        k_input_types = [p.port_type for p in k_node.input_ports]

        # Check for Roll/Pitch/Yaw -> Rotation consolidation
        has_legacy_rotation = (
            PortType.Roll in l_input_types
            or PortType.Pitch in l_input_types
            or PortType.Yaw in l_input_types
        )
        has_kexd_rotation = PortType.Rotation in k_input_types

        if has_legacy_rotation and has_kexd_rotation:
            if PortType.Rotation not in l_input_types:
                differences.append(
                    f"Node {node_id}: rotation ports consolidated: "
                    f"legacy has Roll/Pitch/Yaw, kexd has Rotation"
                )

    # Edge count
    if len(legacy.edges) != len(kexd.edges):
        differences.append(
            f"Edge count mismatch: legacy={len(legacy.edges)}, kexd={len(kexd.edges)}"
        )

    # Scalar values
    all_port_ids = set(legacy.scalars.keys()) | set(kexd.scalars.keys())
    for port_id in sorted(all_port_ids):
        l_val = legacy.scalars.get(port_id)
        k_val = kexd.scalars.get(port_id)

        if l_val is None:
            differences.append(f"Scalar port {port_id}: missing in legacy")
        elif k_val is None:
            differences.append(f"Scalar port {port_id}: missing in KEXD")
        elif abs(l_val - k_val) > tolerance:
            differences.append(
                f"Scalar port {port_id}: value mismatch: "
                f"legacy={l_val:.6f}, kexd={k_val:.6f}, diff={abs(l_val - k_val):.6f}"
            )

    return differences


def validate_index_integrity(graph: GraphData, name: str) -> List[str]:
    """Check for duplicate/clashing IDs in a graph."""
    errors = []

    # Check for duplicate node IDs
    node_ids = [n.node_id for n in graph.nodes]
    node_id_counts = {}
    for nid in node_ids:
        node_id_counts[nid] = node_id_counts.get(nid, 0) + 1
    for nid, count in node_id_counts.items():
        if count > 1:
            errors.append(f"[{name}] DUPLICATE NODE ID: {nid} appears {count} times")

    # Check for duplicate port IDs
    all_port_ids = []
    for node in graph.nodes:
        for port in node.input_ports:
            all_port_ids.append((port.port_id, node.node_id, port.port_type))
        for port in node.output_ports:
            all_port_ids.append((port.port_id, node.node_id, port.port_type))

    port_id_counts: Dict[int, List[Tuple[int, int]]] = {}
    for pid, node_id, port_type in all_port_ids:
        if pid not in port_id_counts:
            port_id_counts[pid] = []
        port_id_counts[pid].append((node_id, port_type))

    for pid, occurrences in port_id_counts.items():
        if len(occurrences) > 1:
            details = ", ".join(
                f"node {nid} ({get_port_type_name(pt)})" for nid, pt in occurrences
            )
            errors.append(f"[{name}] DUPLICATE PORT ID: {pid} appears in {details}")

    # Check for duplicate edge IDs
    edge_ids = [e[0] for e in graph.edges]
    edge_id_counts = {}
    for eid in edge_ids:
        edge_id_counts[eid] = edge_id_counts.get(eid, 0) + 1
    for eid, count in edge_id_counts.items():
        if count > 1:
            errors.append(f"[{name}] DUPLICATE EDGE ID: {eid} appears {count} times")

    # Check for overlapping node positions
    pos_to_nodes: Dict[Tuple[float, float], List[int]] = {}
    for node in graph.nodes:
        pos_key = (round(node.position[0], 1), round(node.position[1], 1))
        if pos_key not in pos_to_nodes:
            pos_to_nodes[pos_key] = []
        pos_to_nodes[pos_key].append(node.node_id)
    for pos, node_ids_at_pos in pos_to_nodes.items():
        if len(node_ids_at_pos) > 1:
            errors.append(
                f"[{name}] OVERLAPPING NODES at ({pos[0]}, {pos[1]}): "
                f"node IDs {node_ids_at_pos}"
            )

    return errors


def compare_legacy_to_kexd_roundtrip(legacy_path: str):
    """Load legacy file, parse both formats, and compare."""
    print("=== LEGACY vs KEXD PARITY CHECK ===\n")
    print(f"File: {legacy_path}\n")

    try:
        with open(legacy_path, "rb") as f:
            data = f.read()
    except Exception as e:
        print(f"ERROR: Cannot read file: {e}")
        return False

    # Detect format
    if is_kexd(data):
        print("File is KEXD format - parsing KEXD only")
        try:
            kexd = parse_kexd(data)
            print(f"Nodes: {len(kexd.nodes)}")
            print(f"Edges: {len(kexd.edges)}")
            print(f"Scalars: {len(kexd.scalars)}")

            # Check for index integrity issues
            errors = validate_index_integrity(kexd, "KEXD")
            if errors:
                print("\n=== INDEX INTEGRITY ERRORS ===\n")
                for e in errors:
                    print(f"  - {e}")
                return False
            return True
        except Exception as e:
            print(f"ERROR: Failed to parse KEXD: {e}")
            import traceback

            traceback.print_exc()
            return False

    print("Parsing legacy format...")
    try:
        legacy = parse_legacy(data)
        print(f"  Nodes: {len(legacy.nodes)}")
        print(f"  Edges: {len(legacy.edges)}")
        print(f"  Scalars: {len(legacy.scalars)}")
    except Exception as e:
        print(f"ERROR: Failed to parse legacy: {e}")
        import traceback

        traceback.print_exc()
        return False

    # Check for index integrity in legacy file
    errors = validate_index_integrity(legacy, "Legacy")
    if errors:
        print("\n=== LEGACY INDEX INTEGRITY ERRORS ===\n")
        for e in errors:
            print(f"  - {e}")

    # For now, just show legacy stats - KEXD comparison requires Unity round-trip
    print("\nLEGACY FILE DETAILS:\n")
    for node in legacy.nodes:
        print(
            f"Node {node.node_id}: {get_node_type_name(node.node_type)} @ ({node.position[0]:.1f}, {node.position[1]:.1f})"
        )
        if node.input_ports:
            print(
                f"  Inputs: {[f'{get_port_type_name(p.port_type)}({p.port_id})' for p in node.input_ports]}"
            )
        if node.output_ports:
            print(
                f"  Outputs: {[f'{get_port_type_name(p.port_type)}({p.port_id})' for p in node.output_ports]}"
            )

    print("\nTo test round-trip parity:")
    print("1. Load this file in Unity")
    print("2. Save as KEXD format")
    print("3. Run: python validate_kexd_parity.py <legacy.kex> <saved.kex>")

    return True


def main():
    if len(sys.argv) < 2:
        print("Usage: validate_kexd_parity.py <file.kex> [kexd_roundtrip.kex]")
        print()
        print("Single file mode: Validates KEXD structure or shows legacy details")
        print("Two file mode: Compares legacy vs KEXD round-trip for parity")
        sys.exit(1)

    if len(sys.argv) == 2:
        filepath = sys.argv[1]
        with open(filepath, "rb") as f:
            data = f.read()

        if is_kexd(data):
            success = validate_kexd_file(filepath)
            sys.exit(0 if success else 1)
        else:
            success = compare_legacy_to_kexd_roundtrip(filepath)
            sys.exit(0 if success else 1)
    else:
        # Two file comparison
        legacy_path = sys.argv[1]
        kexd_path = sys.argv[2]

        print("Comparing legacy vs KEXD round-trip:\n")
        print(f"Legacy: {legacy_path}")
        print(f"KEXD:   {kexd_path}\n")

        with open(legacy_path, "rb") as f:
            legacy_data = f.read()
        with open(kexd_path, "rb") as f:
            kexd_data = f.read()

        try:
            legacy = parse_legacy(legacy_data)
            kexd = parse_kexd(kexd_data)

            differences = compare_graphs(legacy, kexd)

            if differences:
                print(f"Found {len(differences)} differences:\n")
                for diff in differences:
                    print(f"  - {diff}")
                sys.exit(1)
            else:
                print("Perfect parity! All checks passed.")
                sys.exit(0)

        except Exception as e:
            print(f"ERROR: {e}")
            import traceback

            traceback.print_exc()
            sys.exit(1)


if __name__ == "__main__":
    main()
