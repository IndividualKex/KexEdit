#!/usr/bin/env python3
"""
Debug script to understand the overhang direction bug.

THE CORE ISSUE:
When a car overhangs from section A to section B, the overhang samples B's spline.
The arc mapping determines WHERE on B's spline to sample.

For Next (car goes past ArcEnd):
- TryFollowNext connects at either B's START or B's END
- If connecting at B's START: mappedArc = ArcStart + overhang (goes +arc direction)
- If connecting at B's END: mappedArc = ArcEnd - overhang (goes -arc direction)

But the overhang should go in the TRAIN direction, not necessarily the arc direction!

For FWD sections: train direction = +arc direction
For REV sections: train direction = -arc direction

So:
- Connect at START, B is FWD: +arc = train dir. CORRECT!
- Connect at START, B is REV: +arc != train dir. WRONG!
- Connect at END, B is FWD: -arc != train dir. WRONG!
- Connect at END, B is REV: -arc = train dir. CORRECT!

THE FIX:
For Next: only accept START matches for FWD targets, END matches for REV targets.
For Prev: the opposite (since overhang goes in -train direction).
"""
import math
from dataclasses import dataclass
from typing import Tuple

@dataclass
class Scenario:
    name: str
    current_facing: int  # 1=FWD, -1=REV
    target_facing: int
    connect_at: str  # "START" or "END"

    def analyze(self):
        # Current section train direction at exit
        # (For simplicity, assume geo direction is +Z at the junction)
        current_train_dir = self.current_facing  # +1 or -1 (representing +Z or -Z)

        # Target section train direction at connection point
        target_train_dir = self.target_facing

        # Do train directions match? (Required for Next)
        train_dirs_match = (current_train_dir == target_train_dir)

        # Arc direction at connection point
        # At START: arc increases toward END, so +arc direction = geo direction = +1
        # At END: arc increases from START, so +arc direction = geo direction = +1
        # (arc direction is always +geo direction)
        arc_dir = 1  # +Z

        # Overhang direction (same as train direction for Next)
        overhang_dir = current_train_dir

        # Arc mapping direction
        if self.connect_at == "START":
            # mappedArc = ArcStart + overhang, goes in +arc direction
            arc_mapping_dir = +1
        else:
            # mappedArc = ArcEnd - overhang, goes in -arc direction
            arc_mapping_dir = -1

        # Does the arc mapping go in the overhang direction?
        arc_mapping_correct = (arc_mapping_dir == overhang_dir)

        return train_dirs_match, arc_mapping_correct


def main():
    print("=" * 70)
    print("OVERHANG DIRECTION ANALYSIS")
    print("=" * 70)
    print(__doc__)

    print("\n" + "=" * 70)
    print("NEXT OVERHANG SCENARIOS (car goes past ArcEnd)")
    print("=" * 70)
    print()

    scenarios = [
        Scenario("FWD current -> FWD target at START", 1, 1, "START"),
        Scenario("FWD current -> FWD target at END", 1, 1, "END"),
        Scenario("FWD current -> REV target at START", 1, -1, "START"),
        Scenario("FWD current -> REV target at END", 1, -1, "END"),
        Scenario("REV current -> FWD target at START", -1, 1, "START"),
        Scenario("REV current -> FWD target at END", -1, 1, "END"),
        Scenario("REV current -> REV target at START", -1, -1, "START"),
        Scenario("REV current -> REV target at END", -1, -1, "END"),
    ]

    print(f"{'Scenario':<45} {'Train Match':<15} {'Arc Correct':<15} {'Result'}")
    print("-" * 90)

    for s in scenarios:
        train_match, arc_correct = s.analyze()

        if train_match and arc_correct:
            result = "OK - Valid match"
        elif train_match and not arc_correct:
            result = "BUG! Train matches but arc is wrong"
        elif not train_match and arc_correct:
            result = "N/A - Train doesn't match"
        else:
            result = "N/A - Nothing matches"

        print(f"{s.name:<45} {str(train_match):<15} {str(arc_correct):<15} {result}")

    print()
    print("=" * 70)
    print("CONCLUSION")
    print("=" * 70)
    print("""
The bug occurs in scenarios where:
- Train directions match (so FindSpatialMatch accepts the match)
- But arc mapping is in the wrong direction (so the car ends up on wrong side)

BUG SCENARIOS:
1. FWD current -> FWD target at END
2. REV current -> REV target at START  <-- THIS IS THE SHUTTLE BUG

THE FIX:
In FindSpatialMatch or TryFollowNext, reject matches where:
- Connecting at target START and target is REV
- Connecting at target END and target is FWD

Or equivalently, only accept:
- START matches where target.Facing == 1 (FWD)
- END matches where target.Facing == -1 (REV)
""")


if __name__ == "__main__":
    main()
