#!/usr/bin/env python3
"""Debug script to understand TrackSpline build from shuttle.kex"""

import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from analyze_kex import parse_kex

def main():
    kex_path = "Assets/Tests/Assets/shuttle.kex"
    data = parse_kex(kex_path)

    # Find traversal order by priority
    nodes_with_paths = []
    for node in data['nodes']:
        if node['type'] in ['Force', 'Geometric', 'Curved', 'CopyPath', 'Bridge']:
            priority = node.get('priority', 0)
            if priority >= 0:  # Only include positive priority
                nodes_with_paths.append((node['id'], node['type'], priority))

    # Sort by priority descending
    nodes_with_paths.sort(key=lambda x: -x[2])

    print("Traversal Order (nodes with paths, priority >= 0):")
    for i, (node_id, node_type, priority) in enumerate(nodes_with_paths):
        print(f"  [{i}] Node {node_id}: {node_type}, priority={priority}")

    print(f"\nTotal traversed nodes: {len(nodes_with_paths)}")

    if len(nodes_with_paths) == 0:
        print("WARNING: No traversed nodes! TraversalToSegment will be empty.")

if __name__ == "__main__":
    main()
