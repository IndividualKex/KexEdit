//! Analytical invariants for the FVD physics integrator.
//!
//! Each test pins a property derivable from physics first principles
//! (energy conservation, projectile motion, circular motion, etc.) without
//! depending on FVD++/external reference data. Tolerances are derived from
//! the integrator's order × step size (100 Hz fixed step), not tuned to pass.
//!
//! Setup convention: heart_offset is set to 0 across all tests, which
//! collapses spine_position = heart_position so analytical predictions match
//! the heart-position frame directly. Friction and resistance are 0 unless
//! the invariant explicitly involves them.

use kexengine::nodes::{
    anchor, copy_path::CopyPathNode, curved::CurvedNode, force, geometric, reverse, reverse_path,
    DurationType, IterationConfig,
};
use kexengine::sim::{physics, Float3, Keyframe, Point};
use std::f32::consts::PI;

const HEART: f32 = 0.0;

/// Build an anchor with default (BACK) direction and zero pitch/yaw/roll.
/// `position` sets the heart-position; `velocity` sets the initial speed.
fn flat_anchor(position: Float3, velocity: f32) -> Point {
    anchor::build(position, 0.0, 0.0, 0.0, velocity, HEART, 0.0, 0.0)
}

fn k(value: f32) -> Vec<Keyframe> {
    vec![Keyframe::simple(0.0, value)]
}

/// Run a Force-driven section with constant target normal/lateral forces.
/// `driven_velocity` pinned constant when `driven=true`.
#[allow(clippy::too_many_arguments)]
fn run_force(
    anchor_point: &Point,
    duration_s: f32,
    target_normal: f32,
    target_lateral: f32,
    driven: bool,
    driven_velocity: f32,
) -> Vec<Point> {
    let cfg = IterationConfig::new(duration_s, DurationType::Time);
    force::build(
        anchor_point,
        &cfg,
        driven,
        &k(0.0),                // roll_speed
        &k(target_normal),      // normal_force
        &k(target_lateral),     // lateral_force
        &k(driven_velocity),    // driven_velocity (used only if driven)
        &k(HEART),              // heart_offset
        &k(0.0),                // friction
        &k(0.0),                // resistance
        HEART,
        0.0,
        0.0,
    )
}

/// Run a Geometric-driven section. Constant pitch/yaw/roll speeds.
#[allow(clippy::too_many_arguments)]
fn run_geometric(
    anchor_point: &Point,
    duration_s: f32,
    roll_speed: f32,
    pitch_speed: f32,
    yaw_speed: f32,
    driven: bool,
    driven_velocity: f32,
) -> Vec<Point> {
    let cfg = IterationConfig::new(duration_s, DurationType::Time);
    geometric::build(
        anchor_point,
        &cfg,
        driven,
        false, // steering
        &k(roll_speed),
        &k(pitch_speed),
        &k(yaw_speed),
        &k(driven_velocity),
        &k(HEART),
        &k(0.0),
        &k(0.0),
        HEART,
        0.0,
        0.0,
    )
}

// ---------------------------------------------------------------------------
// 1. Free fall: projectile motion under gravity matches ½gt²
// ---------------------------------------------------------------------------
//
// Setup: anchor with horizontal direction (BACK) at origin, v0 = 10 m/s,
// Force node with normal = lateral = 0 (no track reaction force), no friction.
//
// Analytical: with body-frame forces zero, only world gravity acts. The path
// is a parabola in the y–z plane:
//   z(t) = -v0·t              (BACK direction is −z)
//   y(t) = -½·g·t²
//
// Tolerance: position advances by the trapezoidal average of prev/curr
// direction × v·dt. Per-step displacement truncation goes as dt²·d²r/dt² ≈
// dt²·g·sin θ(t), with the projectile pitch θ(t) ≈ g·t/v0. Integrating to
// time T gives a cumulative position error ~ dt·g²·T²/(2·v0). For T = 0.49 s,
// dt = 0.01 s, v0 = 10, g = 9.81: bound ≈ 0.012 m. Use 0.02 m (≈2× the
// derived bound) to absorb the small frame-rotation coupling we're ignoring
// in the leading-order estimate.

