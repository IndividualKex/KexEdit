#!/usr/bin/env python3
"""
Compare old vs new gold data JSON files to validate shape similarity.

Usage:
    python tools/compare_gold_data.py [--backup-dir BACKUP_DIR] [--new-dir NEW_DIR]

Compares gold data files between backup (old) and current (new) directories,
reporting drift statistics and shape similarity metrics.
"""
import json
import math
import sys
from pathlib import Path
from dataclasses import dataclass
from typing import List, Dict, Optional, Any


@dataclass
class DriftStats:
    """Statistics for drift between old and new data."""
    max_position_drift: float = 0.0
    max_velocity_drift: float = 0.0
    max_energy_drift: float = 0.0
    max_direction_drift: float = 0.0
    max_drift_index: int = 0
    total_points: int = 0


def vec3_distance(v1: Dict, v2: Dict) -> float:
    """Euclidean distance between two vec3 dicts."""
    dx = v1.get('x', 0) - v2.get('x', 0)
    dy = v1.get('y', 0) - v2.get('y', 0)
    dz = v1.get('z', 0) - v2.get('z', 0)
    return math.sqrt(dx*dx + dy*dy + dz*dz)


def compare_points(old_points: List[Dict], new_points: List[Dict]) -> DriftStats:
    """Compare two lists of point data and compute drift statistics."""
    stats = DriftStats()
    stats.total_points = len(old_points)

    # Compare up to the minimum length
    compare_count = min(len(old_points), len(new_points))

    for i in range(compare_count):
        old_pt = old_points[i]
        new_pt = new_points[i]

        # Position drift
        pos_drift = vec3_distance(
            old_pt.get('heartPosition', {}),
            new_pt.get('heartPosition', {})
        )
        if pos_drift > stats.max_position_drift:
            stats.max_position_drift = pos_drift
            stats.max_drift_index = i

        # Velocity drift
        vel_drift = abs(old_pt.get('velocity', 0) - new_pt.get('velocity', 0))
        stats.max_velocity_drift = max(stats.max_velocity_drift, vel_drift)

        # Energy drift
        energy_drift = abs(old_pt.get('energy', 0) - new_pt.get('energy', 0))
        stats.max_energy_drift = max(stats.max_energy_drift, energy_drift)

        # Direction drift
        dir_drift = vec3_distance(
            old_pt.get('direction', {}),
            new_pt.get('direction', {})
        )
        stats.max_direction_drift = max(stats.max_direction_drift, dir_drift)

    return stats


def compare_section(old_section: Dict, new_section: Dict) -> Dict[str, Any]:
    """Compare two sections and return comparison results."""
    old_points = old_section.get('outputs', {}).get('points', [])
    new_points = new_section.get('outputs', {}).get('points', [])

    result = {
        'nodeId': old_section.get('nodeId'),
        'nodeType': old_section.get('nodeType'),
        'old_point_count': len(old_points),
        'new_point_count': len(new_points),
        'point_count_diff': len(new_points) - len(old_points),
        'drift': None
    }

    if old_points and new_points:
        result['drift'] = compare_points(old_points, new_points)

    return result


def load_json_with_bom(path: Path) -> Dict[str, Any]:
    """Load JSON file, handling BOM if present."""
    with open(path, 'rb') as f:
        content = f.read()
    # Skip BOM if present
    if content.startswith(b'\xef\xbb\xbf'):
        content = content[3:]
    return json.loads(content)


def compare_gold_files(old_path: Path, new_path: Path) -> Dict[str, Any]:
    """Compare two gold data JSON files."""
    old_data = load_json_with_bom(old_path)
    new_data = load_json_with_bom(new_path)

    result = {
        'file': old_path.stem,
        'old_sections': len(old_data.get('sections', [])),
        'new_sections': len(new_data.get('sections', [])),
        'section_comparisons': [],
        'overall_max_position_drift': 0.0,
        'overall_max_velocity_drift': 0.0,
        'overall_max_energy_drift': 0.0,
        'total_old_points': 0,
        'total_new_points': 0
    }

    old_sections = old_data.get('sections', [])
    new_sections = new_data.get('sections', [])

    # Build map of nodeId -> section for new data
    new_section_map = {s.get('nodeId'): s for s in new_sections}

    for old_section in old_sections:
        node_id = old_section.get('nodeId')
        old_points = old_section.get('outputs', {}).get('points', [])
        result['total_old_points'] += len(old_points)

        if node_id in new_section_map:
            new_section = new_section_map[node_id]
            new_points = new_section.get('outputs', {}).get('points', [])
            result['total_new_points'] += len(new_points)

            comparison = compare_section(old_section, new_section)
            result['section_comparisons'].append(comparison)

            if comparison['drift']:
                drift = comparison['drift']
                result['overall_max_position_drift'] = max(
                    result['overall_max_position_drift'],
                    drift.max_position_drift
                )
                result['overall_max_velocity_drift'] = max(
                    result['overall_max_velocity_drift'],
                    drift.max_velocity_drift
                )
                result['overall_max_energy_drift'] = max(
                    result['overall_max_energy_drift'],
                    drift.max_energy_drift
                )
        else:
            result['section_comparisons'].append({
                'nodeId': node_id,
                'nodeType': old_section.get('nodeType'),
                'error': 'Missing in new data'
            })

    return result


