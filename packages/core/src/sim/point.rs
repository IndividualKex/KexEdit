use super::frame::Frame;
use super::math::{Float3, Quaternion};

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct Point {
    pub heart_position: Float3,
    pub direction: Float3,
    pub normal: Float3,
    pub lateral: Float3,
    pub velocity: f32,
    pub normal_force: f32,
    pub lateral_force: f32,
    pub heart_arc: f32,
    pub spine_arc: f32,
    pub heart_advance: f32,
    pub friction_origin: f32,
    pub roll_speed: f32,
    pub heart_offset: f32,
    pub friction: f32,
    pub resistance: f32,
}

impl Point {
    #[allow(clippy::too_many_arguments)]
    pub const fn new(
        heart_position: Float3,
        direction: Float3,
        normal: Float3,
        lateral: Float3,
        velocity: f32,
        normal_force: f32,
        lateral_force: f32,
        heart_arc: f32,
        spine_arc: f32,
        heart_advance: f32,
        friction_origin: f32,
        roll_speed: f32,
        heart_offset: f32,
        friction: f32,
        resistance: f32,
    ) -> Self {
        Self {
            heart_position,
            direction,
            normal,
            lateral,
            velocity,
            normal_force,
            lateral_force,
            heart_arc,
            spine_arc,
            heart_advance,
            friction_origin,
            roll_speed,
            heart_offset,
            friction,
            resistance,
        }
    }

    pub fn roll(&self) -> f32 {
        self.lateral.y.atan2(-self.normal.y)
    }

    pub fn frame(&self) -> Frame {
        Frame::new(self.direction, self.normal, self.lateral)
    }

    pub fn spine_position(&self, offset: f32) -> Float3 {
        self.heart_position + self.normal * offset
    }

    pub fn create(
        heart_position: Float3,
        direction: Float3,
        roll: f32,
        velocity: f32,
        heart_offset: f32,
        friction: f32,
        resistance: f32,
    ) -> Self {
        let frame = from_direction_and_roll(direction, roll);

        Self::new(
            heart_position,
            frame.direction,
            frame.normal,
            frame.lateral,
            velocity,
            1.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            heart_offset,
            friction,
            resistance,
        )
    }

    pub const DEFAULT: Self = Self::new(
        Float3::new(0.0, 3.0, 0.0),
        Float3::BACK,
        Float3::DOWN,
        Float3::RIGHT,
        10.0,
        1.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.1,
        0.0,
        0.0,
    );

    pub fn with_friction_origin(&self, new_origin: f32) -> Self {
        Self::new(
            self.heart_position,
            self.direction,
            self.normal,
            self.lateral,
            self.velocity,
            self.normal_force,
            self.lateral_force,
            self.heart_arc,
            self.spine_arc,
            self.heart_advance,
            new_origin,
            self.roll_speed,
            self.heart_offset,
            self.friction,
            self.resistance,
        )
    }

    pub fn with_forces(&self, new_normal_force: f32, new_lateral_force: f32) -> Self {
        Self::new(
            self.heart_position,
            self.direction,
            self.normal,
            self.lateral,
            self.velocity,
            new_normal_force,
            new_lateral_force,
            self.heart_arc,
            self.spine_arc,
            self.heart_advance,
            self.friction_origin,
            self.roll_speed,
            self.heart_offset,
            self.friction,
            self.resistance,
        )
    }

    pub fn with_velocity(&self, new_velocity: f32, reset_friction: bool) -> Self {
        let new_friction_origin = if reset_friction {
            self.spine_arc
        } else {
            self.friction_origin
        };

        Self::new(
            self.heart_position,
            self.direction,
            self.normal,
            self.lateral,
            new_velocity,
            self.normal_force,
            self.lateral_force,
            self.heart_arc,
            self.spine_arc,
            self.heart_advance,
            new_friction_origin,
            self.roll_speed,
            self.heart_offset,
            self.friction,
            self.resistance,
        )
    }
}

fn from_direction_and_roll(direction: Float3, roll: f32) -> Frame {
    let dir = direction.normalize();
    let yaw = (-dir.x).atan2(-dir.z);

    let yaw_quat = Quaternion::from_axis_angle(Float3::UP, yaw);
    let lateral_base = yaw_quat.mul_vec(Float3::RIGHT);
    let roll_quat = Quaternion::from_axis_angle(dir, -roll);
    let lateral = roll_quat.mul_vec(lateral_base).normalize();
    let normal = dir.cross(lateral).normalize();

    Frame::new(dir, normal, lateral)
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-5;

    #[test]
    fn default_point_has_expected_values() {
        let point = Point::DEFAULT;
        assert_relative_eq!(point.heart_position.y, 3.0, epsilon = TOLERANCE);
        assert_relative_eq!(point.velocity, 10.0, epsilon = TOLERANCE);
        assert_relative_eq!(point.heart_offset, 1.1, epsilon = TOLERANCE);
    }

    #[test]
    fn create_point_sets_velocity_correctly() {
        let heart_pos = Float3::new(0.0, 5.0, 0.0);
        let velocity = 15.0;
        let heart_offset = 1.0;

        let point = Point::create(
            heart_pos,
            Float3::BACK,
            0.0,
            velocity,
            heart_offset,
            0.0,
            0.0,
        );

        assert_relative_eq!(point.velocity, velocity, epsilon = TOLERANCE);
        assert_relative_eq!(point.heart_offset, heart_offset, epsilon = TOLERANCE);
    }

    #[test]
    fn with_friction_origin_updates_only_friction_origin() {
        let point = Point::DEFAULT;
        let new_origin = 42.0;
        let updated = point.with_friction_origin(new_origin);

        assert_relative_eq!(updated.friction_origin, new_origin, epsilon = TOLERANCE);
        assert_relative_eq!(updated.velocity, point.velocity, epsilon = TOLERANCE);
    }

    #[test]
    fn with_forces_updates_only_forces() {
        let point = Point::DEFAULT;
        let new_normal = 2.5;
        let new_lateral = 0.8;
        let updated = point.with_forces(new_normal, new_lateral);

        assert_relative_eq!(updated.normal_force, new_normal, epsilon = TOLERANCE);
        assert_relative_eq!(updated.lateral_force, new_lateral, epsilon = TOLERANCE);
        assert_relative_eq!(updated.velocity, point.velocity, epsilon = TOLERANCE);
    }
}