#[test]
fn free_fall_matches_projectile_trajectory() {
    let v0 = 10.0;
    let t_target = 0.5;
    let anchor = flat_anchor(Float3::ZERO, v0);

    let path = run_force(&anchor, t_target, 0.0, 0.0, false, v0);

    // Time-section emits points at i/HZ for i=0..⌊HZ·duration⌋−1.
    // The last point's time is (path.len()-1)/HZ.
    let last = path.last().expect("path should have points");
    let t = (path.len() - 1) as f32 / physics::HZ;

    let expected_z = -v0 * t;
    let expected_y = -0.5 * physics::G * t * t;

    let pos_tol = 0.02; // derived in test docstring above
    assert!(
        (last.heart_position.z - expected_z).abs() < pos_tol,
        "z={} expected≈{} (t={})",
        last.heart_position.z,
        expected_z,
        t
    );
    assert!(
        (last.heart_position.y - expected_y).abs() < pos_tol,
        "y={} expected≈{} (t={})",
        last.heart_position.y,
        expected_y,
        t
    );
    // x stays zero (no lateral force, no lateral gravity component).
    assert!(last.heart_position.x.abs() < 1e-4);
}

// ---------------------------------------------------------------------------
// 2. Energy conservation: frictionless free fall preserves total energy
// ---------------------------------------------------------------------------
//
// Setup: same projectile as test 1.
//
// Analytical: with no friction or drag, total mechanical energy is conserved:
//   ½·v(t)² = ½·v0² + g·|Δy|     ⇒    v(t)² = v0² + 2·g·|Δy|
//
// The integrator's `update_velocity` is *defined* by this energy form
// (delta-based KE update with delta_PE = g·Δy), so the equation should hold
// to floating-point precision for the segment-by-segment update. The only
// drift is from accumulating Δy across many segments via finite-precision
// addition.
//
// Tolerance: 1e-3 (m/s)² over ~50 steps of f32 accumulation. Generous; we
// expect << 1e-4 in practice.

#[test]
fn free_fall_conserves_energy() {
    let v0 = 10.0;
    let anchor = flat_anchor(Float3::ZERO, v0);

    let path = run_force(&anchor, 0.5, 0.0, 0.0, false, v0);

    for p in &path {
        let drop = -p.heart_position.y; // positive drop distance
        let expected_v_sq = v0 * v0 + 2.0 * physics::G * drop;
        let actual_v_sq = p.velocity * p.velocity;
        assert!(
            (actual_v_sq - expected_v_sq).abs() < 1e-3,
            "v²={} expected≈{} at drop={}",
            actual_v_sq,
            expected_v_sq,
            drop
        );
    }
}

// ---------------------------------------------------------------------------
// 3. Constant flat track: N=1, L=0 → straight line, constant speed
// ---------------------------------------------------------------------------
//
// Setup: horizontal anchor (BACK), v0 = 10. Force node with N=1 (cancels
// gravity), L=0, friction = resistance = 0.
//
// Analytical: net body-frame force is exactly zero, so direction never
// rotates and speed never changes. Trajectory is the straight line z = -v0·t,
// y = 0.
//
// Tolerance: 1e-4. With zero force the integrator should reproduce the
// trivial solution to f32 precision.

#[test]
fn flat_track_is_straight_at_constant_speed() {
    let v0 = 10.0;
    let anchor = flat_anchor(Float3::ZERO, v0);

    let path = run_force(&anchor, 1.0, 1.0, 0.0, false, v0);

    for p in &path {
        assert!(p.heart_position.y.abs() < 1e-4, "y drifted: {}", p.heart_position.y);
        assert!(p.heart_position.x.abs() < 1e-4, "x drifted: {}", p.heart_position.x);
        assert!(
            (p.velocity - v0).abs() < 1e-4,
            "speed drifted: {} vs {}",
            p.velocity,
            v0
        );
    }
    let last = path.last().unwrap();
    let t = (path.len() - 1) as f32 / physics::HZ;
    assert!(
        (last.heart_position.z - (-v0 * t)).abs() < 1e-3,
        "z={} expected={}",
        last.heart_position.z,
        -v0 * t
    );
}

