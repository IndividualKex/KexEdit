#!/usr/bin/env python3
"""
Comprehensive validation of veloci.kex structure to identify all issues.
"""
import sys
from collections import defaultdict
from analyze_kex import parse_kex, get_node_type_name, NodeType


def validate_file_structure(filepath):
    """Validate the veloci.kex file for all structural issues."""
    print(f"Validating file: {filepath}")
    print("=" * 80)

    _version, nodes, edges = parse_kex(filepath)

    issues = []

    # Check 1: Duplicate node IDs
    print("\n[CHECK 1] Duplicate Node IDs")
    node_id_map = defaultdict(list)
    for idx, sn in enumerate(nodes):
        node_id_map[sn.node.id].append(idx)

    duplicates = {
        nid: indices for nid, indices in node_id_map.items() if len(indices) > 1
    }
    if duplicates:
        for nid, indices in duplicates.items():
            print(
                f"  [ERROR] Node ID {nid} appears {len(indices)} times at indices {indices}"
            )
            for idx in indices:
                sn = nodes[idx]
                print(
                    f"    [{idx}] Type: {get_node_type_name(sn.node.type)}, Pos: {sn.node.position}"
                )
            issues.append(f"Duplicate node ID {nid}")
    else:
        print("  [OK] No duplicate node IDs")

    # Check 2: Duplicate port IDs
    print("\n[CHECK 2] Duplicate Port IDs")
    port_id_map = defaultdict(list)
    for idx, sn in enumerate(nodes):
        for pidx, p in enumerate(sn.input_ports):
            port_id_map[p.port.id].append(("input", idx, pidx))
        for pidx, p in enumerate(sn.output_ports):
            port_id_map[p.port.id].append(("output", idx, pidx))

    dup_ports = {pid: locs for pid, locs in port_id_map.items() if len(locs) > 1}
    if dup_ports:
        for pid, locs in dup_ports.items():
            print(f"  [ERROR] Port ID {pid} appears {len(locs)} times:")
            for direction, nidx, pidx in locs:
                sn = nodes[nidx]
                print(
                    f"    Node {sn.node.id} ({get_node_type_name(sn.node.type)}) {direction}[{pidx}]"
                )
            issues.append(f"Duplicate port ID {pid}")
    else:
        print("  [OK] No duplicate port IDs")

    # Check 3: Duplicate edge IDs
    print("\n[CHECK 3] Duplicate Edge IDs")
    edge_id_map = defaultdict(list)
    for idx, e in enumerate(edges):
        edge_id_map[e.id].append(idx)

    dup_edges = {
        eid: indices for eid, indices in edge_id_map.items() if len(indices) > 1
    }
    if dup_edges:
        for eid, indices in dup_edges.items():
            print(
                f"  [ERROR] Edge ID {eid} appears {len(indices)} times at indices {indices}"
            )
            issues.append(f"Duplicate edge ID {eid}")
    else:
        print("  [OK] No duplicate edge IDs")

    # Check 4: Orphan edges (referencing non-existent ports)
    print("\n[CHECK 4] Orphan Edges")
    valid_port_ids = set(port_id_map.keys())
    orphan_edges = []
    for idx, e in enumerate(edges):
        if e.source_id not in valid_port_ids:
            print(f"  [ERROR] Edge {e.id}: source port {e.source_id} not found")
            orphan_edges.append((e.id, "source", e.source_id))
        if e.target_id not in valid_port_ids:
            print(f"  [ERROR] Edge {e.id}: target port {e.target_id} not found")
            orphan_edges.append((e.id, "target", e.target_id))

    if orphan_edges:
        issues.extend([f"Orphan edge {eid}" for eid, _, _ in orphan_edges])
    else:
        print("  [OK] No orphan edges")

    # Check 5: Bridge nodes analysis
    print("\n[CHECK 5] Bridge Node Target Connections")
    bridges = [
        (idx, sn) for idx, sn in enumerate(nodes) if sn.node.type == NodeType.Bridge
    ]
    for idx, bridge in bridges:
        print(f"  Bridge node {bridge.node.id} (index {idx}):")
        print(f"    Position: {bridge.node.position}")

        # Find Target port (should be second Anchor input)
        anchor_inputs = [
            p for p in bridge.input_ports if p.port.type == 0
        ]  # PortType.Anchor
        if len(anchor_inputs) >= 2:
            target_port_id = anchor_inputs[1].port.id
            print(f"    Target port ID: {target_port_id}")

            # Find edges targeting this port
            target_edges = [e for e in edges if e.target_id == target_port_id]
            if target_edges:
                for e in target_edges:
                    # Find source node
                    locs = port_id_map[e.source_id]
                    if locs:
                        _, src_nidx, _ = locs[0]
                        src_node = nodes[src_nidx]
                        print(
                            f"      Connected to: Node {src_node.node.id} ({get_node_type_name(src_node.node.type)})"
                        )
            else:
                print("      [WARNING] No edges connect to Target port")
                print(
                    f"      Anchor data: pos={bridge.anchor.heart_position[:2]}, dir={bridge.anchor.direction[:2]}"
                )
                has_data = (
                    abs(bridge.anchor.heart_position[0]) > 0.01
                    or abs(bridge.anchor.heart_position[1]) > 0.01
                    or abs(bridge.anchor.direction[0]) > 0.01
                    or abs(bridge.anchor.direction[1]) > 0.01
                )
                if has_data:
                    print(
                        f"      [INFO] Will create synthetic Anchor at {bridge.node.position[0] - 100}, {bridge.node.position[1] + 50}"
                    )
        else:
            print(
                f"    [WARNING] Bridge has only {len(anchor_inputs)} Anchor inputs (expected 2)"
            )

    # Summary
    print("\n" + "=" * 80)
    print(f"VALIDATION SUMMARY: {len(issues)} issues found")
    if issues:
        print("\nIssues:")
        for i, issue in enumerate(issues, 1):
            print(f"  {i}. {issue}")
    else:
        print("File structure is valid!")

    return issues


def main():
    filepath = sys.argv[1] if len(sys.argv) > 1 else "Assets/Tests/Assets/veloci.kex"
    issues = validate_file_structure(filepath)
    sys.exit(1 if issues else 0)


if __name__ == "__main__":
    main()
