use crate::sim::{evaluate, physics, Frame, Keyframe, Matrix3, Point};

use super::forces::compute_force_vector;

const MAX_ITERATIONS: usize = 1_000_000;

pub struct CopyPathNode;

impl CopyPathNode {
    #[allow(clippy::too_many_arguments)]
    pub fn build(
        anchor: &Point,
        source_path: &[Point],
        start: f32,
        end: f32,
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

        if source_path.len() < 2 {
            return result;
        }

        let start_index = if start <= 0.0 {
            0
        } else {
            ((start * physics::HZ).round() as usize).clamp(0, source_path.len() - 1)
        };

        let end_index = if end < 0.0 {
            source_path.len() - 1
        } else {
            ((end * physics::HZ).round() as usize).clamp(start_index, source_path.len() - 1)
        };

        if end_index - start_index < 1 {
            return result;
        }

        let path_start = source_path[start_index];
        let path_basis =
            Matrix3::from_columns(path_start.lateral, path_start.normal, path_start.direction);
        let anchor_basis = Matrix3::from_columns(anchor.lateral, anchor.normal, anchor.direction);
        let rotation = anchor_basis.multiply(&path_basis.transpose());
        let translation =
            anchor.heart_position - rotation.multiply_vector(path_start.heart_position);

        let mut distance = source_path[start_index].spine_arc;
        let end_distance = source_path[end_index].spine_arc;
        let mut index = 0;
        let mut iters = 0;

        let mut state = *anchor;
        let mut prev_heart_offset = anchor_heart;

        while distance < end_distance {
            if iters > MAX_ITERATIONS {
                break;
            }
            iters += 1;

            let mut prev = state;
            let t = index as f32 / physics::HZ;

            let mut advance_velocity = prev.velocity;
            if driven {
                let velocity = evaluate(driven_velocity, t, prev.velocity);
                if velocity < physics::MIN_VELOCITY {
                    break;
                }
                prev = prev.with_velocity(velocity, true);
                advance_velocity = velocity;
            } else if prev.velocity < physics::MIN_VELOCITY {
                let pitch = prev.frame().pitch();
                if pitch < 0.0 {
                    prev = prev.with_velocity(physics::MIN_VELOCITY, true);
                    advance_velocity = physics::MIN_VELOCITY;
                } else {
                    break;
                }
            }

            let heart_offset_val = evaluate(heart_offset, t, anchor_heart);
            let friction_val = evaluate(friction, t, anchor_friction);
            let resistance_val = evaluate(resistance, t, anchor_resistance);

            let expected_advancement = advance_velocity / physics::HZ;
            let desired_distance = distance + expected_advancement;
            let (start_point, end_point, interp_t) = project(
                source_path,
                &mut index,
                desired_distance,
                start_index,
                end_index,
            );

            let (mut position, mut direction, mut lateral, mut normal);
            if interp_t < 0.0 {
                position = end_point.heart_position;
                direction = end_point.direction;
                lateral = end_point.lateral;
                normal = end_point.normal;
                distance = end_distance;
            } else {
                position = start_point.heart_position
                    + (end_point.heart_position - start_point.heart_position) * interp_t;
                direction = (start_point.direction
                    + (end_point.direction - start_point.direction) * interp_t)
                    .normalize();
                lateral = (start_point.lateral
                    + (end_point.lateral - start_point.lateral) * interp_t)
                    .normalize();
                normal = (start_point.normal + (end_point.normal - start_point.normal) * interp_t)
                    .normalize();
                distance += expected_advancement;
            }

            position = rotation.multiply_vector(position) + translation;
            direction = rotation.multiply_vector(direction);
            lateral = rotation.multiply_vector(lateral);
            normal = rotation.multiply_vector(normal);

            let curr_frame = Frame::new(direction, normal, lateral);
            let curr_spine_pos = curr_frame.spine_position(position, heart_offset_val);
            let prev_spine_pos = prev
                .frame()
                .spine_position(prev.heart_position, prev_heart_offset);
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
                prev.friction_origin,
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

fn project(
    source_path: &[Point],
    index: &mut usize,
    distance: f32,
    start_index: usize,
    end_index: usize,
) -> (Point, Point, f32) {
    if distance >= source_path[end_index].spine_arc {
        *index = end_index - start_index;
        let end = source_path[end_index];
        return (end, end, -1.0);
    }

    for i in (start_index + *index)..end_index {
        if source_path[i + 1].spine_arc >= distance {
            *index = i - start_index;
            break;
        }
    }

    let start = source_path[start_index + *index];
    let end = source_path[start_index + *index + 1];

    let denom = end.spine_arc - start.spine_arc;
    let t = if denom > physics::EPSILON {
        ((distance - start.spine_arc) / denom).clamp(0.0, 1.0)
    } else {
        0.0
    };

    (start, end, t)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;

    #[test]
    fn copy_path_node_basic() {
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

        let source_path = vec![
            Point::new(
                Float3::new(0.0, 0.0, 0.0),
                Float3::new(1.0, 0.0, 0.0),
                Float3::new(0.0, 1.0, 0.0),
                Float3::new(0.0, 0.0, 1.0),
                10.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
            ),
            Point::new(
                Float3::new(1.0, 0.0, 0.0),
                Float3::new(1.0, 0.0, 0.0),
                Float3::new(0.0, 1.0, 0.0),
                Float3::new(0.0, 0.0, 1.0),
                10.0,
                1.0,
                0.0,
                1.0,
                1.0,
                1.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
            ),
        ];

        let result = CopyPathNode::build(
            &anchor,
            &source_path,
            0.0,
            1.0,
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
    fn copy_path_node_empty_source() {
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

        let result = CopyPathNode::build(
            &anchor,
            &[],
            0.0,
            1.0,
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
