use crate::sim::{Curvature, Float3, Forces, Frame, Point};

/// Compute the world-space force vector for path-following nodes (Bridge, CopyPath).
///
/// Thin wrapper over `Curvature::from_frames` + `Forces::force_vector` so the
/// path-following nodes share the integrator's curvature math (no duplicated
/// Euler decomposition that would fight the gimbal-safe form in `sim/curvature.rs`).
pub fn compute_force_vector(
    prev: &Point,
    curr: &Frame,
    heart_advance: f32,
    velocity: f32,
) -> Float3 {
    let curvature = Curvature::from_frames(*curr, prev.frame());
    Forces::force_vector(curvature, *curr, velocity, heart_advance)
}
