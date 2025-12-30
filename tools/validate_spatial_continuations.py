#!/usr/bin/env python3
"""
Validate spatial continuation (Next/Prev) logic for overhang behavior.

EXPECTED SCENARIOS (all 9):

SHUTTLE:
1. Beginning (first Geo FWD) START -> Prev = cosmetic extension (Geo REV)
   Back cars overhang onto it.
2. Spike (CopyPath FWD before Reverse) END -> Next = cosmetic spike (Geo FWD)
   Front cars overhang onto it.
3. After reverse (CopyPath REV) START -> Prev = cosmetic spike (Geo FWD)
   Front cars overhang (now behind COM since facing flipped).
4. End (final CopyPath REV) END -> Next = cosmetic extension (Geo REV)
   Back cars overhang (now in front of COM since facing flipped).

VELOCI:
5. End (last Bridge) END -> Next = first Geo section (circuit completion)
   Front cars overhang completing the circuit.
6. Beginning (first Geo) START -> Prev = last Bridge (circuit completion)
   Back cars overhang completing the circuit.

SWITCH:
7. Spike (Geo FWD before Reverse) END -> Next = cosmetic spike (Geo FWD)
   Front cars overhang onto twisting spike.
8. After reverse (CopyPath REV) START -> Prev = cosmetic spike (Geo FWD)
   Front cars overhang (now behind COM since facing flipped).

ALL SCENARIOS:
9. Train positions should maintain rigidity (similar direction for continuations).

KEY INSIGHT:
Both Next and Prev can match EITHER target START or target END.
The only difference is the frame alignment criteria:
- Next: same direction (dirDot > 0.9)
- Prev: opposite direction (dirDot < -0.9)
We don't have a "reverse spike" case, but the logic handles it uniformly.
"""
import sys
import math
from typing import Tuple, List, Dict, Optional, NamedTuple
from dataclasses import dataclass
from analyze_kex import parse_kex, NodeType, PortType, get_node_type_name


