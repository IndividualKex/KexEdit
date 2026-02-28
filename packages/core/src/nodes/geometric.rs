use crate::sim::{physics, Curvature, Float3, Forces, Frame, Keyframe, Point};

use super::{DurationType, IterationConfig};

const MAX_ITERATIONS: usize = 1_000_000;

#[allow(clippy::too_many_arguments)]
pub fn build(
    anchor: &Point,
    config: &IterationConfig,
    driven: bool,
    steering: bool,
    roll_speed: &[Keyframe],
    pitch_speed: &[Keyframe],
    yaw_speed: &[Keyframe],
    driven_velocity: &[Keyframe],
    heart_offset: &[Keyframe],
    friction: &[Keyframe],
    resistance: &[Keyframe],
    anchor_heart: f32,
    anchor_friction: f32,
    anchor_resistance: f32,
) -> Vec<Point> {
    let mut result = Vec::new();
    result.push(*anchor);

    let mut state = *anchor;
    let mut accumulated_roll = 0.0;

    match config.duration_type {
        DurationType::Time => {
            build_time_section(
                config.duration,
                driven,
                steering,
                roll_speed,
                pitch_speed,
                yaw_speed,
                driven_velocity,
                heart_offset,
                friction,
                resistance,
                anchor_heart,
                anchor_friction,
                anchor_resistance,
                &mut state,
                &mut accumulated_roll,
                &mut result,
            );
        }
        DurationType::Distance => {
            build_distance_section(
                config.duration,
                driven,
                steering,
                anchor.spine_arc,
                roll_speed,
                pitch_speed,
                yaw_speed,
                driven_velocity,
                heart_offset,
                friction,
                resistance,
                anchor_heart,
                anchor_friction,
                anchor_resistance,
                &mut state,
                &mut accumulated_roll,
                &mut result,
            );
        }
    }

    result
}

#[allow(clippy::too_many_arguments)]
fn build_time_section(
    duration: f32,
    driven: bool,
    steering: bool,
    roll_speed: &[Keyframe],
    pitch_speed: &[Keyframe],
    yaw_speed: &[Keyframe],
    driven_velocity: &[Keyframe],
    heart_offset: &[Keyframe],
    friction: &[Keyframe],
    resistance: &[Keyframe],
    anchor_heart: f32,
    anchor_friction: f32,
    anchor_resistance: f32,
    state: &mut Point,
    accumulated_roll: &mut f32,
    result: &mut Vec<Point>,
) {
    let point_count = (physics::HZ * duration).floor() as usize;
    for i in 1..point_count {
        let t = i as f32 / physics::HZ;

        let mut prev = *state;

        if driven {
            let velocity = crate::sim::evaluate(driven_velocity, t, prev.velocity);
            if velocity < physics::MIN_VELOCITY {
                break;
            }
            prev = prev.with_velocity(velocity, true);
        } else if prev.velocity < physics::MIN_VELOCITY {
            if prev.frame().pitch() < 0.0 {
                prev = prev.with_velocity(physics::MIN_VELOCITY, true);
            } else {
                break;
            }
        }

        let heart_offset_val = crate::sim::evaluate(heart_offset, t, anchor_heart);
        let friction_val = crate::sim::evaluate(friction, t, anchor_friction);
        let resistance_val = crate::sim::evaluate(resistance, t, anchor_resistance);

        let pitch_speed_val = crate::sim::evaluate(pitch_speed, t, 0.0);
        let yaw_speed_val = crate::sim::evaluate(yaw_speed, t, 0.0);
        let roll_speed_val = crate::sim::evaluate(roll_speed, t, 0.0);

        let delta_roll = roll_speed_val / physics::HZ;
        let delta_pitch = pitch_speed_val / physics::HZ;
        let delta_yaw = yaw_speed_val / physics::HZ;

        let curr = step_geometric(
            &prev,
            heart_offset_val,
            friction_val,
            resistance_val,
            delta_roll,
            delta_pitch,
            delta_yaw,
            driven,
            steering,
            roll_speed_val,
            accumulated_roll,
        );

        if curr.velocity > physics::MAX_VELOCITY {
            break;
        }
        let force_magnitude = (curr.normal_force * curr.normal_force
            + curr.lateral_force * curr.lateral_force)
            .sqrt();
        if force_magnitude > physics::MAX_FORCE {
            break;
        }

        result.push(curr);
        *state = curr;
    }
}

