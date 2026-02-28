use super::frame::Frame;
use super::physics;

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct Curvature {
    pub delta_pitch: f32,
    pub delta_yaw: f32,
    pub yaw_scale: f32,
    pub total_angle: f32,
}

impl Curvature {
    pub const fn new(delta_pitch: f32, delta_yaw: f32, yaw_scale: f32, total_angle: f32) -> Self {
        Self {
            delta_pitch,
            delta_yaw,
            yaw_scale,
            total_angle,
        }
    }

    pub fn from_frames(curr: Frame, prev: Frame) -> Self {
        let diff = curr.direction - prev.direction;
        if diff.magnitude() < physics::EPSILON {
            return Self::new(0.0, 0.0, curr.pitch().abs().cos(), 0.0);
        }

        let delta_pitch = physics::wrap_angle(curr.pitch() - prev.pitch());
        let delta_yaw = physics::wrap_angle(curr.yaw() - prev.yaw());
        let yaw_scale = curr.pitch().abs().cos();
        let total_angle =
            (yaw_scale * yaw_scale * delta_yaw * delta_yaw + delta_pitch * delta_pitch).sqrt();

        Self::new(delta_pitch, delta_yaw, yaw_scale, total_angle)
    }

    pub const ZERO: Self = Self::new(0.0, 0.0, 1.0, 0.0);
}

impl Default for Curvature {
    fn default() -> Self {
        Self::ZERO
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::{Float3, Quaternion};
    use approx::assert_relative_eq;
    use std::f32::consts::PI;

    const TOLERANCE: f32 = 1e-5;

    #[test]
    fn zero_has_zero_angles() {
        let zero = Curvature::ZERO;
        assert_relative_eq!(zero.delta_pitch, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.delta_yaw, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.total_angle, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(zero.yaw_scale, 1.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_identical_frames_zero_curvature() {
        let frame = Frame::DEFAULT;
        let curvature = Curvature::from_frames(frame, frame);

        assert_relative_eq!(curvature.delta_pitch, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(curvature.delta_yaw, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(curvature.total_angle, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_pure_pitch_only_delta_pitch_non_zero() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_pitch(0.1);
        let curvature = Curvature::from_frames(curr, prev);

        assert_ne!(curvature.delta_pitch, 0.0);
        assert_relative_eq!(curvature.delta_yaw, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_pure_yaw_only_delta_yaw_non_zero() {
        let prev = Frame::DEFAULT;
        let curr = prev.with_yaw(0.1);
        let curvature = Curvature::from_frames(curr, prev);

        assert_ne!(curvature.delta_yaw, 0.0);
        assert_relative_eq!(curvature.delta_pitch, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn from_frames_total_angle_combines_pitch_and_yaw() {
        let prev = Frame::DEFAULT;
        let pitched = prev.with_pitch(0.05);
        let curr = pitched.with_yaw(0.05);
        let curvature = Curvature::from_frames(curr, prev);

        assert!(curvature.total_angle > 0.0);
        assert!(curvature.total_angle > curvature.delta_pitch.abs());
    }

    #[test]
    fn from_frames_yaw_scale_depends_on_pitch() {
        let flat = Frame::DEFAULT;
        let pitched = from_euler(0.5, 0.0, 0.0);

        let curv_flat = Curvature::from_frames(flat, flat);
        let curv_pitched = Curvature::from_frames(pitched, pitched);

        assert_relative_eq!(curv_flat.yaw_scale, 1.0, epsilon = TOLERANCE);
        assert!(curv_pitched.yaw_scale < 1.0);
    }

    #[test]
    fn from_frames_angle_wrapping_handles_near_pi() {
        let prev = from_euler(0.0, PI - 0.05, 0.0);
        let curr = from_euler(0.0, -PI + 0.05, 0.0);
        let curvature = Curvature::from_frames(curr, prev);

        assert!(curvature.delta_yaw.abs() < 0.2);
    }

    fn from_euler(pitch: f32, yaw: f32, roll: f32) -> Frame {
        let pitch_quat = Quaternion::from_axis_angle(Float3::RIGHT, pitch);
        let yaw_quat = Quaternion::from_axis_angle(Float3::UP, yaw);
        let combined = yaw_quat * pitch_quat;
        let direction = combined.mul_vec(Float3::BACK).normalize();

        let lateral_base = yaw_quat.mul_vec(Float3::RIGHT);
        let roll_quat = Quaternion::from_axis_angle(direction, -roll);
        let lateral = roll_quat.mul_vec(lateral_base).normalize();
        let normal = direction.cross(lateral).normalize();

        Frame::new(direction, normal, lateral)
    }
}
