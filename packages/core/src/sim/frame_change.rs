use super::frame::Frame;
use super::math::Float3;

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct FrameChange {
    pub new_direction: Float3,
    pub new_normal: Float3,
    pub new_lateral: Float3,
}

impl FrameChange {
    pub const fn new(new_direction: Float3, new_normal: Float3, new_lateral: Float3) -> Self {
        Self {
            new_direction,
            new_normal,
            new_lateral,
        }
    }

    pub fn from_angles(prev: Frame, delta_pitch: f32, delta_yaw: f32) -> Self {
        let pitched = prev.with_pitch(delta_pitch);
        let yawed = pitched.with_yaw(delta_yaw);
        Self::new(yawed.direction, yawed.normal, yawed.lateral)
    }

    pub fn from_axis(prev: Frame, axis: Float3, angle: f32) -> Self {
        let rotated = prev.rotate_around(axis, angle);
        Self::new(rotated.direction, rotated.normal, rotated.lateral)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-5;

    #[test]
    fn from_angles_zero_change_preserves_frame() {
        let prev = Frame::DEFAULT;
        let change = FrameChange::from_angles(prev, 0.0, 0.0);

        assert_relative_eq!(
            change.new_direction.x,
            prev.direction.x,
            epsilon = TOLERANCE
        );
        assert_relative_eq!(
            change.new_direction.y,
            prev.direction.y,
            epsilon = TOLERANCE
        );
        assert_relative_eq!(
            change.new_direction.z,
            prev.direction.z,
            epsilon = TOLERANCE
        );
    }

    #[test]
    fn from_angles_pitch_changes_direction() {
        let prev = Frame::DEFAULT;
        let change = FrameChange::from_angles(prev, 0.1, 0.0);

        assert_ne!(change.new_direction.y, prev.direction.y);
    }

    #[test]
    fn from_axis_rotation_changes_frame() {
        let prev = Frame::DEFAULT;
        let axis = Float3::UP;
        let change = FrameChange::from_axis(prev, axis, 0.5);

        let dot = change.new_direction.dot(prev.direction);
        assert!(dot < 1.0);
    }
}
