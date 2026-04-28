use super::curvature::Curvature;
use super::frame::Frame;
use super::math::Float3;
use super::physics;

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct Forces {
    pub normal: f32,
    pub lateral: f32,
}

impl Forces {
    pub const fn new(normal: f32, lateral: f32) -> Self {
        Self { normal, lateral }
    }

    pub fn compute(curvature: Curvature, frame: Frame, velocity: f32, heart_advance: f32) -> Self {
        let force_vec = Self::force_vector(curvature, frame, velocity, heart_advance);
        Self::new(-force_vec.dot(frame.normal), -force_vec.dot(frame.lateral))
    }

    /// World-space force vector (gravity + centripetal contributions) producing
    /// the body-frame normal/lateral G-forces. Shared by `compute` and by the
    /// path-following nodes (Bridge, CopyPath) that need the raw vector.
    pub fn force_vector(
        curvature: Curvature,
        frame: Frame,
        velocity: f32,
        heart_advance: f32,
    ) -> Float3 {
        if curvature.total_angle.abs() < physics::EPSILON {
            return Float3::UP;
        }
        Float3::UP
            + frame.lateral * (velocity * physics::HZ * curvature.lateral_angle / physics::G)
            + frame.normal
                * (heart_advance * physics::HZ * physics::HZ * curvature.normal_angle / physics::G)
    }

    pub const ONE_G: Self = Self::new(1.0, 0.0);
    pub const ZERO: Self = Self::new(0.0, 0.0);
}

impl Default for Forces {
    fn default() -> Self {
        Self::ONE_G
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-4;

    #[test]
    fn one_g_has_unit_normal() {
        let one_g = Forces::ONE_G;
        assert_relative_eq!(one_g.normal, 1.0, epsilon = TOLERANCE);
        assert_relative_eq!(one_g.lateral, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn zero_has_no_forces() {
        let zero = Forces::ZERO;
        assert_relative_eq!(zero.normal, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.lateral, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn compute_zero_curvature_one_g_normal_zero_lateral() {
        let curvature = Curvature::ZERO;
        let frame = Frame::DEFAULT;
        let forces = Forces::compute(curvature, frame, 10.0, 0.1);

        assert_relative_eq!(forces.normal, 1.0, epsilon = TOLERANCE);
        assert_relative_eq!(forces.lateral, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn compute_flat_track_approximately_one_g() {
        let prev = Frame::DEFAULT;
        let curr = Frame::DEFAULT;
        let curvature = Curvature::from_frames(curr, prev);
        let forces = Forces::compute(curvature, curr, 15.0, 0.15);

        assert_relative_eq!(forces.normal, 1.0, epsilon = TOLERANCE);
        assert_relative_eq!(forces.lateral, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn compute_vertical_loop_high_normal_force() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_pitch(0.1);
        let curvature = Curvature::from_frames(curr, prev);
        let forces = Forces::compute(curvature, curr, 20.0, 0.2);

        assert!(forces.normal > 1.0);
    }

    #[test]
    fn compute_banked_curve_lateral_force_present() {
        let prev = crate::sim::point::from_direction_and_roll(Float3::BACK, 0.3);
        let curr = prev.with_yaw(0.1);
        let curvature = Curvature::from_frames(curr, prev);
        let forces = Forces::compute(curvature, curr, 15.0, 0.15);

        assert_ne!(forces.lateral, 0.0);
    }

    #[test]
    fn compute_zero_velocity_handles_gracefully() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_pitch(0.05);
        let curvature = Curvature::from_frames(curr, prev);

        let _ = Forces::compute(curvature, curr, 0.0, 0.0);
    }

}
