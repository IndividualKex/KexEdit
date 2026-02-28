use crate::sim::{physics, Float3, Frame, Point};

/// Compute the force vector for path-following nodes (Bridge, CopyPath).
///
/// Returns a force vector in world space representing the centripetal
/// acceleration required to follow the path, used to calculate normal
/// and lateral G-forces.
pub fn compute_force_vector(
    prev: &Point,
    curr: &Frame,
    heart_advance: f32,
    velocity: f32,
) -> Float3 {
    let roll = curr.roll();
    let pitch = curr.pitch();
    let yaw = curr.yaw();
    let prev_pitch = prev.frame().pitch();
    let prev_yaw = prev.frame().yaw();

    let pitch_from_last = physics::wrap_angle(pitch - prev_pitch);
    let yaw_from_last = physics::wrap_angle(yaw - prev_yaw);
    let yaw_scale_factor = pitch.abs().cos();
    let angle_from_last = (yaw_scale_factor * yaw_scale_factor * yaw_from_last * yaw_from_last
        + pitch_from_last * pitch_from_last)
        .sqrt();

    if angle_from_last.abs() < physics::EPSILON {
        return Float3::new(0.0, 1.0, 0.0);
    }

    let cos_roll = roll.cos();
    let sin_roll = roll.sin();
    let normal_angle = -pitch_from_last * cos_roll - yaw_scale_factor * yaw_from_last * sin_roll;
    let lateral_angle = pitch_from_last * sin_roll - yaw_scale_factor * yaw_from_last * cos_roll;

    Float3::new(0.0, 1.0, 0.0)
        + curr.lateral * (velocity * physics::HZ * lateral_angle / physics::G)
        + curr.normal * (heart_advance * physics::HZ * physics::HZ * normal_angle / physics::G)
}
