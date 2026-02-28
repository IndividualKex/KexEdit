use crate::sim::{
    evaluate, physics, Curvature, Float3, Forces, Frame, Keyframe, Point, Quaternion,
};

const MAX_ITERATIONS: usize = 1_000_000;

pub struct CurvedNode;

impl CurvedNode {
    #[allow(clippy::too_many_arguments)]
    pub fn build(
        anchor: &Point,
        radius: f32,
        arc: f32,
        axis: f32,
        lead_in: f32,
        lead_out: f32,
        driven: bool,
        roll_speed: &[Keyframe],
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
        let mut angle = 0.0;
        let lead_out_start_angle = arc - lead_out;
        let mut lead_out_started = false;
        let mut lead_out_start_state = *anchor;
        let mut actual_lead_out = 0.0;

        let mut iterations = 0;
        let mut index = 0;

        while angle < arc - physics::EPSILON {
            iterations += 1;
            if iterations > MAX_ITERATIONS {
                break;
            }

            let mut prev = state;
            let t = index as f32 / physics::HZ;

            if driven {
                let velocity = evaluate(driven_velocity, t, prev.velocity);
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

            let heart_offset_val = evaluate(heart_offset, t, anchor_heart);
            let friction_val = evaluate(friction, t, anchor_friction);
            let resistance_val = evaluate(resistance, t, anchor_resistance);

            let mut delta_angle = prev.velocity / radius / physics::HZ * 1.0f32.to_degrees();

            if lead_in > 0.0 {
                let distance_from_start = prev.heart_arc - anchor.heart_arc;
                let expected_lead_in_distance =
                    1.997 / physics::HZ * prev.velocity / delta_angle * lead_in;
                let f_trans = distance_from_start / expected_lead_in_distance;
                if f_trans <= 1.0 {
                    let dampening = f_trans * f_trans * (3.0 + f_trans * (-2.0));
                    delta_angle *= dampening;
                }
            }

            if !lead_out_started && angle > lead_out_start_angle {
                lead_out_started = true;
                lead_out_start_state = prev;
                actual_lead_out = arc - angle;
            }

            if lead_out_started && lead_out > 0.0 {
                let distance_from_lead_out_start = prev.heart_arc - lead_out_start_state.heart_arc;
                let expected_lead_out_distance =
                    1.997 / physics::HZ * prev.velocity / delta_angle * actual_lead_out;
                let f_trans = 1.0 - distance_from_lead_out_start / expected_lead_out_distance;
                if f_trans >= 0.0 {
                    let dampening = f_trans * f_trans * (3.0 + f_trans * (-2.0));
                    delta_angle *= dampening;
                } else {
                    break;
                }
            }

            angle += delta_angle;
            let roll_speed_val = evaluate(roll_speed, angle, 0.0);

            let curr = step_curved(
                &prev,
                axis,
                delta_angle,
                roll_speed_val,
                heart_offset_val,
                friction_val,
                resistance_val,
                driven,
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
            state = curr;
            index += 1;
        }

        result
    }
}

#[allow(clippy::too_many_arguments)]
fn step_curved(
    prev: &Point,
    axis: f32,
    delta_angle: f32,
    roll_speed_val: f32,
    heart_offset_val: f32,
    friction_val: f32,
    resistance_val: f32,
    driven: bool,
) -> Point {
    let prev_frame = prev.frame();

    let axis_rad = axis.to_radians();
    let curve_axis = prev.normal * -axis_rad.cos() + prev.lateral * axis_rad.sin();
    let curve_quat = Quaternion::from_axis_angle(curve_axis, delta_angle.to_radians());
    let curr_direction = curve_quat.mul_vec(prev.direction).normalize();

    let original_roll_deg = prev.lateral.y.atan2(-prev.normal.y).to_degrees();
    let original_roll = ((original_roll_deg + 540.0) % 360.0) - 180.0;

    let up = Float3::new(0.0, 1.0, 0.0);
    let right = Float3::new(1.0, 0.0, 0.0);

    let mut curr_lateral = curr_direction.cross(up).normalize();
    if curr_lateral.magnitude() < physics::EPSILON {
        curr_lateral = right;
    }
    let mut curr_normal = curr_direction.cross(curr_lateral).normalize();

    if original_roll.abs() > physics::EPSILON {
        let preserved_roll_quat =
            Quaternion::from_axis_angle(curr_direction, -original_roll.to_radians());
        curr_lateral = preserved_roll_quat.mul_vec(curr_lateral).normalize();
        curr_normal = curr_direction.cross(curr_lateral).normalize();
    }

    let half_step_distance = prev.velocity / (2.0 * physics::HZ);
    let prev_spine_pos = prev.heart_position + prev.normal * heart_offset_val;
    let curr_spine_pos_if_heart_static = prev.heart_position + curr_normal * heart_offset_val;

    let curr_heart_position = prev.heart_position
        + curr_direction * half_step_distance
        + prev.direction * half_step_distance
        + (prev_spine_pos - curr_spine_pos_if_heart_static);

    let delta_roll = roll_speed_val / physics::HZ;
    let roll_quat = Quaternion::from_axis_angle(curr_direction, -delta_roll);
    curr_lateral = roll_quat.mul_vec(curr_lateral).normalize();
    curr_normal = curr_direction.cross(curr_lateral).normalize();

    let spine_advance =
        (curr_heart_position + curr_normal * heart_offset_val - prev_spine_pos).magnitude();
    let heart_advance = (curr_heart_position - prev.heart_position).magnitude();
    let new_heart_arc = prev.heart_arc + heart_advance;
    let new_spine_arc = prev.spine_arc + spine_advance;

    // Use delta-based velocity update for numerical stability
    let new_velocity = if !driven {
        let prev_center_y = (prev.heart_position + prev.normal * (0.9 * heart_offset_val)).y;
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
    fn curved_node_basic_arc() {
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

        let result = CurvedNode::build(
            &anchor,
            10.0,
            90.0,
            0.0,
            0.0,
            0.0,
            true,
            &[],
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
    fn curved_node_with_lead_in() {
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

        let result = CurvedNode::build(
            &anchor,
            10.0,
            45.0,
            0.0,
            10.0,
            0.0,
            true,
            &[],
            &[Keyframe::simple(0.0, 10.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            0.0,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
    }

    #[test]
    fn curved_node_with_lead_out() {
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

        let result = CurvedNode::build(
            &anchor,
            10.0,
            45.0,
            0.0,
            0.0,
            10.0,
            true,
            &[],
            &[Keyframe::simple(0.0, 10.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            &[Keyframe::simple(0.0, 0.0)],
            0.0,
            0.0,
            0.0,
        );

        assert!(result.len() > 1);
    }
}
