#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum InterpolationType {
    Constant,
    Linear,
    Bezier,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct Keyframe {
    pub time: f32,
    pub value: f32,
    pub in_interpolation: InterpolationType,
    pub out_interpolation: InterpolationType,
    pub in_tangent: f32,
    pub out_tangent: f32,
    pub in_weight: f32,
    pub out_weight: f32,
}

impl Keyframe {
    #[allow(clippy::too_many_arguments)]
    pub const fn new(
        time: f32,
        value: f32,
        in_interpolation: InterpolationType,
        out_interpolation: InterpolationType,
        in_tangent: f32,
        out_tangent: f32,
        in_weight: f32,
        out_weight: f32,
    ) -> Self {
        Self {
            time,
            value,
            in_interpolation,
            out_interpolation,
            in_tangent,
            out_tangent,
            in_weight,
            out_weight,
        }
    }

    pub const fn simple(time: f32, value: f32) -> Self {
        Self::new(
            time,
            value,
            InterpolationType::Bezier,
            InterpolationType::Bezier,
            0.0,
            0.0,
            1.0 / 3.0,
            1.0 / 3.0,
        )
    }
}

pub fn evaluate(keyframes: &[Keyframe], t: f32, default_value: f32) -> f32 {
    if keyframes.is_empty() {
        return default_value;
    }
    if t <= keyframes[0].time {
        return keyframes[0].value;
    }

    let i = keyframes
        .partition_point(|kf| kf.time <= t)
        .saturating_sub(1);

    if i >= keyframes.len() - 1 {
        return keyframes[keyframes.len() - 1].value;
    }

    let start = keyframes[i];
    let end = keyframes[i + 1];
    evaluate_segment(&start, &end, t)
}

pub fn evaluate_segment(start: &Keyframe, end: &Keyframe, t: f32) -> f32 {
    if matches!(start.out_interpolation, InterpolationType::Constant) {
        return start.value;
    }

    let interpolation_type = get_max_interpolation(start.out_interpolation, end.in_interpolation);

    match interpolation_type {
        InterpolationType::Linear => {
            let segment_t = (t - start.time) / (end.time - start.time);
            start.value + (end.value - start.value) * segment_t
        }
        InterpolationType::Bezier => evaluate_bezier_2d(start, end, t),
        InterpolationType::Constant => start.value,
    }
}

fn evaluate_bezier_2d(start: &Keyframe, end: &Keyframe, target_time: f32) -> f32 {
    let dt = end.time - start.time;

    let p0_x = start.time;
    let p0_y = start.value;
    let p1_x = start.time + (dt * start.out_weight);
    let p1_y = start.value + (start.out_tangent * dt * start.out_weight);
    let p2_x = end.time - (dt * end.in_weight);
    let p2_y = end.value - (end.in_tangent * dt * end.in_weight);
    let p3_x = end.time;
    let p3_y = end.value;

    let mut u = (target_time - start.time) / dt;
    for _ in 0..8 {
        let one_minus_u = 1.0 - u;
        let one_minus_u2 = one_minus_u * one_minus_u;
        let one_minus_u3 = one_minus_u2 * one_minus_u;
        let u2 = u * u;
        let u3 = u2 * u;

        let bezier_time = one_minus_u3 * p0_x
            + 3.0 * one_minus_u2 * u * p1_x
            + 3.0 * one_minus_u * u2 * p2_x
            + u3 * p3_x;

        let time_diff = bezier_time - target_time;
        if time_diff.abs() < 1e-6 {
            break;
        }

        let bezier_time_derivative = -3.0 * one_minus_u2 * p0_x + 3.0 * one_minus_u2 * p1_x
            - 6.0 * one_minus_u * u * p1_x
            + 6.0 * one_minus_u * u * p2_x
            - 3.0 * u2 * p2_x
            + 3.0 * u2 * p3_x;

        if bezier_time_derivative.abs() < 1e-9 {
            break;
        }

        u -= time_diff / bezier_time_derivative;
        u = u.clamp(0.0, 1.0);
    }

    let final_one_minus_u = 1.0 - u;
    let final_one_minus_u2 = final_one_minus_u * final_one_minus_u;
    let final_one_minus_u3 = final_one_minus_u2 * final_one_minus_u;
    let final_u2 = u * u;
    let final_u3 = final_u2 * u;

    final_one_minus_u3 * p0_y
        + 3.0 * final_one_minus_u2 * u * p1_y
        + 3.0 * final_one_minus_u * final_u2 * p2_y
        + final_u3 * p3_y
}

