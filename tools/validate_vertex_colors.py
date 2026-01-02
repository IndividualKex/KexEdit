#!/usr/bin/env python3
"""Validate vertex colors in OBJ files for track pieces."""

import sys
from pathlib import Path

def validate_obj_vertex_colors(filepath: Path) -> dict:
    """Check if OBJ file has vertex colors and validate their values."""
    result = {
        "path": str(filepath),
        "total_vertices": 0,
        "vertices_with_colors": 0,
        "color_samples": [],
        "issues": []
    }

    with open(filepath, 'r') as f:
        for line_num, line in enumerate(f, 1):
            parts = line.strip().split()
            if not parts or parts[0] != 'v':
                continue

            result["total_vertices"] += 1

            if len(parts) >= 7:
                result["vertices_with_colors"] += 1
                try:
                    r, g, b = float(parts[4]), float(parts[5]), float(parts[6])

                    # Check if colors are in 0-1 range
                    for val, name in [(r, 'r'), (g, 'g'), (b, 'b')]:
                        if val < 0 or val > 1:
                            result["issues"].append(
                                f"Line {line_num}: {name}={val} out of 0-1 range"
                            )

                    # Sample first few colors
                    if len(result["color_samples"]) < 5:
                        result["color_samples"].append((r, g, b))

                except ValueError as e:
                    result["issues"].append(f"Line {line_num}: Failed to parse color: {e}")
            elif len(parts) >= 4:
                # Vertex without colors
                if result["vertices_with_colors"] == 0 and result["total_vertices"] <= 5:
                    result["issues"].append(
                        f"Line {line_num}: Vertex has no color data (only {len(parts)-1} values)"
                    )

    return result


def main():
    track_styles_dir = Path(__file__).parent.parent / "Assets" / "StreamingAssets" / "TrackStyles"

    if not track_styles_dir.exists():
        print(f"ERROR: Directory not found: {track_styles_dir}")
        sys.exit(1)

    obj_files = list(track_styles_dir.glob("classic_*.obj"))

    if not obj_files:
        print(f"ERROR: No classic_*.obj files found in {track_styles_dir}")
        sys.exit(1)

    print(f"Validating {len(obj_files)} OBJ files...\n")

    all_valid = True

    for obj_file in sorted(obj_files):
        result = validate_obj_vertex_colors(obj_file)

        print(f"=== {obj_file.name} ===")
        print(f"  Total vertices: {result['total_vertices']}")
        print(f"  Vertices with colors: {result['vertices_with_colors']}")

        if result["vertices_with_colors"] == 0:
            print("  WARNING: No vertex colors found!")
            print("  Expected format: v x y z r g b")
            print("  Actual format:   v x y z")
            all_valid = False
        elif result["vertices_with_colors"] < result["total_vertices"]:
            print(f"  WARNING: Only {result['vertices_with_colors']}/{result['total_vertices']} vertices have colors")
            all_valid = False
        else:
            print("  All vertices have colors")

        if result["color_samples"]:
            print(f"  Sample colors: {result['color_samples'][:3]}")

            # Check for expected solid red/green
            has_red = any(c[0] > 0.9 and c[1] < 0.1 and c[2] < 0.1 for c in result["color_samples"])
            has_green = any(c[1] > 0.9 and c[0] < 0.1 and c[2] < 0.1 for c in result["color_samples"])

            if has_red:
                print("  Found solid RED vertices")
            if has_green:
                print("  Found solid GREEN vertices")

        if result["issues"]:
            print("  Issues:")
            for issue in result["issues"][:5]:
                print(f"    - {issue}")
            all_valid = False

        print()

    if all_valid:
        print("All OBJ files have valid vertex colors.")
        sys.exit(0)
    else:
        print("VALIDATION FAILED: Some OBJ files are missing vertex colors.")
        print("\nTo fix this in Blender:")
        print("1. Ensure your mesh has vertex colors assigned")
        print("2. Export OBJ with 'Include Vertex Colors' enabled")
        print("   OR use the custom exporter script to include colors as 'v x y z r g b'")
        sys.exit(1)


if __name__ == "__main__":
    main()
