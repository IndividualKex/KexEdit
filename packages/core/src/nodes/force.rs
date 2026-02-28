use crate::sim::{physics, Curvature, Forces, Frame, Keyframe, PhysicsParams, Point};

use super::{DurationType, IterationConfig};

const MAX_ITERATIONS: usize = 1_000_000;
const MAX_ANGLE_RATE: f32 = 0.5;

fn step_by_forces(
    prev: &Frame,
    normal_force: f32,
    lateral_force: f32,
    velocity: f32,
    heart_advance: f32,
) -> Frame {
    let force_vec = prev.normal * (-normal_force)
        + prev.lateral * (-lateral_force)
        + crate::sim::Float3::new(0.0, -1.0, 0.0);
    let normal_accel = -force_vec.dot(prev.normal) * physics::G;
    let lateral_accel = -force_vec.dot(prev.lateral) * physics::G;

    let estimated_velocity = if heart_advance.abs() < physics::EPSILON {
        velocity
    } else {
        heart_advance * physics::HZ
    };

    // Use safe threshold to prevent angle explosion at low velocities
    let safe_estimated = estimated_velocity.abs().max(physics::MIN_VELOCITY);
    let safe_velocity = velocity.abs().max(physics::MIN_VELOCITY);

    // Compute angles with clamping to prevent instability
    let normal_angle =
        (normal_accel / safe_estimated / physics::HZ).clamp(-MAX_ANGLE_RATE, MAX_ANGLE_RATE);
    let lateral_angle =
        (-lateral_accel / safe_velocity / physics::HZ).clamp(-MAX_ANGLE_RATE, MAX_ANGLE_RATE);

    let q_normal = crate::sim::Quaternion::from_axis_angle(prev.lateral, normal_angle);
    let q_lateral = crate::sim::Quaternion::from_axis_angle(prev.normal, lateral_angle);
    let combined = q_normal * q_lateral;

    let new_direction = combined.mul_vec(prev.direction).normalize();
    let new_lateral = q_lateral.mul_vec(prev.lateral).normalize();
    let new_normal = new_direction.cross(new_lateral).normalize();

    Frame::new(new_direction, new_normal, new_lateral).reorthonormalize()
}

