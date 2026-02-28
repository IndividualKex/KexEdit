use crate::sim::{Float3, Point, Quaternion};

fn from_euler(pitch: f32, yaw: f32, roll: f32) -> (Float3, Float3, Float3) {
    let pitch_quat = Quaternion::from_axis_angle(Float3::RIGHT, pitch);
    let yaw_quat = Quaternion::from_axis_angle(Float3::UP, yaw);

    let temp_x = pitch_quat.mul_vec(Float3::RIGHT);
    let temp_z = pitch_quat.mul_vec(Float3::BACK);

    let direction = yaw_quat.mul_vec(temp_z);

    let lateral_base = yaw_quat.mul_vec(temp_x);
    let roll_quat = Quaternion::from_axis_angle(direction, -roll);
    let lateral = roll_quat.mul_vec(lateral_base).normalize();
    let normal = direction.cross(lateral).normalize();

    (direction, normal, lateral)
}

#[allow(clippy::too_many_arguments)]
pub fn build(
    position: Float3,
    pitch: f32,
    yaw: f32,
    roll: f32,
    velocity: f32,
    heart_offset: f32,
    friction: f32,
    resistance: f32,
) -> Point {
    let (direction, normal, lateral) = from_euler(pitch, yaw, roll);

    Point::new(
        position,
        direction,
        normal,
        lateral,
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn build_creates_point_with_correct_position() {
        let position = Float3::new(1.0, 2.0, 3.0);
        let result = build(position, 0.0, 0.0, 0.0, 10.0, 1.1, 0.0, 0.0);

        assert_eq!(result.heart_position, position);
        assert_eq!(result.velocity, 10.0);
    }

    #[test]
    fn build_creates_point_with_correct_orientation() {
        let result = build(Float3::ZERO, 0.0, 0.0, 0.0, 10.0, 1.1, 0.0, 0.0);

        assert_eq!(result.direction, Float3::BACK);
    }
}