def print_comparison(result: Dict[str, Any], verbose: bool = False):
    """Print comparison results."""
    print(f"\n{'='*60}")
    print(f"File: {result['file']}")
    print(f"{'='*60}")
    print(f"Sections: {result['old_sections']} -> {result['new_sections']}")
    print(f"Total points: {result['total_old_points']} -> {result['total_new_points']}")
    print(f"\nOverall drift:")
    print(f"  Max position drift: {result['overall_max_position_drift']:.6f} m")
    print(f"  Max velocity drift: {result['overall_max_velocity_drift']:.6f} m/s")
    print(f"  Max energy drift: {result['overall_max_energy_drift']:.6f} J")

    # Flag concerning results
    warnings = []
    if result['overall_max_position_drift'] > 5.0:
        warnings.append(f"Position drift > 5m ({result['overall_max_position_drift']:.2f}m)")
    if result['overall_max_velocity_drift'] > 2.0:
        warnings.append(f"Velocity drift > 2m/s ({result['overall_max_velocity_drift']:.2f}m/s)")
    if abs(result['total_new_points'] - result['total_old_points']) > 100:
        warnings.append(f"Point count changed significantly ({result['total_old_points']} -> {result['total_new_points']})")

    if warnings:
        print(f"\n[!] WARNINGS:")
        for w in warnings:
            print(f"    - {w}")
    else:
        print(f"\n[OK] Shape looks similar")

    if verbose:
        print(f"\nSection details:")
        for comp in result['section_comparisons']:
            if 'error' in comp:
                print(f"  [{comp['nodeType']}] nodeId={comp['nodeId']}: {comp['error']}")
            else:
                drift_info = ""
                if comp['drift']:
                    drift_info = f" (pos_drift={comp['drift'].max_position_drift:.4f}m at idx {comp['drift'].max_drift_index})"
                print(f"  [{comp['nodeType']}] nodeId={comp['nodeId']}: {comp['old_point_count']} -> {comp['new_point_count']} pts{drift_info}")


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Compare old vs new gold data')
    parser.add_argument('--backup-dir', default='Assets/Tests/TrackData/backup',
                       help='Directory containing old gold data')
    parser.add_argument('--new-dir', default='Assets/Tests/TrackData',
                       help='Directory containing new gold data')
    parser.add_argument('-v', '--verbose', action='store_true',
                       help='Show detailed section comparisons')
    parser.add_argument('--files', nargs='*',
                       help='Specific files to compare (without .json extension)')
    args = parser.parse_args()

    backup_dir = Path(args.backup_dir)
    new_dir = Path(args.new_dir)

    if not backup_dir.exists():
        print(f"Error: Backup directory not found: {backup_dir}")
        sys.exit(1)

    if not new_dir.exists():
        print(f"Error: New data directory not found: {new_dir}")
        sys.exit(1)

    # Get files to compare
    if args.files:
        files = [f + '.json' for f in args.files]
    else:
        files = [f.name for f in backup_dir.glob('*.json')]

    print(f"Comparing gold data: {backup_dir} vs {new_dir}")

    all_ok = True
    for filename in files:
        old_path = backup_dir / filename
        new_path = new_dir / filename

        if not old_path.exists():
            print(f"Warning: Old file not found: {old_path}")
            continue

        if not new_path.exists():
            print(f"Warning: New file not found: {new_path}")
            continue

        try:
            result = compare_gold_files(old_path, new_path)
            print_comparison(result, verbose=args.verbose)

            # Check for concerning drift
            if result['overall_max_position_drift'] > 10.0:
                all_ok = False
        except Exception as e:
            print(f"Error comparing {filename}: {e}")
            all_ok = False

    print(f"\n{'='*60}")
    if all_ok:
        print("SUMMARY: All comparisons within acceptable drift")
    else:
        print("SUMMARY: Some files have concerning drift - review manually")

    return 0 if all_ok else 1


if __name__ == '__main__':
    sys.exit(main())