// ---------------------------------------------------------------------------
// 4. Closed loop: Curved arc=360° returns to start
// ---------------------------------------------------------------------------
//
// Setup: Curved node, radius r = 10, arc = 360°, axis = 0 (curve around the
// frame's normal axis → for default BACK/DOWN/RIGHT this is the world UP
// axis: a horizontal flat circle). Driven at constant velocity v = 10 to
// avoid speed/gravity coupling. Friction = resistance = 0.
//
// Analytical: a closed circle of circumference 2πr returns to its start
// position. Heart-arc accumulates to ≈ 2πr.
//
// Tolerance: per-step yaw is dα = v/(r·HZ) ≈ 0.01 rad ≈ 0.573°. The loop
// exits as soon as accumulated angle ≥ 360°, so the final point overshoots
// the target by up to one step. The dominant residual is therefore that
// overshoot, ~ r·dα ≈ 0.1 m, plus a sub-millimetre chord-trapezoidal
// truncation per step. Use 0.2 m position tolerance and 0.15 m on heart_arc
// (the arc accumulates one extra v/HZ step on overshoot).

#[test]
fn full_circle_loop_returns_to_start() {
    let v = 10.0;
    let r = 10.0;
    let anchor = flat_anchor(Float3::ZERO, v);

    let path = CurvedNode::build(
        &anchor,
        r,
        360.0, // arc in degrees
        0.0,   // axis
        0.0,   // lead_in
        0.0,   // lead_out
        true,  // driven
        &k(0.0),
        &k(v),
        &k(HEART),
        &k(0.0),
        &k(0.0),
        HEART,
        0.0,
        0.0,
    );

    let last = path.last().expect("path should have points");
    let pos_err = (last.heart_position - anchor.heart_position).magnitude();
    let pos_tol = 0.2; // overshoot bound, derived above
    assert!(
        pos_err < pos_tol,
        "loop didn't close: residual = {} m (pos = {:?})",
        pos_err,
        last.heart_position
    );

    let circumference = 2.0 * PI * r;
    let arc_err = (last.heart_arc - circumference).abs();
    assert!(
        arc_err < 0.15,
        "heart_arc = {} expected ≈ {} (residual {})",
        last.heart_arc,
        circumference,
        arc_err
    );
}

// ---------------------------------------------------------------------------
// 5. Steady circular motion: vertical Curved arc gives N = 1 + v²/(rg)
// ---------------------------------------------------------------------------
//
// Setup: Curved node, axis = 90° (curve around body lateral → vertical loop
// in the y-z plane), radius r = 20 m, velocity v = 10 m/s held constant by
// driven mode. Short arc (3°) starting at the bottom of the loop, where the
// body normal is still ≈ DOWN and gravity contributes its full +1 g to the
// body normal direction.
//
// Analytical: a rider on a circular arc at speed v feels a centripetal
// acceleration v²/r along the body normal direction. At the bottom of a
// vertical loop, gravity adds another g along the same direction, giving
//   N = 1 + v²/(r·g)
// For r = 20, v = 10: N = 1 + 100/(20·9.80665) = 1.5098
//
// Across the short arc (3°), the body normal tilts forward by the same 3°,
// so gravity's projection onto body normal varies by (1 − cos 3°) ≈ 1.4·10⁻³.
// That is the leading-order deviation from "constant N". Tolerance set to
// 5·10⁻³ to absorb that geometric drift plus per-step integrator noise.

#[test]
fn steady_circular_motion_holds_normal_force() {
    let v = 10.0;
    let r = 20.0;
    let arc_deg = 3.0;
    let target_n = 1.0 + v * v / (r * physics::G);
    let anchor = flat_anchor(Float3::ZERO, v);

    let path = CurvedNode::build(
        &anchor,
        r,
        arc_deg,
        90.0, // axis: vertical loop (curve around lateral)
        0.0,
        0.0,
        true,
        &k(0.0),
        &k(v),
        &k(HEART),
        &k(0.0),
        &k(0.0),
        HEART,
        0.0,
        0.0,
    );

    assert!(path.len() > 5, "arc too short: {} points", path.len());
    // Skip the anchor (index 0): anchor::build sets normal_force = 1.0
    // unconditionally, so it never matches the curving target.
    for (i, p) in path.iter().enumerate().skip(1) {
        let err = (p.normal_force - target_n).abs();
        assert!(
            err < 5e-3,
            "step {} normal_force={} target={} (err {})",
            i,
            p.normal_force,
            target_n,
            err
        );
        assert!(
            p.lateral_force.abs() < 1e-3,
            "step {} lateral_force={} should be ~0",
            i,
            p.lateral_force
        );
    }
}

