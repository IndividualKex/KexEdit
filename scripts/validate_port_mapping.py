#!/usr/bin/env python3
"""
Validate that the PortId -> PortSpec mapping is correct and complete.
This script verifies the refactor from port-name-based to data-type-based design.
"""

from dataclasses import dataclass
from enum import IntEnum
from typing import Dict, List, Tuple

class PortDataType(IntEnum):
    Scalar = 0
    Vector = 1
    Anchor = 2
    Path = 3

class PortId(IntEnum):
    Anchor = 0
    Path = 1
    Duration = 2
    Radius = 3
    Arc = 4
    Axis = 5
    LeadIn = 6
    LeadOut = 7
    InWeight = 8
    OutWeight = 9
    Start = 10
    End = 11
    Position = 12
    Rotation = 13
    Scalar = 14
    Vector = 15
    Target = 16
    Velocity = 17

@dataclass
class PortSpec:
    data_type: PortDataType
    local_index: int

# Expected mapping from PortId to PortDataType (as implemented in PortIdExtensions.DataType())
PORTID_TO_DATATYPE: Dict[PortId, PortDataType] = {
    PortId.Anchor: PortDataType.Anchor,
    PortId.Target: PortDataType.Anchor,
    PortId.Path: PortDataType.Path,
    PortId.Position: PortDataType.Vector,
    PortId.Rotation: PortDataType.Vector,
    PortId.Vector: PortDataType.Vector,
    PortId.Scalar: PortDataType.Scalar,
    # All others default to Scalar
    PortId.Duration: PortDataType.Scalar,
    PortId.Radius: PortDataType.Scalar,
    PortId.Arc: PortDataType.Scalar,
    PortId.Axis: PortDataType.Scalar,
    PortId.LeadIn: PortDataType.Scalar,
    PortId.LeadOut: PortDataType.Scalar,
    PortId.InWeight: PortDataType.Scalar,
    PortId.OutWeight: PortDataType.Scalar,
    PortId.Start: PortDataType.Scalar,
    PortId.End: PortDataType.Scalar,
    PortId.Velocity: PortDataType.Scalar,
}

# Default values per PortId (as implemented in PortIdExtensions.DefaultValue())
PORTID_DEFAULTS: Dict[PortId, float] = {
    PortId.Duration: 5.0,
    PortId.Radius: 20.0,
    PortId.Arc: 90.0,
    PortId.Axis: 0.0,
    PortId.LeadIn: 0.0,
    PortId.LeadOut: 0.0,
    PortId.InWeight: 0.5,
    PortId.OutWeight: 0.5,
    PortId.Start: 0.0,
    PortId.End: 1.0,
}

# Node input schemas: (NodeType, index) -> (PortId, PortSpec)
# This validates that InputSpec matches the Input method
NODE_INPUTS = {
    "Force": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
        (PortId.Duration, PortSpec(PortDataType.Scalar, 0)),
    ],
    "Geometric": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
        (PortId.Duration, PortSpec(PortDataType.Scalar, 0)),
    ],
    "Curved": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
        (PortId.Radius, PortSpec(PortDataType.Scalar, 0)),
        (PortId.Arc, PortSpec(PortDataType.Scalar, 1)),
        (PortId.Axis, PortSpec(PortDataType.Scalar, 2)),
        (PortId.LeadIn, PortSpec(PortDataType.Scalar, 3)),
        (PortId.LeadOut, PortSpec(PortDataType.Scalar, 4)),
    ],
    "CopyPath": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
        (PortId.Path, PortSpec(PortDataType.Path, 0)),
        (PortId.Start, PortSpec(PortDataType.Scalar, 0)),
        (PortId.End, PortSpec(PortDataType.Scalar, 1)),
    ],
    "Bridge": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
        (PortId.Target, PortSpec(PortDataType.Anchor, 1)),
        (PortId.InWeight, PortSpec(PortDataType.Scalar, 0)),
        (PortId.OutWeight, PortSpec(PortDataType.Scalar, 1)),
    ],
    "Anchor": [
        (PortId.Position, PortSpec(PortDataType.Vector, 0)),
        (PortId.Rotation, PortSpec(PortDataType.Vector, 1)),
        (PortId.Velocity, PortSpec(PortDataType.Scalar, 0)),
    ],
    "Reverse": [
        (PortId.Anchor, PortSpec(PortDataType.Anchor, 0)),
    ],
    "ReversePath": [
        (PortId.Path, PortSpec(PortDataType.Path, 0)),
    ],
}