#[allow(clippy::too_many_arguments)]
fn build_distance_section(
    duration: f32,
    driven: bool,
    steering: bool,
    anchor_spine_arc: f32,
    roll_speed: &[Keyframe],
    pitch_speed: &[Keyframe],
    yaw_speed: &[Keyframe],
    driven_velocity: &[Keyframe],
    heart_offset: &[Keyframe],
    friction: &[Keyframe],
    resistance: &[Keyframe],
    anchor_heart: f32,
    anchor_friction: f32,
    anchor_resistance: f32,
    state: &mut Point,
    accumulated_roll: &mut f32,
    result: &mut Vec<Point>,
) {
    let end_length = anchor_spine_arc + duration;
    let mut iterations = 0;

    while state.spine_arc < end_length {
        if iterations >= MAX_ITERATIONS {
            break;
        }
        iterations += 1;

        let prev = *state;
        let d = prev.spine_arc - anchor_spine_arc + prev.velocity / physics::HZ;

        let mut prev = prev;
        if driven {
            let velocity = crate::sim::evaluate(driven_velocity, d, prev.velocity);
            if velocity < physics::MIN_VELOCITY {
                break;
            }
            prev = prev.with_velocity(velocity, true);
        } else if prev.velocity < physics::MIN_VELOCITY {
            if prev.frame().pitch() < 0.0 {
                prev = prev.with_velocity(physics::MIN_VELOCITY, true);
            } else {
                break;
            }
        }

        let heart_offset_val = crate::sim::evaluate(heart_offset, d, anchor_heart);
        let friction_val = crate::sim::evaluate(friction, d, anchor_friction);
        let resistance_val = crate::sim::evaluate(resistance, d, anchor_resistance);

        let pitch_speed_val = crate::sim::evaluate(pitch_speed, d, 0.0);
        let yaw_speed_val = crate::sim::evaluate(yaw_speed, d, 0.0);
        let roll_speed_val = crate::sim::evaluate(roll_speed, d, 0.0);

        let delta_roll = roll_speed_val * (prev.velocity / physics::HZ);
        let delta_pitch = pitch_speed_val * (prev.velocity / physics::HZ);
        let delta_yaw = yaw_speed_val * (prev.velocity / physics::HZ);

        let curr = step_geometric(
            &prev,
            heart_offset_val,
            friction_val,
            resistance_val,
            delta_roll,
            delta_pitch,
            delta_yaw,
            driven,
            steering,
            roll_speed_val,
            accumulated_roll,
        );

        if curr.velocity > physics::MAX_VELOCITY {
            break;
        }
        let force_magnitude = (curr.normal_force * curr.normal_force
            + curr.lateral_force * curr.lateral_force)
            .sqrt();
        if force_magnitude > physics::MAX_FORCE {
            break;
        }

        result.push(curr);
        *state = curr;
    }
}

