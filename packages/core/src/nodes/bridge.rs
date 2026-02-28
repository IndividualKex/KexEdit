use crate::sim::{evaluate, physics, Float3, Frame, Keyframe, Point, Quaternion};

use super::forces::compute_force_vector;

const MAX_ITERATIONS: usize = 1_000_000;

pub struct BridgeNode;

impl BridgeNode {
    #[allow(clippy::too_many_arguments)]
    pub fn build(
        anchor: &Point,
        target_anchor: &Point,
        in_weight: f32,
        out_weight: f32,
        driven: bool,
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

        let vector = target_anchor.heart_position - anchor.heart_position;
        let length = vector.magnitude();

        if length < physics::EPSILON {
            return result;
        }

        let clamped_out_weight = out_weight.clamp(1e-3, 1.0);
        let clamped_in_weight = in_weight.clamp(1e-3, 1.0);

        let bridge_path = create_bridge_path(
            anchor,
            target_anchor,
            length,
            clamped_out_weight,
            clamped_in_weight,
        );

        if bridge_path.len() < 2 {
            return result;
        }

        let mut path_distance = 0.0;
        let end_distance = bridge_path[bridge_path.len() - 1].total_length;
        let mut path_index = 0;
        let mut iters = 0;

        let mut state = *anchor;
        let mut prev_heart_offset = anchor_heart;

        while path_distance < end_distance {
            if iters > MAX_ITERATIONS {
                break;
            }
            iters += 1;

            let mut prev = state;
            let t = (result.len() - 1) as f32 / physics::HZ;

            let mut advance_velocity = prev.velocity;
            if driven {
                let velocity = evaluate(driven_velocity, t, prev.velocity);
                if velocity < physics::MIN_VELOCITY {
                    break;
                }
                prev = prev.with_velocity(velocity, true);
                advance_velocity = velocity;
            } else if prev.velocity < physics::MIN_VELOCITY {
                if prev.frame().pitch() < 0.0 {
                    advance_velocity = physics::MIN_VELOCITY;
                } else {
                    break;
                }
            }

            let heart_offset_val = evaluate(heart_offset, t, anchor_heart);
            let friction_val = evaluate(friction, t, anchor_friction);
            let resistance_val = evaluate(resistance, t, anchor_resistance);

            let expected_advancement = advance_velocity / physics::HZ;
            let desired_distance = path_distance + expected_advancement;

            let (start, end, interp_t) = project(&bridge_path, &mut path_index, desired_distance);

            let (position, direction, lateral, normal, _roll);
            if interp_t < 0.0 {
                position = end.position;
                direction = end.direction;
                lateral = end.lateral;
                normal = end.normal;
                _roll = end.roll;
                path_distance = bridge_path[bridge_path.len() - 1].total_length;
            } else {
                position = start.position + (end.position - start.position) * interp_t;
                direction =
                    (start.direction + (end.direction - start.direction) * interp_t).normalize();
                lateral = (start.lateral + (end.lateral - start.lateral) * interp_t).normalize();
                normal = (start.normal + (end.normal - start.normal) * interp_t).normalize();

                let mut roll_diff = end.roll - start.roll;
                if roll_diff > std::f32::consts::PI {
                    roll_diff -= 2.0 * std::f32::consts::PI;
                }
                if roll_diff < -std::f32::consts::PI {
                    roll_diff += 2.0 * std::f32::consts::PI;
                }
                _roll = start.roll + roll_diff * interp_t;
                path_distance += expected_advancement;
            }

            let curr_frame = Frame::new(direction, normal, lateral);
            let curr_spine_pos = curr_frame.spine_position(position, heart_offset_val);
            // Use current heart_offset_val for both (matches Unity)
            let prev_spine_pos = prev
                .frame()
                .spine_position(prev.heart_position, heart_offset_val);
            let spine_advance = (curr_spine_pos - prev_spine_pos).magnitude();
            let heart_advance = (position - prev.heart_position).magnitude();
            let heart_arc = prev.heart_arc + heart_advance;
            let spine_arc = prev.spine_arc + spine_advance;

            // Use delta-based velocity update for numerical stability
            let new_velocity = if driven {
                evaluate(driven_velocity, t, prev.velocity)
            } else {
                let prev_center_y = prev
                    .frame()
                    .spine_position(prev.heart_position, prev_heart_offset * 0.9)
                    .y;
                let curr_center_y = curr_frame
                    .spine_position(position, heart_offset_val * 0.9)
                    .y;
                let delta_y = curr_center_y - prev_center_y;
                physics::update_velocity(
                    prev.velocity,
                    delta_y,
                    spine_advance,
                    friction_val,
                    resistance_val,
                )
            };

            let force_vec = compute_force_vector(&prev, &curr_frame, heart_advance, new_velocity);
            let normal_force = -force_vec.dot(normal);
            let lateral_force = -force_vec.dot(lateral);

            state = Point::new(
                position,
                direction,
                normal,
                lateral,
                new_velocity,
                normal_force,
                lateral_force,
                heart_arc,
                spine_arc,
                spine_advance,
                state.friction_origin,
                anchor.roll_speed,
                heart_offset_val,
                friction_val,
                resistance_val,
            );

            if state.velocity > physics::MAX_VELOCITY {
                break;
            }
            let force_magnitude =
                (normal_force * normal_force + lateral_force * lateral_force).sqrt();
            if force_magnitude > physics::MAX_FORCE {
                break;
            }

            result.push(state);
            prev_heart_offset = heart_offset_val;
        }

        result
    }
}