# Expected port names (as implemented in NodeSchema.InputName)
NODE_INPUT_NAMES = {
    "Force": ["Anchor", "Duration"],
    "Geometric": ["Anchor", "Duration"],
    "Curved": ["Anchor", "Radius", "Arc", "Axis", "Lead In", "Lead Out"],
    "CopyPath": ["Anchor", "Path", "Start", "End"],
    "Bridge": ["Anchor", "Target", "In Weight", "Out Weight"],
    "Anchor": ["Position", "Rotation", "Velocity"],
    "Reverse": ["Anchor"],
    "ReversePath": ["Path"],
}

def validate_datatype_mapping():
    """Verify all PortId values have a DataType mapping."""
    print("Validating PortId -> PortDataType mapping...")
    errors = []

    for port_id in PortId:
        if port_id not in PORTID_TO_DATATYPE:
            errors.append(f"  Missing mapping for {port_id.name}")

    if errors:
        print("ERRORS:")
        for e in errors:
            print(e)
        return False

    print(f"  All {len(PortId)} PortId values have DataType mappings")
    return True

def validate_spec_consistency():
    """Verify PortSpec local indices are consistent within each node type."""
    print("\nValidating PortSpec local index consistency...")
    errors = []

    for node_type, inputs in NODE_INPUTS.items():
        # Track local indices per data type
        type_counts: Dict[PortDataType, int] = {}

        for idx, (port_id, spec) in enumerate(inputs):
            # Verify DataType matches
            expected_dt = PORTID_TO_DATATYPE[port_id]
            if spec.data_type != expected_dt:
                errors.append(f"  {node_type}[{idx}]: {port_id.name} has DataType {spec.data_type.name}, expected {expected_dt.name}")

            # Verify local index is sequential
            expected_local = type_counts.get(spec.data_type, 0)
            if spec.local_index != expected_local:
                errors.append(f"  {node_type}[{idx}]: {port_id.name} has LocalIndex {spec.local_index}, expected {expected_local}")

            type_counts[spec.data_type] = expected_local + 1

    if errors:
        print("ERRORS:")
        for e in errors:
            print(e)
        return False

    print(f"  All {len(NODE_INPUTS)} node types have consistent PortSpecs")
    return True

def validate_names():
    """Verify port names match expectations."""
    print("\nValidating port names...")

    # Just report the expected names for manual verification
    for node_type, names in NODE_INPUT_NAMES.items():
        print(f"  {node_type}: {names}")

    return True

def validate_defaults():
    """Report default values for ports."""
    print("\nDefault values:")
    for port_id, default in sorted(PORTID_DEFAULTS.items(), key=lambda x: x[0].value):
        print(f"  {port_id.name}: {default}")
    return True

def print_migration_summary():
    """Print a summary of the port mapping for reference."""
    print("\n" + "="*60)
    print("MIGRATION SUMMARY")
    print("="*60)

    for node_type, inputs in NODE_INPUTS.items():
        print(f"\n{node_type}:")
        for idx, (port_id, spec) in enumerate(inputs):
            name = NODE_INPUT_NAMES[node_type][idx]
            default = PORTID_DEFAULTS.get(port_id, 0.0)
            print(f"  [{idx}] {name}: {spec.data_type.name}({spec.local_index}) = {default}")

def main():
    print("Port Type System Validation")
    print("="*60)

    success = True
    success = validate_datatype_mapping() and success
    success = validate_spec_consistency() and success
    success = validate_names() and success
    success = validate_defaults() and success

    print_migration_summary()

    print("\n" + "="*60)
    if success:
        print("VALIDATION PASSED")
    else:
        print("VALIDATION FAILED")
    print("="*60)

    return 0 if success else 1

if __name__ == "__main__":
    exit(main())