// ---------------------------------------------------------------------------
// 6. Banked turn equilibrium: roll = atan(v²/(rg)) ⇒ lateral_force ≈ 0
// ---------------------------------------------------------------------------
//
// Setup: Curved node, axis = 0 (curve around body normal — for an unrolled
// rider, this is the world UP axis), radius r = 50 m, velocity v = 5 m/s,
// anchor pre-banked at the equilibrium roll φ = atan(v²/(r·g)).
//
// Analytical (free-body): in a horizontal turn, centripetal accel a_c = v²/r
// is horizontal toward the center, gravity g is vertical down. The body
// lateral force vanishes when the rider is banked so that the resultant of
// (gravity, centripetal) aligns with the body normal. That requires
//   tan(φ) = v²/(r·g)
// For v = 5, r = 50: φ = atan(25/(50·9.80665)) = atan(0.05098) ≈ 0.0509 rad.
//
// Note on parameter choice: the Curved node's curve axis is `−body_normal`,
// so banking the anchor *also* tilts the curve axis away from world UP. The
// tan(φ)=v²/(rg) equation strictly applies only when the curve axis is world
// UP, i.e., φ → 0. For finite φ, the lateral_force residual is O(φ³) (next
// order in the small-bank expansion). Choosing r = 50, v = 5 keeps φ small
// (~0.05 rad), pushing the residual into the 10⁻⁴ range.
//
// Tolerance: |lateral_force| < 1e-3 across the arc.

#[test]
fn banked_turn_equilibrium_zeros_lateral_force() {
    let v = 5.0;
    let r = 50.0;
    let bank_roll = (v * v / (r * physics::G)).atan();
    let anchor = anchor::build(Float3::ZERO, 0.0, 0.0, bank_roll, v, HEART, 0.0, 0.0);

    let path = CurvedNode::build(
        &anchor,
        r,
        90.0, // quarter turn — equilibrium is steady, so length doesn't matter
        0.0,
        0.0,
        0.0,
        true,
        &k(0.0),
        &k(v),
        &k(HEART),
        &k(0.0),
        &k(0.0),
        HEART,
        0.0,
        0.0,
    );

    assert!(path.len() > 10);
    // Skip anchor (index 0): anchor::build sets lateral_force = 0 unconditionally.
    for (i, p) in path.iter().enumerate().skip(1) {
        assert!(
            p.lateral_force.abs() < 1e-3,
            "step {} lateral_force={} should be ~0 at equilibrium roll {}",
            i,
            p.lateral_force,
            bank_roll
        );
    }
}

// ---------------------------------------------------------------------------
// 7. Roll integration: constant roll_speed for time t ⇒ final roll = K·t
// ---------------------------------------------------------------------------
//
// Setup: Geometric node, no pitch/yaw, constant roll_speed K (rad/s). The
// frame integrates roll by adding K/HZ each step (see geometric::step_geometric
// non-steering branch with `with_roll(delta_roll)`).
//
// Analytical: after N = ⌊HZ·t⌋ steps, total roll added = N · K/HZ ≈ K·t.
//
// Tolerance: 1e-5 rad. Each `with_roll(delta_roll)` is an exact quaternion
// rotation about the direction, so the only error sources are f32 round-off
// in quaternion compositions (~ε per step ≈ 100·ε ≈ 1.2·10⁻⁵ over a
// 1-second integration) and the cancellation in `lateral.y.atan2(-normal.y)`
// at small angles. The (HZ·duration).floor() rounding is folded into the
// expected value, not the tolerance.

#[test]
fn roll_integration_matches_constant_speed() {
    let k_roll = 0.5; // rad/s
    let duration = 1.0;
    let v = 10.0;
    let anchor = flat_anchor(Float3::ZERO, v);

    let path = run_geometric(&anchor, duration, k_roll, 0.0, 0.0, true, v);

    let last = path.last().expect("path should have points");
    // Time section runs i=1..⌊HZ·duration⌋, so N steps = ⌊HZ·duration⌋ − 1.
    let n_steps = ((physics::HZ * duration).floor() as usize) - 1;
    let expected_roll = k_roll * (n_steps as f32 / physics::HZ);

    let actual_roll = last.roll();
    let err = (actual_roll - expected_roll).abs();
    assert!(
        err < 1e-5,
        "roll={} expected={} (err {}, n_steps={})",
        actual_roll,
        expected_roll,
        err,
        n_steps
    );
}

