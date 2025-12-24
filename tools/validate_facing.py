#!/usr/bin/env python3
"""
Validate facing values in legacy .kex files.

Facing is point-level data in legacy format but needs to be node-level in new format.
This script analyzes anchor data in nodes to understand facing values.
"""
import sys
from analyze_kex import parse_kex, get_node_type_name, NodeType


def validate_facing(filepath):
    """Validate facing values in a .kex file."""
    print(f"Analyzing facing values in: {filepath}\n")

    version, nodes, edges = parse_kex(filepath)

    print("=== FACING ANALYSIS ===\n")

    for i, sn in enumerate(nodes):
        anchor = sn.anchor
        node_type = get_node_type_name(sn.node.type)

        print(f"Node {sn.node.id} ({node_type}):")
        print(f"  Anchor facing: {anchor.facing}")

        has_direction = (
            abs(anchor.direction[0]) > 0.01
            or abs(anchor.direction[1]) > 0.01
            or abs(anchor.direction[2]) > 0.01
        )
        has_position = (
            abs(anchor.heart_position[0]) > 0.01
            or abs(anchor.heart_position[1]) > 0.01
            or abs(anchor.heart_position[2]) > 0.01
        )

        if has_direction or has_position:
            print(
                f"  Position: ({anchor.heart_position[0]:.2f}, {anchor.heart_position[1]:.2f}, {anchor.heart_position[2]:.2f})"
            )
            print(
                f"  Direction: ({anchor.direction[0]:.3f}, {anchor.direction[1]:.3f}, {anchor.direction[2]:.3f})"
            )

        if sn.node.type == NodeType.Reverse:
            print("  [REVERSE NODE] Expected facing to flip direction")

        print()

    print("\n=== FACING SUMMARY ===")

    facing_values = [sn.anchor.facing for sn in nodes]
    unique_facing = set(facing_values)
    print(f"Unique facing values: {sorted(unique_facing)}")
    print("Facing distribution:")
    for value in sorted(unique_facing):
        count = facing_values.count(value)
        print(f"  {value:+2d}: {count} nodes")

    all_forward = all(f == 1 for f in facing_values)
    if all_forward:
        print("\n[OK] All nodes have forward facing (1)")
    else:
        print("\n[WARNING] Mixed facing values detected")
        reverse_nodes = [nodes[i] for i, f in enumerate(facing_values) if f != 1]
        print("Nodes with facing != 1:")
        for sn in reverse_nodes:
            print(
                f"  Node {sn.node.id} ({get_node_type_name(sn.node.type)}): facing={sn.anchor.facing}"
            )


def main():
    filepath = sys.argv[1] if len(sys.argv) > 1 else "Assets/Tests/Assets/veloci.kex"
    validate_facing(filepath)


if __name__ == "__main__":
    main()
