#!/usr/bin/env python3
"""
Simulate TryFollowNext to understand why car positions are wrong.
"""
import math
from typing import Tuple, Optional
from dataclasses import dataclass
from analyze_kex import parse_kex, NodeType


def fmt_vec(v: Tuple[float, float, float]) -> str:
    return f"({v[0]:.3f}, {v[1]:.3f}, {v[2]:.3f})"


def distance(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


@dataclass
class Section:
    node_id: int
    facing: int
    start_pos: Tuple[float, float, float]
    end_pos: Tuple[float, float, float]  # Approximate
    start_dir: Tuple[float, float, float]
    arc_start: float
    arc_end: float
    priority: int
    prev_idx: int = -1
    next_idx: int = -1


def analyze_tryfollow(filepath: str):
    print("=" * 70)
    print("TryFollowNext SIMULATION")
    print("=" * 70)

    version, nodes, edges = parse_kex(filepath)

    SECTION_TYPES = {NodeType.ForceSection, NodeType.GeometricSection,
                     NodeType.CurvedSection, NodeType.CopyPathSection, NodeType.Bridge}

    sections = {}
    for sn in nodes:
        if sn.node.type not in SECTION_TYPES:
            continue
        anchor = sn.anchor
        facing = anchor.facing if anchor.facing != 0 else 1
        # Approximate end position (we don't have it in KEX, but for short sections it's close)
        # For the shuttle, sections are relatively straight
        direction = anchor.direction
        mag = math.sqrt(sum(d * d for d in direction))
        if mag > 0:
            direction = tuple(d / mag for d in direction)
        else:
            direction = (0, 0, 1)

        sections[sn.node.id] = Section(
            node_id=sn.node.id,
            facing=facing,
            start_pos=anchor.heart_position,
            end_pos=anchor.heart_position,  # Will be estimated
            start_dir=direction,
            arc_start=0,
            arc_end=50,  # Estimate
            priority=sn.node.priority,
        )

    # Get traversal order
    traversal = [s for s in sections.values() if s.priority >= 0]
    traversal.sort(key=lambda s: s.priority)

    print(f"\nTraversal sections: {len(traversal)}")
    for i, s in enumerate(traversal):
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  [{i}] Node {s.node_id} ({facing}) at {fmt_vec(s.start_pos)}")

    # Cosmetic sections
    cosmetic = [s for s in sections.values() if s.priority < 0]
    print(f"\nCosmetic sections: {len(cosmetic)}")
    for s in cosmetic:
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {s.node_id} ({facing}) at {fmt_vec(s.start_pos)}")

    if len(traversal) < 2:
        print("Not enough traversal sections")
        return

    last_section = traversal[-1]
    print(f"\n{'=' * 70}")
    print(f"LAST TRAVERSAL SECTION: Node {last_section.node_id}")
    print(f"{'=' * 70}")
    print(f"Facing: {'REV' if last_section.facing == -1 else 'FWD'}")
    print(f"START pos: {fmt_vec(last_section.start_pos)}")
    print(f"START dir: {fmt_vec(last_section.start_dir)}")

    # For shuttle, both sections share the same track
    # The FWD section START = REV section START = station position
    fwd_section = traversal[0]
    print(f"\nFWD section START: {fmt_vec(fwd_section.start_pos)}")
    print(f"REV section START: {fmt_vec(last_section.start_pos)}")

    # ourEnd for TryFollowNext is GetSectionEndPosition
    # For REV section sharing same spline as FWD, EndIndex is at far end
    # But we don't know exact position from KEX alone
    # However, geoDir tells us direction from START to END

    print(f"\n{'=' * 70}")
    print("Simulating TryFollowNext for overhang past ArcEnd")
    print(f"{'=' * 70}")

    # Find potential Next sections (cosmetic at far end)
    # The far end is in the direction of start_dir from start_pos
    far_end_dir = last_section.start_dir  # geo dir points toward END

    print(f"\nLooking for cosmetic sections at the far end (direction {fmt_vec(far_end_dir)})")

    # For each cosmetic, calculate distance to where ourEnd would be
    # ourEnd is somewhere along far_end_dir from start_pos
    # Without exact spline, we can look at start positions

    for cosmetic_s in cosmetic:
        cs_facing = "FWD" if cosmetic_s.facing == 1 else "REV"

        # Calculate approximate positions
        vec_to_cs = tuple(cosmetic_s.start_pos[i] - last_section.start_pos[i] for i in range(3))
        dist_to_cs = math.sqrt(sum(v * v for v in vec_to_cs))

        # Dot product with far_end_dir to see if it's in the right direction
        dot = sum(vec_to_cs[i] * far_end_dir[i] for i in range(3))

        print(f"\nCosmetic {cosmetic_s.node_id} ({cs_facing}):")
        print(f"  Position: {fmt_vec(cosmetic_s.start_pos)}")
        print(f"  Distance from last section START: {dist_to_cs:.2f}m")
        print(f"  Dot with far_end_dir: {dot:.2f} ({'far end' if dot > 0 else 'station side'})")
        print(f"  Direction: {fmt_vec(cosmetic_s.start_dir)}")

        # If this is at the far end and same geo direction
        dir_dot = sum(cosmetic_s.start_dir[i] * far_end_dir[i] for i in range(3))
        print(f"  Dir alignment: {dir_dot:.2f}")

        if dot > 10 and abs(dir_dot) > 0.9:
            print(f"  ** This is likely the far-end extension **")

            # Now simulate TryFollowNext
            # ourEnd is at section's EndIndex = far end position
            # For REV section, this is at high Z (away from station)

            # nextStart = cosmetic's StartIndex position
            # nextEnd = cosmetic's EndIndex position

            # If cosmetic is FWD and starts at far end:
            #   ourEnd ≈ nextStart (they connect)
            #   distToStart < distToEnd -> TRUE
            #   But our fix requires next.Facing == 1 for START match
            #   If cosmetic is FWD, this works!

            # If cosmetic is REV and starts at far end:
            #   ourEnd ≈ nextStart (they connect at cosmetic's START)
            #   distToStart < distToEnd -> TRUE
            #   Our fix requires next.Facing == 1 for START match -> FALSE
            #   Falls back to checking distToEnd < ENDPOINT_TOLERANCE -> FALSE
            #   Returns false -> Extrapolate

            if cosmetic_s.facing == 1:
                print("  TryFollowNext: Would match at START (FWD section), mappedArc = ArcStart + overhang")
            else:
                print("  TryFollowNext: Would REJECT (REV section at START)")
                print("  Falls back to Extrapolate(fromEnd=true)")
                print("  Extrapolate: position = endPos + geoDir * delta")
                print("  For REV section, geoDir points AWAY from station")
                print("  So car is placed further AWAY from station")


def main():
    analyze_tryfollow("Assets/Tests/Assets/shuttle.kex")

    print(f"\n{'=' * 70}")
    print("ANALYSIS")
    print(f"{'=' * 70}")
    print("""
The issue is:

1. Last section is REV, its geometric END is at far end (away from station)
2. Cosmetic extension is also REV, starts at far end
3. TryFollowNext tries to match:
   - ourEnd (last section's END) ≈ cosmetic's START
   - distToStart < distToEnd (connects at START)
   - But next.Facing == -1, so START match is REJECTED

4. Falls back to Extrapolate(fromEnd=true):
   - Gets anchor point at section's EndIndex (far end)
   - delta = targetArc - ArcEnd (positive)
   - result.Position = endPos + geoDir * delta

5. But geoDir is +Z (pointing toward END, which is far end)
   So the car is placed even FURTHER from station

6. This is actually CORRECT for arc-based positioning!
   Going past ArcEnd means going further in +arc direction
   For REV section, +arc = toward END = away from station

THE REAL ISSUE:

The test expects car 0 to be BEHIND COM in train direction.
But our arc calculation puts it AHEAD!

For REV section with facing=-1:
- offset = -6, facing = -1
- targetArc = baseArc + (-6) * (-1) = baseArc + 6
- This goes in +arc direction (toward ArcEnd and beyond)
- But train direction is -arc (toward ArcStart)
- So +arc direction = BACKWARD relative to train = BEHIND

The arc->position mapping should give a position that is:
- Further in +arc direction from COM
- Which for a REV section means further from ArcStart (toward END)
- Since END is at far end (high Z), the car should be at HIGHER Z than COM

But test shows car at LOWER Z than COM!

This means the position is being calculated WRONG somewhere.
""")


if __name__ == "__main__":
    main()