#[derive(Debug, Clone, Copy)]
struct PathPoint {
    position: Float3,
    direction: Float3,
    lateral: Float3,
    normal: Float3,
    roll: f32,
    total_length: f32,
}

fn create_bridge_path(
    source: &Point,
    target: &Point,
    length: f32,
    out_weight: f32,
    in_weight: f32,
) -> Vec<PathPoint> {
    let path_points = (length * 2.0).max(10.0) as usize;

    let p0 = source.heart_position;
    let p1 = source.heart_position + source.direction * (length * out_weight);
    let p2 = target.heart_position - target.direction * (length * in_weight);
    let p3 = target.heart_position;

    let source_roll = source.frame().roll();
    let target_roll = target.frame().roll();

    let up = Float3::new(0.0, 1.0, 0.0);
    let right = Float3::new(1.0, 0.0, 0.0);

    let mut path: Vec<PathPoint> = Vec::new();

    for i in 0..=path_points {
        let t = i as f32 / path_points as f32;

        let position = cubic_bezier(p0, p1, p2, p3, t);
        let direction = cubic_bezier_derivative(p0, p1, p2, p3, t).normalize();

        let mut roll_diff = target_roll - source_roll;
        if roll_diff > std::f32::consts::PI {
            roll_diff -= 2.0 * std::f32::consts::PI;
        }
        if roll_diff < -std::f32::consts::PI {
            roll_diff += 2.0 * std::f32::consts::PI;
        }
        let roll = source_roll + roll_diff * smoothstep(t);

        let mut lateral = direction.cross(up).normalize();
        if lateral.magnitude() < physics::EPSILON {
            lateral = right;
        }
        let mut normal = direction.cross(lateral).normalize();

        if roll.abs() > physics::EPSILON {
            let roll_quat = Quaternion::from_axis_angle(direction, -roll);
            lateral = roll_quat.mul_vec(lateral).normalize();
            normal = direction.cross(lateral).normalize();
        }

        let total_length = if i == 0 {
            0.0
        } else {
            path[i - 1].total_length + (position - path[i - 1].position).magnitude()
        };

        path.push(PathPoint {
            position,
            direction,
            lateral,
            normal,
            roll,
            total_length,
        });
    }

    path
}

fn project(
    path: &[PathPoint],
    path_index: &mut usize,
    distance: f32,
) -> (PathPoint, PathPoint, f32) {
    if distance >= path[path.len() - 1].total_length {
        *path_index = path.len() - 1;
        let end = path[path.len() - 1];
        return (end, end, -1.0);
    }

    for i in *path_index..path.len() - 1 {
        if path[i + 1].total_length >= distance {
            *path_index = i;
            break;
        }
    }

    let start = path[*path_index];
    let end = path[*path_index + 1];
    let denom = end.total_length - start.total_length;
    let t = if denom > physics::EPSILON {
        ((distance - start.total_length) / denom).clamp(0.0, 1.0)
    } else {
        0.0
    };

    (start, end, t)
}

fn cubic_bezier(p0: Float3, p1: Float3, p2: Float3, p3: Float3, t: f32) -> Float3 {
    let u = 1.0 - t;
    let uu = u * u;
    let uuu = uu * u;
    let tt = t * t;
    let ttt = tt * t;
    p0 * uuu + p1 * (3.0 * uu * t) + p2 * (3.0 * u * tt) + p3 * ttt
}

fn cubic_bezier_derivative(p0: Float3, p1: Float3, p2: Float3, p3: Float3, t: f32) -> Float3 {
    let u = 1.0 - t;
    (p1 - p0) * (3.0 * u * u) + (p2 - p1) * (6.0 * u * t) + (p3 - p2) * (3.0 * t * t)
}

fn smoothstep(t: f32) -> f32 {
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bridge_node_basic() {
        let anchor = Point::new(
            Float3::new(0.0, 0.0, 0.0),
            Float3::new(1.0, 0.0, 0.0),
            Float3::new(0.0, 1.0, 0.0),
            Float3::new(0.0, 0.0, 1.0),
            10.0,
            1.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
        );

        let target = Point::new(
            Float3::new(10.0, 5.0, 0.0),
            Float3::new(1.0, 0.0, 0.0),
            Float3::new(0.0, 1.0, 0.0),
            Float3::new(0.0, 0.0, 1.0),
            10.0,
            1.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
        );

        let result = BridgeNode::build(
            &anchor,
            &target,
            0.33,
            0.33,
            true,
            &[Keyframe::simple(0.0, 10.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            0.0,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
        assert!((result[0].heart_position - anchor.heart_position).magnitude() < 0.01);
    }

    #[test]
    fn bridge_node_zero_length() {
        let anchor = Point::new(
            Float3::new(0.0, 0.0, 0.0),
            Float3::new(1.0, 0.0, 0.0),
            Float3::new(0.0, 1.0, 0.0),
            Float3::new(0.0, 0.0, 1.0),
            10.0,
            1.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
        );

        let result = BridgeNode::build(
            &anchor,
            &anchor,
            0.33,
            0.33,
            true,
            &[Keyframe::simple(0.0, 10.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            0.0,
            0.0,
            0.0,
        );

        assert_eq!(result.len(), 1);
    }
}
