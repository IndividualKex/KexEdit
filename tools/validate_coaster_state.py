#!/usr/bin/env python3
"""
Validate Coaster aggregate state against .kex file expectations.

This script validates that the Coaster aggregate correctly represents .kex file data
by comparing expected values from the binary format against exported Coaster state.

Usage:
    python tools/validate_coaster_state.py path/to/file.kex [path/to/coaster_export.json]

If coaster_export.json is not provided, this script will only analyze the .kex file
and output expected Coaster state for manual verification.
"""
import struct
import sys
import json
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import List, Dict, Optional, Any
from enum import IntEnum


# Import existing .kex parser components
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


class DurationType(IntEnum):
    Time = 0
    Distance = 1


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


@dataclass
class ExpectedCoasterState:
    """Expected Coaster aggregate state extracted from .kex file"""

    # Graph structure
    node_ids: List[int]
    node_types: List[str]
    node_positions: List[tuple]  # (x, y)
    edge_count: int
    edges: List[tuple]  # (source_port_id, target_port_id)

    # Port values (Scalars/Vectors/Rotations)
    scalars: Dict[int, float]  # portId -> value
    vectors: Dict[int, tuple]  # nodeId -> (x, y, z)
    rotations: Dict[int, tuple]  # nodeId -> (x, y, z)

    # Durations
    durations: Dict[int, tuple]  # nodeId -> (value, type)

    # Keyframes
    keyframes: Dict[str, int]  # "(nodeId, propertyId)" -> keyframe_count

    # Flags
    steering_nodes: List[int]
    driven_nodes: List[int]


@dataclass
class PointData:
    heart_position: tuple
    direction: tuple
    lateral: tuple
    normal: tuple
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
    position: tuple
    type: int
    priority: int
    selected: bool
    next_entity: tuple
    prev_entity: tuple


@dataclass
class SerializedNode:
    node: Node
    anchor: PointData
    field_flags: int
    boolean_flags: int
    input_ports: List[SerializedPort]
    output_ports: List[SerializedPort]
    curve_data: Optional[tuple] = None  # (radius, arc, axis, lead_in, lead_out)
    duration: Optional[tuple] = None  # (type, value)
    keyframe_counts: Optional[List[int]] = None  # Count per property


@dataclass
class SerializedEdge:
    id: int
    source_id: int
    target_id: int
    selected: bool


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
        self.pos += 3  # padding
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
            self.pos += count * 48
        else:
            self.pos += count * 48
        return count