#[allow(clippy::too_many_arguments)]
fn step_geometric(
    prev: &Point,
    heart_offset_val: f32,
    friction_val: f32,
    resistance_val: f32,
    delta_roll: f32,
    delta_pitch: f32,
    delta_yaw: f32,
    driven: bool,
    steering: bool,
    roll_speed_val: f32,
    accumulated_roll: &mut f32,
) -> Point {
    let prev_frame = prev.frame();
    let prev_direction = prev.direction;
    let prev_heart_position = prev.heart_position;

    let (curr_direction, curr_lateral, curr_normal, curr_heart_position) = if steering {
        let mut unrolled_frame = prev_frame;
        if accumulated_roll.abs() > physics::EPSILON {
            unrolled_frame = prev_frame.with_roll(-*accumulated_roll);
        }

        let up = if unrolled_frame.normal.y >= 0.0 {
            Float3::UP
        } else {
            -Float3::UP
        };
        let pitch_axis = up.cross(prev_direction).normalize();
        let rotated = unrolled_frame
            .rotate_around(pitch_axis, delta_pitch)
            .with_yaw(delta_yaw);
        let curr_direction = rotated.direction;
        let lateral_unrolled = rotated.lateral;
        let normal_unrolled = rotated.normal;

        let half_step_distance = prev.velocity / (2.0 * physics::HZ);
        let curr_heart_position = prev_heart_position
            + curr_direction * half_step_distance
            + prev_direction * half_step_distance;

        *accumulated_roll += delta_roll;

        let (curr_lateral, curr_normal) = if accumulated_roll.abs() > physics::EPSILON {
            let rerolled = Frame::new(curr_direction, normal_unrolled, lateral_unrolled)
                .with_roll(*accumulated_roll);
            (rerolled.lateral, rerolled.normal)
        } else {
            (lateral_unrolled, normal_unrolled)
        };

        (
            curr_direction,
            curr_lateral,
            curr_normal,
            curr_heart_position,
        )
    } else {
        let rotated = prev_frame.with_pitch(delta_pitch).with_yaw(delta_yaw);
        let curr_direction = rotated.direction;
        let mut curr_normal = rotated.normal;

        let half_step_distance = prev.velocity / (2.0 * physics::HZ);
        let prev_spine_pos = prev.spine_position(heart_offset_val);
        let curr_spine_pos_if_heart_static = prev_heart_position + curr_normal * heart_offset_val;

        let curr_heart_position = prev_heart_position
            + curr_direction * half_step_distance
            + prev_direction * half_step_distance
            + (prev_spine_pos - curr_spine_pos_if_heart_static);

        let rolled = rotated.with_roll(delta_roll);
        let curr_lateral = rolled.lateral;
        curr_normal = rolled.normal;

        (
            curr_direction,
            curr_lateral,
            curr_normal,
            curr_heart_position,
        )
    };

    let spine_advance = ((curr_heart_position + curr_normal * heart_offset_val)
        - prev.spine_position(heart_offset_val))
    .magnitude();
    let heart_advance = (curr_heart_position - prev_heart_position).magnitude();
    let new_heart_arc = prev.heart_arc + heart_advance;
    let new_spine_arc = prev.spine_arc + spine_advance;

    // Use delta-based velocity update for numerical stability
    let new_velocity = if !driven {
        let prev_center_y = (prev_heart_position + prev.normal * (0.9 * heart_offset_val)).y;
        let curr_center_y = (curr_heart_position + curr_normal * (0.9 * heart_offset_val)).y;
        let delta_y = curr_center_y - prev_center_y;
        physics::update_velocity(
            prev.velocity,
            delta_y,
            spine_advance,
            friction_val,
            resistance_val,
        )
    } else {
        prev.velocity
    };

    let curr_frame = Frame::new(curr_direction, curr_normal, curr_lateral);
    let curvature = Curvature::from_frames(curr_frame, prev_frame);
    let forces = Forces::compute(curvature, curr_frame, new_velocity, heart_advance);

    Point::new(
        curr_heart_position,
        curr_direction,
        curr_normal,
        curr_lateral,
        new_velocity,
        forces.normal,
        forces.lateral,
        new_heart_arc,
        new_spine_arc,
        heart_advance,
        prev.friction_origin,
        roll_speed_val,
        heart_offset_val,
        friction_val,
        resistance_val,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn build_time_section_creates_points() {
        let anchor = Point::DEFAULT;
        let config = IterationConfig::new(0.5, DurationType::Time);

        let result = build(
            &anchor,
            &config,
            false,
            false,
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            1.1,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
        assert_eq!(result[0], anchor);
    }

    #[test]
    fn build_distance_section_creates_points() {
        let anchor = Point::DEFAULT;
        let config = IterationConfig::new(5.0, DurationType::Distance);

        let result = build(
            &anchor,
            &config,
            false,
            false,
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            1.1,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
        assert_eq!(result[0], anchor);
    }

    #[test]
    fn build_with_steering_creates_points() {
        let anchor = Point::DEFAULT;
        let config = IterationConfig::new(0.5, DurationType::Time);

        let result = build(
            &anchor,
            &config,
            false,
            true,
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            &[],
            1.1,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
        assert_eq!(result[0], anchor);
    }

    #[test]
    fn step_geometric_no_steering_updates_position() {
        let anchor = Point::DEFAULT;
        let mut accumulated_roll = 0.0;

        let result = step_geometric(
            &anchor,
            1.1,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            false,
            false,
            0.0,
            &mut accumulated_roll,
        );

        assert_ne!(result.heart_position, anchor.heart_position);
    }

    #[test]
    fn step_geometric_with_steering_updates_position() {
        let anchor = Point::DEFAULT;
        let mut accumulated_roll = 0.0;

        let result = step_geometric(
            &anchor,
            1.1,
            0.0,
            0.0,
            0.1,
            0.0,
            0.0,
            false,
            true,
            0.1,
            &mut accumulated_roll,
        );

        assert_ne!(result.heart_position, anchor.heart_position);
        assert!(accumulated_roll.abs() > 0.0);
    }
}
