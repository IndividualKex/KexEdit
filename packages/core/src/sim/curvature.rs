use super::frame::Frame;
use super::physics;

/// Body-frame curvature between two consecutive integrator frames.
///
/// `normal_angle` and `lateral_angle` are the components of the rotation
/// vector ω (from prev to curr, in world frame) projected onto prev's body
/// lateral and body normal axes — sign-conventioned to plug straight into
/// `Forces::compute`. They are **not** Euler `delta_pitch`/`delta_yaw`;
/// computing them via direct projection avoids the gimbal singularity that
/// the Euler form hits when the trajectory crosses pitch = ±π/2.
#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct Curvature {
    /// Rotation about the body lateral axis (drives the body-normal force component).
    pub normal_angle: f32,
    /// Rotation about the body normal axis (drives the body-lateral force component).
    pub lateral_angle: f32,
    /// √(normal_angle² + lateral_angle²) — total body-frame curvature magnitude.
    pub total_angle: f32,
}

impl Curvature {
    pub const fn new(normal_angle: f32, lateral_angle: f32, total_angle: f32) -> Self {
        Self {
            normal_angle,
            lateral_angle,
            total_angle,
        }
    }

    pub fn from_frames(curr: Frame, prev: Frame) -> Self {
        let omega = curr.angular_delta_from(prev);
        let normal_angle = -omega.dot(prev.lateral);
        let lateral_angle = omega.dot(prev.normal);
        let total_angle = (normal_angle * normal_angle + lateral_angle * lateral_angle).sqrt();
        if total_angle < physics::EPSILON {
            return Self::ZERO;
        }
        Self::new(normal_angle, lateral_angle, total_angle)
    }

    pub const ZERO: Self = Self::new(0.0, 0.0, 0.0);
}

impl Default for Curvature {
    fn default() -> Self {
        Self::ZERO
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;
    use approx::assert_relative_eq;
    use std::f32::consts::PI;

    const TOLERANCE: f32 = 1e-5;

    #[test]
    fn zero_has_zero_angles() {
        let zero = Curvature::ZERO;
        assert_relative_eq!(zero.normal_angle, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.lateral_angle, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.total_angle, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_identical_frames_zero_curvature() {
        let frame = Frame::DEFAULT;
        let curvature = Curvature::from_frames(frame, frame);

        assert_relative_eq!(curvature.normal_angle, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(curvature.lateral_angle, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(curvature.total_angle, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_pure_pitch_only_normal_angle_non_zero() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_pitch(0.1);
        let curvature = Curvature::from_frames(curr, prev);

        // Pitch up rotates around body lateral, so curvature shows up in normal_angle.
        assert!(curvature.normal_angle.abs() > 0.0);
        assert_relative_eq!(curvature.lateral_angle, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_pure_yaw_only_lateral_angle_non_zero() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_yaw(0.1);
        let curvature = Curvature::from_frames(curr, prev);

        // Yaw rotates around world UP = -body normal (default frame), so curvature shows up in lateral_angle.
        assert!(curvature.lateral_angle.abs() > 0.0);
        assert_relative_eq!(curvature.normal_angle, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_total_angle_combines_pitch_and_yaw() {
        let prev = Frame::DEFAULT;
        let pitched = prev.with_pitch(0.05);
        let curr = pitched.with_yaw(0.05);
        let curvature = Curvature::from_frames(curr, prev);

        assert!(curvature.total_angle > 0.0);
        assert!(curvature.total_angle > curvature.normal_angle.abs());
    }

    /// Singularity check: a Force-driven path crossing pitch = ±π/2 used to
    /// produce a single-step curvature spike (delta_yaw flipped by ~π for
    /// arbitrary direction noise). The quaternion-delta form smoothly handles
    /// vertical: a small frame perturbation near the singularity gives a
    /// proportionally small curvature, not a spike.
    #[test]
    fn from_frames_smooth_through_vertical() {
        // prev = nearly straight up, curr = perturbed by 0.01 rad of pitch
        let near_vertical = Frame::DEFAULT.with_pitch(PI / 2.0 - 0.001);
        let just_past = near_vertical.with_pitch(0.01);
        let curvature = Curvature::from_frames(just_past, near_vertical);

        // The actual rotation is 0.01 rad about the lateral axis. Total
        // curvature should match within asin/sin truncation.
        assert!(
            curvature.total_angle < 0.02,
            "expected curvature ≈ 0.01 rad near vertical, got {}",
            curvature.total_angle
        );
    }

    #[test]
    fn from_frames_recovers_known_rotation() {
        // Yaw left by 0.1 rad: ω = 0.1·UP, prev.normal = DOWN, prev.lateral = RIGHT.
        // ω·prev.normal = -0.1, ω·prev.lateral = 0.
        // ⇒ normal_angle = 0, lateral_angle = -0.1.
        let prev = Frame::DEFAULT;
        let curr = prev.with_yaw(0.1);
        let curvature = Curvature::from_frames(curr, prev);

        assert_relative_eq!(curvature.normal_angle, 0.0, epsilon = 1e-3);
        assert_relative_eq!(curvature.lateral_angle, -0.1, epsilon = 1e-3);
    }

    #[test]
    fn from_frames_with_angular_delta_matches_direct_rotation() {
        // For a rotation by axis*θ, angular_delta should recover that vector.
        let axis = Float3::new(1.0, 2.0, -1.0).normalize();
        let theta = 0.3;
        let rotated = Frame::DEFAULT.rotate_around(axis, theta);
        let omega = rotated.angular_delta_from(Frame::DEFAULT);

        let recovered_axis = omega.normalize();
        let recovered_angle = omega.magnitude();

        assert_relative_eq!(recovered_angle, theta, epsilon = 1e-5);
        assert_relative_eq!(recovered_axis.dot(axis), 1.0, epsilon = 1e-5);
    }
}