def extract_expected_state(kex_path: str) -> ExpectedCoasterState:
    """Extract expected Coaster state from .kex file"""
    with open(kex_path, "rb") as f:
        data = f.read()

    reader = KexReader(data)
    version = reader.read_int()

    # Skip UI State
    if version >= SerializationVersion.UI_STATE_SERIALIZATION:
        for _ in range(12):
            reader.read_float()

    node_count = reader.read_int()
    nodes: List[SerializedNode] = []
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
            reader.pos += 1
        if field_flags & NodeFieldFlags.HasSelectedProperties:
            reader.pos += 4

        curve_data = None
        if field_flags & NodeFieldFlags.HasCurveData:
            radius = reader.read_float()
            arc = reader.read_float()
            axis = reader.read_float()
            lead_in = reader.read_float()
            lead_out = reader.read_float()
            curve_data = (radius, arc, axis, lead_in, lead_out)

        duration = None
        if field_flags & NodeFieldFlags.HasDuration:
            duration_type = reader.read_int()
            duration_value = reader.read_float()
            duration = (duration_value, duration_type)

        if field_flags & NodeFieldFlags.HasMeshFilePath:
            reader.pos += 514

        # Input ports
        input_port_count = reader.read_int()
        input_ports = []
        for _ in range(input_port_count):
            input_ports.append(reader.read_serialized_port())

        # Output ports
        output_port_count = reader.read_int()
        output_ports = []
        for _ in range(output_port_count):
            output_ports.append(reader.read_serialized_port())

        # Keyframes
        num_keyframe_arrays = (
            10 if version >= SerializationVersion.TRACK_STYLE_PROPERTY else 9
        )
        keyframe_counts = []
        for _ in range(num_keyframe_arrays):
            keyframe_counts.append(reader.read_keyframe_array(version))

        serialized_node = SerializedNode(
            node=node,
            anchor=anchor,
            field_flags=field_flags,
            boolean_flags=boolean_flags,
            input_ports=input_ports,
            output_ports=output_ports,
            curve_data=curve_data,
            duration=duration,
            keyframe_counts=keyframe_counts,
        )
        nodes.append(serialized_node)

    # Edges
    edge_count = reader.read_int()
    edges = []
    for _ in range(edge_count):
        edges.append(reader.read_edge())

    # Build expected Coaster state
    # Handle duplicate node IDs (LegacyImporter remaps them)
    seen_node_ids = set()
    node_id_remap = {}  # index -> remapped_id
    next_node_id = max(n.node.id for n in nodes) + 1

    for i, n in enumerate(nodes):
        if n.node.id in seen_node_ids:
            node_id_remap[i] = next_node_id
            next_node_id += 1
        else:
            seen_node_ids.add(n.node.id)

    node_ids = []
    for i, n in enumerate(nodes):
        node_ids.append(node_id_remap.get(i, n.node.id))

    node_types = [NodeType(n.node.type).name for n in nodes]
    node_positions = [n.node.position for n in nodes]

    scalars = {}
    vectors = {}
    rotations = {}
    durations = {}
    keyframes = {}
    steering_nodes = []
    driven_nodes = []

    for i, sn in enumerate(nodes):
        node_id = node_id_remap.get(i, sn.node.id)

        # Extract port values
        # NOTE: Legacy serialization stores ALL scalar port values in PointData.Roll field
        # See LegacyImporter.cs line 330: coaster.Scalars[portId] = value.Roll;
        for port in sn.input_ports:
            port_id = port.port.id
            port_type = PortType(port.port.type)

            # Scalar ports (all stored in value.roll)
            if port_type in [
                PortType.Velocity,
                PortType.Roll,
                PortType.Pitch,
                PortType.Yaw,
                PortType.Friction,
                PortType.Resistance,
                PortType.Heart,
                PortType.Radius,
                PortType.Arc,
                PortType.Axis,
                PortType.LeadIn,
                PortType.LeadOut,
                PortType.InWeight,
                PortType.OutWeight,
                PortType.Start,
                PortType.End,
            ]:
                scalars[port_id] = port.value.roll

            # Vector port (Position) - stored in (roll, velocity, energy) fields
            elif port_type == PortType.Position:
                vectors[node_id] = (
                    port.value.roll,
                    port.value.velocity,
                    port.value.energy,
                )

            # Rotation port - stored in (roll, velocity, energy) fields, converted to radians
            elif port_type == PortType.Rotation:
                import math

                rotations[node_id] = (
                    math.radians(port.value.roll),
                    math.radians(port.value.velocity),
                    math.radians(port.value.energy),
                )

        # Anchor nodes: velocity port gets value from anchor data
        # See LegacyImporter.cs line 341: coaster.Scalars[velocityPortId] = node.Anchor.Velocity;
        if sn.node.type == NodeType.Anchor:
            # Find velocity port (should be an input port added during import)
            # The importer creates a new velocity port for anchor nodes
            # For validation, we'll assume the anchor.velocity is used
            # Note: This is a synthetic port created by the importer, so it won't have a port_id from .kex
            # We'll skip this for now as it's a migration artifact
            pass

        # Anchor nodes: position from anchor data
        # See LegacyImporter.cs line 348: coaster.Vectors[nodeId] = node.Anchor.HeartPosition;
        if sn.node.type == NodeType.Anchor:
            if not all(v == 0 for v in sn.anchor.heart_position):
                vectors[node_id] = sn.anchor.heart_position

        # Duration
        if sn.duration:
            durations[node_id] = sn.duration

        # Keyframes
        if sn.keyframe_counts:
            for prop_idx, count in enumerate(sn.keyframe_counts):
                if count > 0:
                    key = f"({node_id}, {PropertyId(prop_idx).name})"
                    keyframes[key] = count

        # Steering flag
        if sn.field_flags & NodeFieldFlags.HasSteering:
            if sn.boolean_flags & 0x01:  # Assuming steering is first bit
                steering_nodes.append(node_id)

    edge_list = [(e.source_id, e.target_id) for e in edges]

    return ExpectedCoasterState(
        node_ids=node_ids,
        node_types=node_types,
        node_positions=node_positions,
        edge_count=edge_count,
        edges=edge_list,
        scalars=scalars,
        vectors=vectors,
        rotations=rotations,
        durations=durations,
        keyframes=keyframes,
        steering_nodes=steering_nodes,
        driven_nodes=driven_nodes,
    )