// ---------------------------------------------------------------------------
// Regression: Force-driven trajectory crossing pitch = ±π/2 stays smooth
// ---------------------------------------------------------------------------
//
// Setup: Force node, driven at v = 10 m/s, target N = 3 g (vertical loop of
// r = v²/((N−1)·g) ≈ 5.1 m). Duration 1 s carries the trajectory through the
// vertical (pitch ≈ π/2) and out the other side.
//
// Pre-fix: at the single step where direction.y ≈ 1 (pitch crossing ±π/2),
// the Euler `delta_yaw` in `Curvature::from_frames` jumped by ~π for arbitrary
// direction noise, producing a one-frame `lateral_force` spike of ~8.7 g (and
// a `normal_force` dip from ~3 to ~2.5).
//
// Post-fix: `Curvature::from_frames` projects the world-frame angular delta
// onto body axes via cross-product of basis vectors (no atan2 of nearly-zero
// arguments), so the singularity is gone. Output forces stay smooth across
// the vertical.
//
// Assertion: `lateral_force` should never exceed 0.05 g, and `normal_force`
// should stay within 0.05 of the 3 g target across the whole pass.

#[test]
fn force_driven_loop_smooth_through_vertical() {
    let v = 10.0;
    let target_n = 3.0;
    let anchor = flat_anchor(Float3::ZERO, v);

    let path = run_force(&anchor, 1.0, target_n, 0.0, true, v);
    assert!(
        path.iter().any(|p| p.direction.y > 0.95),
        "duration too short to reach vertical: max dir.y = {}",
        path.iter().map(|p| p.direction.y).fold(f32::MIN, f32::max)
    );

    for (i, p) in path.iter().enumerate().skip(1) {
        assert!(
            p.lateral_force.abs() < 0.05,
            "lateral_force spike at step {}: L={} (dir.y={})",
            i,
            p.lateral_force,
            p.direction.y
        );
        assert!(
            (p.normal_force - target_n).abs() < 0.05,
            "normal_force dip at step {}: N={} (dir.y={})",
            i,
            p.normal_force,
            p.direction.y
        );
    }
}

// ---------------------------------------------------------------------------
// 8. Reverse roundtrip: forward → Reverse + ReversePath → CopyPath returns to start
// ---------------------------------------------------------------------------
//
// Setup: forward path = a Geometric arc (constant yaw_speed), driven so the
// path is a smooth turn with known endpoints. Then construct:
//   reversed_anchor = reverse(forward.last())
//   reversed_path   = reverse_path(forward)
//   copy_back       = CopyPath(reversed_anchor, reversed_path, 0, full_duration)
//
// Analytical: traversing the path backwards from its endpoint should return
// to the start position with direction reversed (since CopyPath replays the
// reversed path starting from a flipped-direction anchor).
//
// Tolerance: under driven (constant) velocity, every roundtrip step lands
// exactly on a source-path sample (CopyPath's per-step distance v/HZ
// matches the original geometric step), so interpolation is degenerate and
// the roundtrip is exact up to f32 round-off. Use 1e-4 m on position and
// require dir·anchor.dir within 1e-5 of −1.

#[test]
fn reverse_roundtrip_returns_to_start() {
    let v = 10.0;
    let duration = 0.5;
    let anchor = flat_anchor(Float3::ZERO, v);

    // Forward path: gentle yaw turn at constant velocity (driven).
    let forward = run_geometric(&anchor, duration, 0.0, 0.0, 0.5, true, v);
    assert!(forward.len() > 10);

    let last_forward = *forward.last().unwrap();
    let reversed_anchor = reverse::build(&last_forward);
    let reversed_path = reverse_path::build(&forward);

    let total_t = (forward.len() - 1) as f32 / physics::HZ;
    let copy_back = CopyPathNode::build(
        &reversed_anchor,
        &reversed_path,
        0.0,
        total_t,
        true,
        &k(v),
        &k(HEART),
        &k(0.0),
        &k(0.0),
        HEART,
        0.0,
        0.0,
    );
    assert!(copy_back.len() > 10);

    let landed = copy_back.last().unwrap();
    let pos_err = (landed.heart_position - anchor.heart_position).magnitude();
    assert!(
        pos_err < 1e-4,
        "roundtrip position err = {} (landed at {:?}, start at {:?})",
        pos_err,
        landed.heart_position,
        anchor.heart_position
    );

    // Direction should be reversed: dot(landed.dir, anchor.dir) ≈ -1
    let dir_dot = landed.direction.dot(anchor.direction);
    assert!(
        (dir_dot - (-1.0)).abs() < 1e-5,
        "direction dot = {} expected ≈ -1",
        dir_dot
    );
}