pub fn advance(
    prev: &Point,
    target_normal_force: f32,
    target_lateral_force: f32,
    physics: &PhysicsParams,
    roll_speed_val: f32,
) -> Point {
    let prev_frame = prev.frame();
    let new_frame = step_by_forces(
        &prev_frame,
        target_normal_force,
        target_lateral_force,
        prev.velocity,
        prev.heart_advance,
    );

    let curr_direction = new_frame.direction;
    let mut curr_normal = new_frame.normal;

    let half_step_distance = prev.velocity / (2.0 * physics::HZ);
    let prev_spine_pos = prev.spine_position(physics.heart_offset);
    let curr_spine_pos_if_heart_static = prev.heart_position + curr_normal * physics.heart_offset;

    let displacement = curr_direction * half_step_distance
        + prev.direction * half_step_distance
        + (prev_spine_pos - curr_spine_pos_if_heart_static);
    let curr_heart_position = prev.heart_position + displacement;

    let rolled_frame = new_frame.with_roll(physics.delta_roll);
    let curr_lateral = rolled_frame.lateral;
    curr_normal = rolled_frame.normal;

    let spine_advance = ((curr_heart_position + curr_normal * physics.heart_offset)
        - prev.spine_position(physics.heart_offset))
    .magnitude();
    let new_spine_arc = prev.spine_arc + spine_advance;
    let heart_advance = (curr_heart_position - prev.heart_position).magnitude();
    let new_heart_arc = prev.heart_arc + heart_advance;

    // Use delta-based velocity update for numerical stability
    let new_velocity = if !physics.driven {
        let prev_center_y = (prev.heart_position + prev.normal * (0.9 * physics.heart_offset)).y;
        let curr_center_y = (curr_heart_position + curr_normal * (0.9 * physics.heart_offset)).y;
        let delta_y = curr_center_y - prev_center_y;
        physics::update_velocity(
            prev.velocity,
            delta_y,
            spine_advance,
            physics.friction,
            physics.resistance,
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
        physics.heart_offset,
        physics.friction,
        physics.resistance,
    )
}

#[allow(clippy::too_many_arguments)]
pub fn build(
    anchor: &Point,
    config: &IterationConfig,
    driven: bool,
    roll_speed: &[Keyframe],
    normal_force: &[Keyframe],
    lateral_force: &[Keyframe],
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

    match config.duration_type {
        DurationType::Time => {
            build_time_section(
                config.duration,
                driven,
                roll_speed,
                normal_force,
                lateral_force,
                driven_velocity,
                heart_offset,
                friction,
                resistance,
                anchor_heart,
                anchor_friction,
                anchor_resistance,
                &mut state,
                &mut result,
            );
        }
        DurationType::Distance => {
            build_distance_section(
                config.duration,
                driven,
                anchor.spine_arc,
                roll_speed,
                normal_force,
                lateral_force,
                driven_velocity,
                heart_offset,
                friction,
                resistance,
                anchor_heart,
                anchor_friction,
                anchor_resistance,
                &mut state,
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
    roll_speed: &[Keyframe],
    normal_force: &[Keyframe],
    lateral_force: &[Keyframe],
    driven_velocity: &[Keyframe],
    heart_offset: &[Keyframe],
    friction: &[Keyframe],
    resistance: &[Keyframe],
    anchor_heart: f32,
    anchor_friction: f32,
    anchor_resistance: f32,
    state: &mut Point,
    result: &mut Vec<Point>,
) {
    let point_count = (physics::HZ * duration).floor() as usize;
    for i in 1..point_count {
        let t = i as f32 / physics::HZ;

        let mut prev = *state;

        let heart_offset_val = crate::sim::evaluate(heart_offset, t, anchor_heart);
        let friction_val = crate::sim::evaluate(friction, t, anchor_friction);
        let resistance_val = crate::sim::evaluate(resistance, t, anchor_resistance);

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

        let target_normal_force = crate::sim::evaluate(normal_force, t, 1.0);
        let target_lateral_force = crate::sim::evaluate(lateral_force, t, 0.0);
        let roll_speed_val = crate::sim::evaluate(roll_speed, t, 0.0);
        let delta_roll = roll_speed_val / physics::HZ;

        let physics = PhysicsParams::new(
            heart_offset_val,
            friction_val,
            resistance_val,
            delta_roll,
            driven,
        );
        let curr = advance(
            &prev,
            target_normal_force,
            target_lateral_force,
            &physics,
            roll_speed_val,
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
    anchor_spine_arc: f32,
    roll_speed: &[Keyframe],
    normal_force: &[Keyframe],
    lateral_force: &[Keyframe],
    driven_velocity: &[Keyframe],
    heart_offset: &[Keyframe],
    friction: &[Keyframe],
    resistance: &[Keyframe],
    anchor_heart: f32,
    anchor_friction: f32,
    anchor_resistance: f32,
    state: &mut Point,
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

        let heart_offset_val = crate::sim::evaluate(heart_offset, d, anchor_heart);
        let friction_val = crate::sim::evaluate(friction, d, anchor_friction);
        let resistance_val = crate::sim::evaluate(resistance, d, anchor_resistance);

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

        let target_normal_force = crate::sim::evaluate(normal_force, d, 1.0);
        let target_lateral_force = crate::sim::evaluate(lateral_force, d, 0.0);
        let roll_speed_val = crate::sim::evaluate(roll_speed, d, 0.0);
        let delta_roll = roll_speed_val * (prev.velocity / physics::HZ);

        let physics = PhysicsParams::new(
            heart_offset_val,
            friction_val,
            resistance_val,
            delta_roll,
            driven,
        );
        let curr = advance(
            &prev,
            target_normal_force,
            target_lateral_force,
            &physics,
            roll_speed_val,
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

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;

    #[test]
    fn advance_updates_position() {
        let anchor = Point::DEFAULT;
        let physics = PhysicsParams::new(1.1, 0.0, 0.0, 0.0, false);

        let result = advance(&anchor, 1.0, 0.0, &physics, 0.0);

        assert_ne!(result.heart_position, anchor.heart_position);
        assert!(result.heart_advance > 0.0);
    }

    #[test]
    fn advance_updates_velocity_when_not_driven() {
        let anchor = Point::create(
            Float3::new(0.0, 10.0, 0.0),
            Float3::BACK,
            0.0,
            10.0,
            1.1,
            0.0,
            0.0,
        );
        let physics = PhysicsParams::new(1.1, 0.0, 0.0, 0.0, false);

        let result = advance(&anchor, 1.0, 0.0, &physics, 0.0);

        assert!(result.velocity > 0.0);
    }

    #[test]
    fn build_time_section_creates_points() {
        let anchor = Point::DEFAULT;
        let config = IterationConfig::new(0.5, DurationType::Time);

        let result = build(
            &anchor,
            &config,
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
    fn build_respects_min_velocity_threshold() {
        let slow_anchor = Point::create(
            Float3::new(0.0, 3.0, 0.0),
            Float3::BACK,
            0.0,
            0.001,
            1.1,
            0.0,
            0.0,
        );
        let config = IterationConfig::new(1.0, DurationType::Time);

        let result = build(
            &slow_anchor,
            &config,
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

        assert!(result.len() <= (physics::HZ * config.duration).round() as usize);
    }
}