def normalize(v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    mag = math.sqrt(v[0]**2 + v[1]**2 + v[2]**2)
    if mag < 1e-6:
        return (0.0, 0.0, 0.0)
    return (v[0]/mag, v[1]/mag, v[2]/mag)


def dot(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]


def cross(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> Tuple[float, float, float]:
    return (
        a[1]*b[2] - a[2]*b[1],
        a[2]*b[0] - a[0]*b[2],
        a[0]*b[1] - a[1]*b[0]
    )


def distance(p1: Tuple[float, float, float], p2: Tuple[float, float, float]) -> float:
    return math.sqrt(sum((a - b) ** 2 for a, b in zip(p1, p2)))


def fmt_vec(v: Tuple[float, float, float]) -> str:
    return f"({v[0]:.3f}, {v[1]:.3f}, {v[2]:.3f})"


@dataclass
class Frame:
    direction: Tuple[float, float, float]
    normal: Tuple[float, float, float]
    lateral: Tuple[float, float, float]

    def matches_for_continuation(self, other: 'Frame', threshold: float, is_next: bool) -> bool:
        dir_dot = dot(self.direction, other.direction)
        norm_dot = dot(self.normal, other.normal)
        lat_dot = dot(self.lateral, other.lateral)

        if is_next:
            return dir_dot > threshold and norm_dot > threshold and lat_dot > threshold
        else:
            return dir_dot < -threshold and norm_dot > threshold and lat_dot < -threshold


@dataclass
class Section:
    node_id: int
    node_type: NodeType
    priority: int
    facing: int
    start_pos: Tuple[float, float, float]
    start_dir: Tuple[float, float, float]
    start_normal: Tuple[float, float, float]
    start_lateral: Tuple[float, float, float]

    @property
    def is_traversal(self) -> bool:
        return self.priority >= 0

    @property
    def is_cosmetic(self) -> bool:
        return self.priority < 0

    def start_frame(self) -> Frame:
        return Frame(self.start_dir, self.start_normal, self.start_lateral)


class TrackAnalyzer:
    SECTION_TYPES = {NodeType.ForceSection, NodeType.GeometricSection,
                     NodeType.CurvedSection, NodeType.CopyPathSection, NodeType.Bridge}

    def __init__(self, filepath: str):
        self.filepath = filepath
        self.version, self.nodes, self.edges = parse_kex(filepath)
        self._build_structures()

    def _build_structures(self):
        self.port_to_node = {}
        self.node_by_id = {}
        for sn in self.nodes:
            self.node_by_id[sn.node.id] = sn
            for p in sn.input_ports:
                self.port_to_node[p.port.id] = (sn.node.id, True, p.port.type)
            for p in sn.output_ports:
                self.port_to_node[p.port.id] = (sn.node.id, False, p.port.type)

        self.anchor_graph = {}
        for edge in self.edges:
            src_info = self.port_to_node.get(edge.source_id)
            tgt_info = self.port_to_node.get(edge.target_id)
            if src_info and tgt_info:
                src_node_id, _, src_port_type = src_info
                tgt_node_id, _, tgt_port_type = tgt_info
                if src_port_type == PortType.Anchor and tgt_port_type == PortType.Anchor:
                    self.anchor_graph.setdefault(src_node_id, []).append(tgt_node_id)

        self.sections: Dict[int, Section] = {}
        for sn in self.nodes:
            if sn.node.type not in self.SECTION_TYPES:
                continue

            anchor = sn.anchor
            facing = anchor.facing if anchor.facing != 0 else 1
            direction = normalize(anchor.direction)
            normal = normalize(anchor.normal) if anchor.normal != (0, 0, 0) else (0.0, -1.0, 0.0)
            lateral = normalize(cross(normal, direction))

            self.sections[sn.node.id] = Section(
                node_id=sn.node.id,
                node_type=NodeType(sn.node.type),
                priority=sn.node.priority,
                facing=facing,
                start_pos=anchor.heart_position,
                start_dir=direction,
                start_normal=normal,
                start_lateral=lateral,
            )

        self.traversal_order = self._trace_traversal()

    def _trace_traversal(self) -> List[int]:
        visited = set()
        result = []

        def trace(node_id):
            if node_id in visited:
                return
            visited.add(node_id)

            sn = self.node_by_id.get(node_id)
            if not sn:
                return

            if sn.node.type in self.SECTION_TYPES and sn.node.priority >= 0:
                result.append(node_id)

            for next_id in self.anchor_graph.get(node_id, []):
                next_sn = self.node_by_id.get(next_id)
                if not next_sn:
                    continue
                if next_sn.node.type in {NodeType.Reverse, NodeType.ReversePath}:
                    for after in self.anchor_graph.get(next_id, []):
                        trace(after)
                elif next_sn.node.type in self.SECTION_TYPES:
                    trace(next_id)

        for sn in self.nodes:
            if sn.node.type == NodeType.Anchor:
                for next_id in self.anchor_graph.get(sn.node.id, []):
                    trace(next_id)
                break

        return result

    def get_section(self, node_id: int) -> Optional[Section]:
        return self.sections.get(node_id)

    def find_sections_at_position(self, pos: Tuple[float, float, float], tolerance: float = 0.1) -> List[Section]:
        result = []
        for section in self.sections.values():
            if distance(pos, section.start_pos) < tolerance:
                result.append(section)
        return result

    def find_node_before_reverse(self) -> Optional[int]:
        """Find the section node that connects to a Reverse node."""
        for node_id, targets in self.anchor_graph.items():
            for target_id in targets:
                target_sn = self.node_by_id.get(target_id)
                if target_sn and target_sn.node.type in {NodeType.Reverse, NodeType.ReversePath}:
                    if node_id in self.sections:
                        return node_id
        return None

    def find_node_after_reverse(self) -> Optional[int]:
        """Find the first section node after a Reverse node."""
        for sn in self.nodes:
            if sn.node.type in {NodeType.Reverse, NodeType.ReversePath}:
                for target_id in self.anchor_graph.get(sn.node.id, []):
                    if target_id in self.sections:
                        return target_id
        return None


def check_frame_match(source_frame: Frame, target_frame: Frame, is_next: bool, threshold: float = 0.9) -> Tuple[bool, str]:
    dir_dot = dot(source_frame.direction, target_frame.direction)
    norm_dot = dot(source_frame.normal, target_frame.normal)
    lat_dot = dot(source_frame.lateral, target_frame.lateral)

    if is_next:
        matches = dir_dot > threshold and norm_dot > threshold and lat_dot > threshold
        expected = "same direction"
    else:
        matches = dir_dot < -threshold and norm_dot > threshold and lat_dot < -threshold
        expected = "opposite direction"

    return matches, f"dirDot={dir_dot:.3f}, normDot={norm_dot:.3f}, latDot={lat_dot:.3f} ({expected})"


def analyze_shuttle(filepath: str) -> List[str]:
    print(f"\n{'='*70}")
    print("SHUTTLE COASTER ANALYSIS")
    print(f"File: {filepath}")
    print("="*70)

    analyzer = TrackAnalyzer(filepath)
    issues = []

    # Print all sections
    print(f"\nAll sections ({len(analyzer.sections)}):")
    for node_id in sorted(analyzer.sections.keys()):
        s = analyzer.sections[node_id]
        facing = "FWD" if s.facing == 1 else "REV"
        trav = "TRAV" if s.is_traversal else "COSMETIC"
        print(f"  Node {node_id}: {s.node_type.name} ({facing}, {trav})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    print(f"\nTraversal order ({len(analyzer.traversal_order)} sections):")
    for i, node_id in enumerate(analyzer.traversal_order):
        s = analyzer.get_section(node_id)
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  [{i}] Node {node_id}: {s.node_type.name} ({facing})")

    # Identify key sections
    cosmetic = [s for s in analyzer.sections.values() if s.is_cosmetic]
    print(f"\nCosmetic sections ({len(cosmetic)}):")
    for s in cosmetic:
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {s.node_id}: {s.node_type.name} ({facing}) at {fmt_vec(s.start_pos)}")
        if s.node_type == NodeType.CopyPathSection:
            print(f"       ** RED HERRING - this CopyPath cosmetic won't be matched (no frame alignment)")

    # Find specific sections for scenarios
    first_traversal = analyzer.traversal_order[0] if analyzer.traversal_order else None
    last_traversal = analyzer.traversal_order[-1] if analyzer.traversal_order else None
    before_reverse = analyzer.find_node_before_reverse()
    after_reverse = analyzer.find_node_after_reverse()

    print(f"\nKey sections:")
    print(f"  First traversal: Node {first_traversal}")
    print(f"  Before Reverse: Node {before_reverse}")
    print(f"  After Reverse: Node {after_reverse}")
    print(f"  Last traversal: Node {last_traversal}")

    # =========================================================================
    # SCENARIO 1: First traversal START -> Prev = cosmetic extension
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 1: First traversal START -> Prev")
    print("Expected: Cosmetic extension (Geo REV). Back cars overhang.")
    print("-"*50)

    if first_traversal:
        first = analyzer.get_section(first_traversal)
        print(f"Source: Node {first_traversal} START at {fmt_vec(first.start_pos)}")
        print(f"  Direction: {fmt_vec(first.start_dir)}")

        candidates = analyzer.find_sections_at_position(first.start_pos)
        print(f"\nCandidates (checking both START for opposite dir):")
        for cand in candidates:
            if cand.node_id == first_traversal:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"
            matches, details = check_frame_match(first.start_frame(), cand.start_frame(), is_next=False)
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START at {fmt_vec(cand.start_pos)}, dir={fmt_vec(cand.start_dir)}")
            print(f"    Frame match for Prev: {matches} - {details}")
            if matches and cand.is_cosmetic and cand.node_type == NodeType.GeometricSection:
                print(f"    ** EXPECTED Prev target **")

    # =========================================================================
    # SCENARIO 2: Before reverse END -> Next = cosmetic spike
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 2: Section before Reverse END -> Next")
    print("Expected: Cosmetic spike (Geo FWD). Front cars overhang.")
    print("-"*50)

    if before_reverse:
        before = analyzer.get_section(before_reverse)
        print(f"Source: Node {before_reverse} ({before.node_type.name})")
        print(f"  START: {fmt_vec(before.start_pos)}, dir={fmt_vec(before.start_dir)}")

        # For a FWD section going to Reverse, the END is where spike connects
        # The spike should be at the same position with same direction
        candidates = analyzer.find_sections_at_position(before.start_pos)
        print(f"\nCandidates at spike position (checking START for same dir):")
        for cand in candidates:
            if cand.node_id == before_reverse:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"
            # For Next, check same direction
            matches, details = check_frame_match(before.start_frame(), cand.start_frame(), is_next=True)
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START dir={fmt_vec(cand.start_dir)}")
            print(f"    Frame match for Next: {matches} - {details}")
            if matches and cand.is_cosmetic:
                print(f"    ** EXPECTED Next target (cosmetic spike) **")

    # =========================================================================
    # SCENARIO 3: After reverse START -> Prev = cosmetic spike
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 3: Section after Reverse START -> Prev")
    print("Expected: Cosmetic spike (same as scenario 2). Front cars overhang (now behind COM).")
    print("-"*50)

    if after_reverse:
        after = analyzer.get_section(after_reverse)
        print(f"Source: Node {after_reverse} ({after.node_type.name}, {'REV' if after.facing == -1 else 'FWD'})")
        print(f"  START: {fmt_vec(after.start_pos)}, dir={fmt_vec(after.start_dir)}")

        candidates = analyzer.find_sections_at_position(after.start_pos)
        print(f"\nCandidates (checking START for opposite dir):")
        for cand in candidates:
            if cand.node_id == after_reverse:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"
            matches, details = check_frame_match(after.start_frame(), cand.start_frame(), is_next=False)
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START dir={fmt_vec(cand.start_dir)}")
            print(f"    Frame match for Prev: {matches} - {details}")
            if matches and cand.is_cosmetic:
                print(f"    ** EXPECTED Prev target (cosmetic spike) **")

    # =========================================================================
    # SCENARIO 4: Last traversal END -> Next = cosmetic extension
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 4: Last traversal END -> Next")
    print("Expected: Cosmetic extension (Geo REV). Back cars overhang (now in front of COM).")
    print("-"*50)

    if last_traversal and first_traversal:
        last = analyzer.get_section(last_traversal)
        first = analyzer.get_section(first_traversal)
        # Shuttle ends where it started
        end_pos = first.start_pos

        print(f"Source: Node {last_traversal} ({last.node_type.name}, {'REV' if last.facing == -1 else 'FWD'})")
        print(f"  Expected END position (where shuttle started): {fmt_vec(end_pos)}")

        candidates = analyzer.find_sections_at_position(end_pos)
        print(f"\nCandidates (checking START for same dir):")
        for cand in candidates:
            if cand.node_id == last_traversal:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"

            # At shuttle end (REV), direction should be +Z
            # The cosmetic extension (REV) also has direction +Z at its START
            # So for Next, we check same direction
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START at {fmt_vec(cand.start_pos)}, dir={fmt_vec(cand.start_dir)}")

            if cand.is_cosmetic and cand.facing == -1 and cand.node_type == NodeType.GeometricSection:
                print(f"    ** EXPECTED Next target (cosmetic extension, matches by position and REV facing) **")
            elif cand.is_traversal:
                print(f"    (traversal section - should NOT be selected due to frame mismatch)")

    return issues


def analyze_veloci(filepath: str) -> List[str]:
    print(f"\n{'='*70}")
    print("VELOCI COASTER ANALYSIS")
    print(f"File: {filepath}")
    print("="*70)

    analyzer = TrackAnalyzer(filepath)
    issues = []

    print(f"\nAll sections ({len(analyzer.sections)}):")
    for node_id in sorted(analyzer.sections.keys()):
        s = analyzer.sections[node_id]
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {node_id}: {s.node_type.name} ({facing})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    print(f"\nTraversal order ({len(analyzer.traversal_order)} sections):")
    for i, node_id in enumerate(analyzer.traversal_order):
        s = analyzer.get_section(node_id)
        print(f"  [{i}] Node {node_id}: {s.node_type.name}")

    if len(analyzer.traversal_order) >= 2:
        first_id = analyzer.traversal_order[0]
        last_id = analyzer.traversal_order[-1]
        first = analyzer.get_section(first_id)
        last = analyzer.get_section(last_id)

        # =========================================================================
        # SCENARIO 5: Last section END -> Next = first section (circuit)
        # =========================================================================
        print("\n" + "-"*50)
        print("SCENARIO 5: Last section END -> Next")
        print("Expected: First Geo section (circuit completion). Front cars overhang.")
        print("-"*50)

        print(f"First section: Node {first_id} ({first.node_type.name})")
        print(f"  START: {fmt_vec(first.start_pos)}, dir={fmt_vec(first.start_dir)}")
        print(f"\nLast section: Node {last_id} ({last.node_type.name})")
        print(f"  START: {fmt_vec(last.start_pos)}")
        print(f"\nFor circuit completion, last END should connect to first START")
        print(f"  -> Next for last = first section (same direction at junction)")

        # =========================================================================
        # SCENARIO 6: First section START -> Prev = last section (circuit)
        # =========================================================================
        print("\n" + "-"*50)
        print("SCENARIO 6: First section START -> Prev")
        print("Expected: Last Bridge section (circuit completion). Back cars overhang.")
        print("-"*50)

        print(f"First section: Node {first_id}")
        print(f"  START: {fmt_vec(first.start_pos)}, dir={fmt_vec(first.start_dir)}")
        print(f"\nFor circuit completion:")
        print(f"  -> Prev for first = last section (opposite direction at junction)")

    return issues


def analyze_switch(filepath: str) -> List[str]:
    print(f"\n{'='*70}")
    print("SWITCH COASTER ANALYSIS")
    print(f"File: {filepath}")
    print("="*70)

    analyzer = TrackAnalyzer(filepath)
    issues = []

    print(f"\nAll sections ({len(analyzer.sections)}):")
    for node_id in sorted(analyzer.sections.keys()):
        s = analyzer.sections[node_id]
        facing = "FWD" if s.facing == 1 else "REV"
        trav = "TRAV" if s.is_traversal else "COSMETIC"
        print(f"  Node {node_id}: {s.node_type.name} ({facing}, {trav})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    print(f"\nTraversal order ({len(analyzer.traversal_order)} sections):")
    for i, node_id in enumerate(analyzer.traversal_order):
        s = analyzer.get_section(node_id)
        if s:
            facing = "FWD" if s.facing == 1 else "REV"
            print(f"  [{i}] Node {node_id}: {s.node_type.name} ({facing})")

    cosmetic = [s for s in analyzer.sections.values() if s.is_cosmetic]
    print(f"\nCosmetic sections ({len(cosmetic)}) - THE TWISTING SPIKE:")
    for s in cosmetic:
        facing = "FWD" if s.facing == 1 else "REV"
        print(f"  Node {s.node_id}: {s.node_type.name} ({facing})")
        print(f"       START: {fmt_vec(s.start_pos)}, dir={fmt_vec(s.start_dir)}")

    before_reverse = analyzer.find_node_before_reverse()
    after_reverse = analyzer.find_node_after_reverse()

    # =========================================================================
    # SCENARIO 7: Before reverse END -> Next = cosmetic spike
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 7: Section before Reverse END -> Next")
    print("Expected: Cosmetic twisting spike. Front cars overhang.")
    print("-"*50)

    if before_reverse:
        before = analyzer.get_section(before_reverse)
        print(f"Source: Node {before_reverse} ({before.node_type.name})")
        print(f"  START: {fmt_vec(before.start_pos)}, dir={fmt_vec(before.start_dir)}")

        candidates = analyzer.find_sections_at_position(before.start_pos)
        print(f"\nCandidates at spike position:")
        for cand in candidates:
            if cand.node_id == before_reverse:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"
            matches, details = check_frame_match(before.start_frame(), cand.start_frame(), is_next=True)
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START dir={fmt_vec(cand.start_dir)}")
            print(f"    Frame match for Next: {matches} - {details}")
            if matches and cand.is_cosmetic:
                print(f"    ** EXPECTED Next target (twisting cosmetic spike) **")

    # =========================================================================
    # SCENARIO 8: After reverse START -> Prev = cosmetic spike
    # =========================================================================
    print("\n" + "-"*50)
    print("SCENARIO 8: Section after Reverse START -> Prev")
    print("Expected: Cosmetic twisting spike. Front cars overhang (now behind COM).")
    print("-"*50)

    if after_reverse:
        after = analyzer.get_section(after_reverse)
        print(f"Source: Node {after_reverse} ({after.node_type.name}, {'REV' if after.facing == -1 else 'FWD'})")
        print(f"  START: {fmt_vec(after.start_pos)}, dir={fmt_vec(after.start_dir)}")

        candidates = analyzer.find_sections_at_position(after.start_pos)
        print(f"\nCandidates (checking START for opposite dir):")
        for cand in candidates:
            if cand.node_id == after_reverse:
                continue
            facing = "FWD" if cand.facing == 1 else "REV"
            trav = "COSMETIC" if cand.is_cosmetic else "TRAV"
            matches, details = check_frame_match(after.start_frame(), cand.start_frame(), is_next=False)
            print(f"  Node {cand.node_id} ({cand.node_type.name}, {facing}, {trav}):")
            print(f"    START dir={fmt_vec(cand.start_dir)}")
            print(f"    Frame match for Prev: {matches} - {details}")
            if matches and cand.is_cosmetic:
                print(f"    ** EXPECTED Prev target (twisting cosmetic spike) **")

    return issues


def main():
    print("="*70)
    print("SPATIAL CONTINUATION VALIDATION - ALL 9 SCENARIOS")
    print("="*70)
    print(__doc__)

    shuttle_path = "Assets/Tests/Assets/shuttle.kex"
    veloci_path = "Assets/Tests/Assets/veloci.kex"
    switch_path = "Assets/Tests/Assets/switch.kex"

    all_issues = []
    all_issues.extend(analyze_shuttle(shuttle_path))
    all_issues.extend(analyze_veloci(veloci_path))
    all_issues.extend(analyze_switch(switch_path))

    print("\n" + "="*70)
    print("SCENARIO 9: RIGIDITY VALIDATION")
    print("="*70)
    print("""
For all scenarios above, the frame alignment ensures rigidity:
- Same direction (Next): cars continue smoothly forward
- Opposite direction (Prev): cars continue smoothly backward

The frame match (dirDot, normDot, latDot) ensures that:
1. Direction aligns (same or opposite depending on continuation type)
2. Normal aligns (always same - no flipping upside down)
3. Lateral aligns (same for Next, opposite for Prev)

This guarantees that cars positioned via overhang will maintain
consistent spacing and orientation with the rest of the train.
""")

    print("\n" + "="*70)
    print("SUMMARY")
    print("="*70)

    print("""
KEY IMPLEMENTATION INSIGHT:

Both Next and Prev can match EITHER target START or target END.
The ONLY difference is the frame alignment requirement:
- Next: same direction (dirDot > 0.9, latDot > 0.9)
- Prev: opposite direction (dirDot < -0.9, latDot < -0.9)

Current implementation should:
1. Get source frame at the relevant endpoint (END for Next, START for Prev)
2. For each candidate section, check BOTH its START and END frames
3. Accept the candidate if frame alignment passes
4. Prefer dead-end sections, then closest distance
""")


if __name__ == "__main__":
    main()