def compare_states(expected: ExpectedCoasterState, actual: Dict[str, Any]) -> List[str]:
    """Compare expected vs actual Coaster state, return list of discrepancies

    NOTE: The actual Coaster may contain additional ports not in the .kex file:
    - Anchor nodes get synthetic Velocity input ports (LegacyImporter.cs line 146-154)
    - Bridge nodes may get additional weight ports (LegacyImporter.cs line 133-143)
    These are expected and not counted as discrepancies.
    """
    discrepancies = []

    # Compare graph structure
    if len(expected.node_ids) != len(actual.get("node_ids", [])):
        discrepancies.append(
            f"Node count mismatch: expected {len(expected.node_ids)}, got {len(actual.get('node_ids', []))}"
        )

    if expected.edge_count != len(actual.get("edges", [])):
        discrepancies.append(
            f"Edge count mismatch: expected {expected.edge_count}, got {len(actual.get('edges', []))}"
        )

    # Compare scalars (only check ones in expected; actual may have more)
    actual_scalars = actual.get("scalars", {})
    for port_id, expected_value in expected.scalars.items():
        if str(port_id) in actual_scalars:
            actual_value = actual_scalars[str(port_id)]
            if abs(expected_value - actual_value) > 0.001:
                discrepancies.append(
                    f"Scalar[{port_id}] mismatch: expected {expected_value}, got {actual_value}"
                )
        else:
            discrepancies.append(f"Scalar[{port_id}] missing in actual state")

    # Compare vectors
    actual_vectors = actual.get("vectors", {})
    for node_id, expected_vec in expected.vectors.items():
        if str(node_id) in actual_vectors:
            actual_vec = actual_vectors[str(node_id)]
            for i in range(3):
                if abs(expected_vec[i] - actual_vec[i]) > 0.001:
                    discrepancies.append(
                        f"Vector[{node_id}][{i}] mismatch: expected {expected_vec[i]}, got {actual_vec[i]}"
                    )
        else:
            discrepancies.append(f"Vector[{node_id}] missing in actual state")

    # Compare durations
    actual_durations = actual.get("durations", {})
    for node_id, expected_dur in expected.durations.items():
        if str(node_id) in actual_durations:
            actual_dur = actual_durations[str(node_id)]
            if abs(expected_dur[0] - actual_dur["value"]) > 0.001:
                discrepancies.append(
                    f"Duration[{node_id}] value mismatch: expected {expected_dur[0]}, got {actual_dur['value']}"
                )
            if expected_dur[1] != actual_dur["type"]:
                discrepancies.append(
                    f"Duration[{node_id}] type mismatch: expected {expected_dur[1]}, got {actual_dur['type']}"
                )
        else:
            discrepancies.append(f"Duration[{node_id}] missing in actual state")

    # Compare keyframe counts
    actual_keyframes = actual.get("keyframes", {})
    for key, expected_count in expected.keyframes.items():
        if key in actual_keyframes:
            actual_count = actual_keyframes[key]
            if expected_count != actual_count:
                discrepancies.append(
                    f"Keyframe{key} count mismatch: expected {expected_count}, got {actual_count}"
                )
        else:
            discrepancies.append(f"Keyframe{key} missing in actual state")

    return discrepancies


def main():
    if len(sys.argv) < 2:
        print(
            "Usage: python tools/validate_coaster_state.py path/to/file.kex [path/to/coaster_export.json]"
        )
        sys.exit(1)

    kex_path = sys.argv[1]
    export_path = sys.argv[2] if len(sys.argv) > 2 else None

    print(f"Analyzing .kex file: {kex_path}\n")

    try:
        expected = extract_expected_state(kex_path)

        print("=== EXPECTED COASTER STATE ===\n")
        print("Graph Structure:")
        print(f"  Nodes: {len(expected.node_ids)}")
        print(f"  Edges: {expected.edge_count}")
        print("\nData Collections:")
        print(f"  Scalars: {len(expected.scalars)}")
        print(f"  Vectors: {len(expected.vectors)}")
        print(f"  Rotations: {len(expected.rotations)}")
        print(f"  Durations: {len(expected.durations)}")
        print(f"  Keyframe curves: {len(expected.keyframes)}")
        print("\nFlags:")
        print(f"  Steering nodes: {expected.steering_nodes}")
        print(f"  Driven nodes: {expected.driven_nodes}")

        # Export expected state to JSON for reference
        output_path = Path(kex_path).with_suffix(".expected.json")
        with open(output_path, "w") as f:
            json.dump(asdict(expected), f, indent=2)
        print(f"\n[OK] Expected state written to: {output_path}")

        # If actual export provided, compare
        if export_path:
            print("\n=== COMPARING WITH ACTUAL STATE ===\n")
            with open(export_path, "r") as f:
                actual = json.load(f)

            discrepancies = compare_states(expected, actual)

            if discrepancies:
                print(f"[FAIL] Found {len(discrepancies)} discrepancies:\n")
                for d in discrepancies:
                    print(f"  - {d}")
                sys.exit(1)
            else:
                print("[OK] All checks passed! Coaster state matches .kex file.")
        else:
            print(
                "\nNo actual state provided. Run Unity test to export Coaster state for comparison."
            )

    except Exception as e:
        print(f"Error: {e}")
        import traceback

        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