fn get_max_interpolation(a: InterpolationType, b: InterpolationType) -> InterpolationType {
    let a_val = a as i32;
    let b_val = b as i32;
    if a_val > b_val {
        a
    } else {
        b
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-4;

    fn linear_keyframe(time: f32, value: f32) -> Keyframe {
        Keyframe::new(
            time,
            value,
            InterpolationType::Linear,
            InterpolationType::Linear,
            0.0,
            0.0,
            1.0 / 3.0,
            1.0 / 3.0,
        )
    }

    fn constant_keyframe(time: f32, value: f32) -> Keyframe {
        Keyframe::new(
            time,
            value,
            InterpolationType::Constant,
            InterpolationType::Constant,
            0.0,
            0.0,
            1.0 / 3.0,
            1.0 / 3.0,
        )
    }

    #[test]
    fn simple_keyframe_has_bezier_defaults() {
        let kf = Keyframe::simple(1.0, 5.0);
        assert_relative_eq!(kf.time, 1.0, epsilon = TOLERANCE);
        assert_relative_eq!(kf.value, 5.0, epsilon = TOLERANCE);
        assert!(matches!(kf.in_interpolation, InterpolationType::Bezier));
        assert!(matches!(kf.out_interpolation, InterpolationType::Bezier));
        assert_relative_eq!(kf.in_weight, 1.0 / 3.0, epsilon = TOLERANCE);
        assert_relative_eq!(kf.out_weight, 1.0 / 3.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_empty_array_returns_default() {
        let keyframes: &[Keyframe] = &[];
        let result = evaluate(keyframes, 0.5, 42.0);
        assert_relative_eq!(result, 42.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_single_keyframe_returns_value() {
        let keyframes = &[Keyframe::simple(1.0, 5.0)];
        let result = evaluate(keyframes, 0.5, 0.0);
        assert_relative_eq!(result, 5.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_before_first_keyframe_returns_first_value() {
        let keyframes = &[Keyframe::simple(1.0, 10.0), Keyframe::simple(2.0, 20.0)];
        let result = evaluate(keyframes, 0.0, 0.0);
        assert_relative_eq!(result, 10.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_after_last_keyframe_returns_last_value() {
        let keyframes = &[Keyframe::simple(1.0, 10.0), Keyframe::simple(2.0, 20.0)];
        let result = evaluate(keyframes, 5.0, 0.0);
        assert_relative_eq!(result, 20.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_exactly_on_keyframe_returns_keyframe_value() {
        let keyframes = &[
            Keyframe::simple(1.0, 10.0),
            Keyframe::simple(2.0, 20.0),
            Keyframe::simple(3.0, 30.0),
        ];
        let result = evaluate(keyframes, 2.0, 0.0);
        assert_relative_eq!(result, 20.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_constant_interpolation_returns_start_value() {
        let keyframes = &[constant_keyframe(0.0, 10.0), constant_keyframe(2.0, 20.0)];
        let result = evaluate(keyframes, 1.0, 0.0);
        assert_relative_eq!(result, 10.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_linear_interpolation_interpolates_correctly() {
        let keyframes = &[linear_keyframe(0.0, 0.0), linear_keyframe(2.0, 10.0)];
        let result = evaluate(keyframes, 1.0, 0.0);
        assert_relative_eq!(result, 5.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_linear_interpolation_midpoint_returns_average() {
        let keyframes = &[linear_keyframe(0.0, 100.0), linear_keyframe(1.0, 200.0)];
        let result = evaluate(keyframes, 0.5, 0.0);
        assert_relative_eq!(result, 150.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_bezier_interpolation_smooth_curve() {
        let keyframes = &[Keyframe::simple(0.0, 0.0), Keyframe::simple(1.0, 1.0)];
        let result = evaluate(keyframes, 0.5, 0.0);
        assert!(
            result > 0.0,
            "Bezier should produce positive value at midpoint"
        );
        assert!(
            result < 1.0,
            "Bezier should produce value less than end at midpoint"
        );
    }

    #[test]
    fn evaluate_multiple_keyframes_finds_correct_segment() {
        let keyframes = &[
            linear_keyframe(0.0, 0.0),
            linear_keyframe(1.0, 10.0),
            linear_keyframe(2.0, 20.0),
            linear_keyframe(3.0, 30.0),
        ];

        let result1 = evaluate(keyframes, 0.5, 0.0);
        let result2 = evaluate(keyframes, 1.5, 0.0);
        let result3 = evaluate(keyframes, 2.5, 0.0);

        assert_relative_eq!(result1, 5.0, epsilon = TOLERANCE);
        assert_relative_eq!(result2, 15.0, epsilon = TOLERANCE);
        assert_relative_eq!(result3, 25.0, epsilon = TOLERANCE);
    }

    #[test]
    fn evaluate_linear_monotonic_behavior() {
        let keyframes = &[linear_keyframe(0.0, 0.0), linear_keyframe(1.0, 100.0)];

        for &t in &[0.1, 0.25, 0.5, 0.75, 0.9] {
            let result = evaluate(keyframes, t, 0.0);
            let expected = t * 100.0;
            assert_relative_eq!(result, expected, epsilon = TOLERANCE);
        }
    }
}
