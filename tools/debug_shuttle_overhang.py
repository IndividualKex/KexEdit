#!/usr/bin/env python3
"""
Debug the shuttle coaster overhang issue at the end of traversal.

Key question: When a car overhangs past the END of the last REV section,
what direction does it end up facing?
"""
import sys
import math
from typing import Tuple, List, Dict, Optional
from dataclasses import dataclass
from analyze_kex import parse_kex, NodeType, PortType


def normalize(v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    mag = math.sqrt(v[0]**2 + v[1]**2 + v[2]**2)
    if mag < 1e-6:
        return (0.0, 0.0, 0.0)
    return (v[0]/mag, v[1]/mag, v[2]/mag)


def dot(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]


def fmt_vec(v: Tuple[float, float, float]) -> str:
    return f"({v[0]:.3f}, {v[1]:.3f}, {v[2]:.3f})"


def neg(v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    return (-v[0], -v[1], -v[2])


@dataclass
class Section:
    node_id: int
    node_type: NodeType
    priority: int
    facing: int  # 1=FWD, -1=REV
    start_pos: Tuple[float, float, float]
    end_pos: Tuple[float, float, float]  # We'll derive this
    start_dir: Tuple[float, float, float]
    end_dir: Tuple[float, float, float]  # We'll derive this
    prev_id: int
    next_id: int

    @property
    def is_cosmetic(self) -> bool:
        return self.priority < 0


def analyze_shuttle_overhang(filepath: str):
    print(f"\n{'='*70}")
    print("SHUTTLE OVERHANG DEBUG")
    print(f"{'='*70}")

    version, nodes, edges = parse_kex(filepath)

    # Build port -> node mapping
    port_to_node = {}
    node_by_id = {}
    for sn in nodes:
        node_by_id[sn.node.id] = sn
        for p in sn.input_ports:
            port_to_node[p.port.id] = (sn.node.id, True, p.port.type)
        for p in sn.output_ports:
            port_to_node[p.port.id] = (sn.node.id, False, p.port.type)

    # Build section list
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
            node_type=NodeType(sn.node.type),
            priority=sn.node.priority,
            facing=facing,
            start_pos=anchor.heart_position,
            end_pos=(0, 0, 0),  # Unknown from KEX
            start_dir=direction,
            end_dir=(0, 0, 0),  # Unknown from KEX
            prev_id=-1,
            next_id=-1,
        )

    print(f"\nTotal sections: {len(sections)}")

    # Find traversal order
    traversal_sections = [s for s in sections.values() if s.priority >= 0]
    traversal_sections.sort(key=lambda s: s.priority)
    print(f"Traversal sections: {len(traversal_sections)}")

    for i, s in enumerate(traversal_sections):
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  [{i}] Node {s.node_id} ({s.node_type.name}, {facing})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    if not traversal_sections:
        print("No traversal sections found!")
        return

    # Analyze the LAST traversal section
    last = traversal_sections[-1]
    print(f"\n{'='*70}")
    print("LAST TRAVERSAL SECTION ANALYSIS")
    print(f"{'='*70}")
    print(f"Section: Node {last.node_id} ({last.node_type.name})")
    print(f"Facing: {'REV' if last.facing == -1 else 'FWD'} ({last.facing})")
    print(f"START pos: {fmt_vec(last.start_pos)}")
    print(f"START dir: {fmt_vec(last.start_dir)}")

    # For a REV section:
    # - Geometric direction points from START to END
    # - Train direction is OPPOSITE (from END to START)
    train_dir = neg(last.start_dir) if last.facing == -1 else last.start_dir
    print(f"\nTrain direction at START: {fmt_vec(train_dir)}")

    # At the END of the shuttle (traversal end):
    # For a REV section, the traversal END is at the geometric START
    # Wait no - for REV, arc goes from ArcStart to ArcEnd same as geometry
    # But train travels from ArcEnd to ArcStart

    print(f"\nFor a REV section:")
    print(f"  - Arc goes from ArcStart to ArcEnd (same as geometry)")
    print(f"  - Train ENTERS at geometric END (high arc)")
    print(f"  - Train EXITS at geometric START (low arc)")
    print(f"  - So at traversal end, train is near geometric START")

    print(f"\n{'='*70}")
    print("WHAT HAPPENS WHEN CAR OVERHANGS PAST ARC_END?")
    print(f"{'='*70}")

    print("""
At the END of the shuttle traversal:
1. Train is on the last REV section
2. COM is near section.ArcEnd (geometric END)
3. follower.Facing = -1 (flipped after Reverse)

For the FRONT cars (car 0, which is now BEHIND the COM due to reversal):
- offset = 0 - halfSpan = negative
- targetArc = baseArc + offset * follower.Facing
- targetArc = baseArc + negative * (-1) = baseArc + positive
- So front cars go toward HIGHER arc (toward ArcEnd and beyond)

If targetArc > section.ArcEnd:
- TryFollowNext is called
- ourEnd = geometric END position
- nextStart/nextEnd = positions of section.Next
""")

    # Find cosmetic sections that could be Next
    cosmetic = [s for s in sections.values() if s.is_cosmetic]
    print(f"\nCosmetic sections that could be Next:")
    for s in cosmetic:
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {s.node_id} ({s.node_type.name}, {facing})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    # The key question
    print(f"\n{'='*70}")
    print("THE KEY QUESTION")
    print(f"{'='*70}")

    # Find the cosmetic extension at the station
    station_pos = (0.0, 3.0, 0.0)  # From analysis
    cosmetic_at_station = None
    for s in cosmetic:
        dist = math.sqrt(sum((a-b)**2 for a, b in zip(s.start_pos, station_pos)))
        if dist < 0.1 and s.node_type == NodeType.GeometricSection:
            cosmetic_at_station = s
            break

    if cosmetic_at_station:
        s = cosmetic_at_station
        print(f"\nCosmetic extension at station: Node {s.node_id}")
        print(f"  Facing: {'REV' if s.facing == -1 else 'FWD'} ({s.facing})")
        print(f"  START dir: {fmt_vec(s.start_dir)}")

        # When we sample this section, what direction do we get?
        print(f"\n  When sampling this section:")
        print(f"    SplinePoint.Direction = geometric direction = {fmt_vec(s.start_dir)}")

        # What direction SHOULD the car have?
        # The car was on the last REV section, traveling toward the station
        # The last REV section has START dir pointing AWAY from station (+Z)
        # So train direction = -START dir = -Z
        expected_car_dir = neg(last.start_dir) if last.facing == -1 else last.start_dir
        print(f"\n  What direction should the overhanging car have?")
        print(f"    Last section train direction: {fmt_vec(expected_car_dir)}")

        # Does the current code flip?
        last_facing = last.facing
        next_facing = s.facing
        will_flip = last_facing != next_facing

        print(f"\n  Current code logic:")
        print(f"    last.Facing = {last_facing}")
        print(f"    next.Facing = {next_facing}")
        print(f"    Will flip? {will_flip}")

        if will_flip:
            sampled_dir = neg(s.start_dir)
        else:
            sampled_dir = s.start_dir

        print(f"\n  Result:")
        print(f"    Sampled direction (after flip logic): {fmt_vec(sampled_dir)}")
        print(f"    Expected direction: {fmt_vec(expected_car_dir)}")

        dir_dot = dot(sampled_dir, expected_car_dir)
        print(f"    Dot product: {dir_dot:.3f}")

        if dir_dot > 0.9:
            print(f"    ✓ CORRECT - directions match")
        elif dir_dot < -0.9:
            print(f"    ✗ WRONG - directions are OPPOSITE!")
            print(f"\n    THIS IS THE BUG!")
            print(f"    The car is facing the wrong way, causing it to overlap with other cars.")
        else:
            print(f"    ? Directions don't match clearly")


def main():
    shuttle_path = "Assets/Tests/Assets/shuttle.kex"
    analyze_shuttle_overhang(shuttle_path)

    print(f"\n{'='*70}")
    print("ANALYSIS OF THE ISSUE")
    print(f"{'='*70}")
    print("""
The problem is in how we determine if we should flip the direction.

Current logic in TryFollowNext:
  if (section.Facing != next.Facing) { flip direction }

This means:
  REV -> REV: no flip
  REV -> FWD: flip
  FWD -> REV: flip
  FWD -> FWD: no flip

But what we ACTUALLY need depends on the train direction, not the section facing!

At the end of shuttle:
- Last section is REV (Facing = -1)
- Train is traveling in the direction of decreasing arc (toward ArcStart)
- But we're looking for overhang past ArcEnd (toward increasing arc)

Wait... this is confusing. Let me think about the coordinate system.

For a REV section:
- Geometric direction goes from START to END
- ArcStart is at geometric START, ArcEnd is at geometric END
- But TRAIN travels from END to START (opposite direction)

At shuttle end:
- COM is near the station (geometric END of last section)
- baseArc is near section.ArcEnd
- Front cars (negative offset) with follower.Facing=-1:
  targetArc = baseArc + (-6) * (-1) = baseArc + 6 > ArcEnd
- These cars go PAST ArcEnd toward higher arc values

TryFollowNext uses ourEnd (geometric END) to find matches.

WAIT. I think the issue is that for REV sections:
- The "end" in traversal terms is the geometric START
- But GetSectionEndPosition returns the geometric END

So TryFollowNext is looking at the WRONG endpoint!
""")


if __name__ == "__main__":
    main()
