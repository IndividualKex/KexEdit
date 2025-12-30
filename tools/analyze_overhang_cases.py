#!/usr/bin/env python3
"""
Analyze overhang cases for veloci and switch tracks.
This script examines:
1. Section layout and traversal order
2. Next/Prev relationships
3. Start/End positions and directions
4. Expected overhang behavior
"""
import math
from typing import Tuple, Dict, List, Optional
from dataclasses import dataclass
from analyze_kex import parse_kex, NodeType


def fmt_vec(v: Tuple[float, float, float]) -> str:
    return f"({v[0]:.2f}, {v[1]:.2f}, {v[2]:.2f})"


def distance(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def normalize(v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    mag = math.sqrt(sum(x * x for x in v))
    if mag < 1e-6:
        return (0.0, 0.0, 0.0)
    return (v[0] / mag, v[1] / mag, v[2] / mag)


def dot(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return sum(x * y for x, y in zip(a, b))


@dataclass
class Section:
    node_id: int
    node_type: str
    priority: int
    facing: int  # 1=FWD, -1=REV
    start_pos: Tuple[float, float, float]
    start_dir: Tuple[float, float, float]


def get_section_type_name(node_type: NodeType) -> str:
    names = {
        NodeType.ForceSection: "Force",
        NodeType.GeometricSection: "Geo",
        NodeType.CurvedSection: "Curved",
        NodeType.CopyPathSection: "CopyPath",
        NodeType.Bridge: "Bridge",
    }
    return names.get(node_type, str(node_type))


def analyze_track(filepath: str, track_name: str):
    print(f"\n{'=' * 80}")
    print(f"TRACK ANALYSIS: {track_name}")
    print(f"{'=' * 80}")

    version, nodes, edges = parse_kex(filepath)

    SECTION_TYPES = {NodeType.ForceSection, NodeType.GeometricSection,
                     NodeType.CurvedSection, NodeType.CopyPathSection, NodeType.Bridge}

    sections: Dict[int, Section] = {}
    for sn in nodes:
        if sn.node.type not in SECTION_TYPES:
            continue
        anchor = sn.anchor
        facing = anchor.facing if anchor.facing != 0 else 1
        direction = normalize(anchor.direction)

        sections[sn.node.id] = Section(
            node_id=sn.node.id,
            node_type=get_section_type_name(sn.node.type),
            priority=sn.node.priority,
            facing=facing,
            start_pos=anchor.heart_position,
            start_dir=direction,
        )

    # Separate traversal and cosmetic sections
    traversal = sorted([s for s in sections.values() if s.priority >= 0], key=lambda s: s.priority)
    cosmetic = [s for s in sections.values() if s.priority < 0]

    print(f"\n--- TRAVERSAL ORDER ({len(traversal)} sections) ---")
    for i, s in enumerate(traversal):
        facing_str = "FWD" if s.facing == 1 else "REV"
        print(f"  [{i}] Node {s.node_id} ({s.node_type}, {facing_str})")
        print(f"       Priority: {s.priority}")
        print(f"       START pos: {fmt_vec(s.start_pos)}")
        print(f"       START dir: {fmt_vec(s.start_dir)}")

    print(f"\n--- COSMETIC SECTIONS ({len(cosmetic)} sections) ---")
    for s in cosmetic:
        facing_str = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {s.node_id} ({s.node_type}, {facing_str})")
        print(f"       START pos: {fmt_vec(s.start_pos)}")
        print(f"       START dir: {fmt_vec(s.start_dir)}")

    # Analyze connections
    print(f"\n--- CONNECTION ANALYSIS ---")

    # For each traversal section, find what should be its Prev and Next
    for i, s in enumerate(traversal):
        print(f"\nSection [{i}] Node {s.node_id} ({s.node_type}, {'FWD' if s.facing == 1 else 'REV'}):")

        # For overhang, we care about:
        # - Prev: where cars overhang BEFORE this section's start (in -geoDir)
        # - Next: where cars overhang PAST this section's end (in +geoDir)

        # Find potential Prev (sections whose end is near our start)
        # Since we only have start positions, we need to estimate or look at spatial proximity

        # For now, analyze based on traversal order and spatial proximity
        prev_candidates = []
        next_candidates = []

        for other in list(traversal) + cosmetic:
            if other.node_id == s.node_id:
                continue

            dist = distance(s.start_pos, other.start_pos)
            dir_dot = dot(s.start_dir, other.start_dir)

            # Check if other's start is near our start (potential Prev connecting at our start)
            if dist < 1.0:
                prev_candidates.append((other, dist, "start-start", dir_dot))

        if prev_candidates:
            print(f"  Potential Prev connections (at our START):")
            for other, dist, conn_type, dir_dot in sorted(prev_candidates, key=lambda x: x[1]):
                facing_str = "FWD" if other.facing == 1 else "REV"
                cosmetic_str = " [COSMETIC]" if other.priority < 0 else ""
                print(f"    - Node {other.node_id} ({other.node_type}, {facing_str}){cosmetic_str}")
                print(f"      Distance: {dist:.3f}m, DirDot: {dir_dot:.3f}")

    return traversal, cosmetic


def analyze_veloci():
    print("\n" + "=" * 80)
    print("VELOCI ANALYSIS - Complete Circuit")
    print("=" * 80)

    traversal, cosmetic = analyze_track("Assets/Tests/Assets/veloci.kex", "Veloci")

    print("\n--- VELOCI OVERHANG EXPECTATIONS ---")
    print("""
    Veloci is a COMPLETE CIRCUIT (loop track).

    Key scenario: At the END of the last traversal section (Bridge),
    the front cars should overhang onto the FIRST section (completing the loop).

    Expected behavior:
    - Last section's Next should point to the first section
    - When car overhangs past last section's ArcEnd, it should sample the first section
    - This requires ourEnd (last section END) to be near firstSection's START

    Current issue: Front cars extrapolate linearly instead of following the first section.

    Possible causes:
    1. Next pointer not set (Track.Build doesn't create circuit connections?)
    2. TryFollowNext fails because ourEnd is not near nextStart
    3. Connection exists but my fix rejects it incorrectly
    """)

    if len(traversal) >= 2:
        first = traversal[0]
        last = traversal[-1]

        print(f"\nCircuit connection analysis:")
        print(f"  First section: Node {first.node_id} ({first.node_type})")
        print(f"    START pos: {fmt_vec(first.start_pos)}")
        print(f"    START dir: {fmt_vec(first.start_dir)}")
        print(f"    Facing: {'FWD' if first.facing == 1 else 'REV'}")

        print(f"\n  Last section: Node {last.node_id} ({last.node_type})")
        print(f"    START pos: {fmt_vec(last.start_pos)}")
        print(f"    START dir: {fmt_vec(last.start_dir)}")
        print(f"    Facing: {'FWD' if last.facing == 1 else 'REV'}")

        # For a complete circuit, last section's END should connect to first section's START
        # We don't have END positions, but we can estimate based on direction
        print(f"\n  Distance first.start to last.start: {distance(first.start_pos, last.start_pos):.2f}m")
        print(f"  Direction alignment: {dot(first.start_dir, last.start_dir):.3f}")


def analyze_switch():
    print("\n" + "=" * 80)
    print("SWITCH ANALYSIS - CopyPath with Cosmetic Spike")
    print("=" * 80)

    traversal, cosmetic = analyze_track("Assets/Tests/Assets/switch.kex", "Switch")

    print("\n--- SWITCH OVERHANG EXPECTATIONS ---")
    print("""
    Switch track has a cosmetic spike after the reverse node.

    Key scenario: After reversing onto the CopyPath section (REV),
    the front cars (now physically at back due to reversal) should overhang
    onto the cosmetic spike (Prev direction).

    Expected behavior:
    - CopyPath section's Prev should point to the cosmetic spike
    - When car overhangs before CopyPath's ArcStart, it should sample the spike
    - This requires ourStart (CopyPath START) to be near spike's END

    Current issue: Front cars extrapolate linearly instead of following the spike.

    Possible causes:
    1. Prev pointer not set correctly
    2. TryFollowPrev fails because ourStart is not near prevEnd
    3. My fix requires connections at Prev's END, but spike connects at START?
    """)

    # Find the CopyPath section
    copypath = None
    for s in traversal:
        if s.node_type == "CopyPath":
            copypath = s
            break

    if copypath:
        print(f"\nCopyPath section analysis:")
        print(f"  Node {copypath.node_id}")
        print(f"  START pos: {fmt_vec(copypath.start_pos)}")
        print(f"  START dir: {fmt_vec(copypath.start_dir)}")
        print(f"  Facing: {'FWD' if copypath.facing == 1 else 'REV'}")

        # Find cosmetic sections near CopyPath's start (potential Prev)
        print(f"\n  Cosmetic sections near CopyPath START:")
        for c in cosmetic:
            dist = distance(copypath.start_pos, c.start_pos)
            if dist < 50:  # Within 50m
                dir_dot = dot(copypath.start_dir, c.start_dir)
                facing_str = "FWD" if c.facing == 1 else "REV"
                print(f"    - Node {c.node_id} ({c.node_type}, {facing_str})")
                print(f"      START pos: {fmt_vec(c.start_pos)}")
                print(f"      Distance to CopyPath.start: {dist:.2f}m")
                print(f"      Direction alignment: {dir_dot:.3f}")


def main():
    analyze_veloci()
    analyze_switch()

    print("\n" + "=" * 80)
    print("KEY INSIGHT: My TryFollowNext/Prev Fix")
    print("=" * 80)
    print("""
    My current fix:
    - TryFollowNext: ONLY accepts connections where ourEnd ~= nextStart
    - TryFollowPrev: ONLY accepts connections where ourStart ~= prevEnd

    This works for the shuttle case where:
    - Last section END is at Z=0
    - Next section START is at Z=-44.95 (far away, not a match)
    - Next section END is at Z=0 (close, but should be rejected)

    But this might BREAK other cases where:
    - The Prev/Next section connects at its START (for Prev) or END (for Next)
    - Such connections ARE valid in certain geometric configurations

    The issue is that my fix is TOO RESTRICTIVE.

    The CORRECT logic should consider:
    1. WHERE we connect (our START/END to their START/END)
    2. What DIRECTION the overhang goes spatially
    3. Whether the arc mapping preserves that spatial direction
    """)


if __name__ == "__main__":
    main()
