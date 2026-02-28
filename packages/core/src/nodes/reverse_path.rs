use crate::sim::Point;

pub fn build(path: &[Point]) -> Vec<Point> {
    if path.is_empty() {
        return Vec::new();
    }

    let total_heart_arc = path.last().map(|p| p.heart_arc).unwrap_or(0.0);
    let total_spine_arc = path.last().map(|p| p.spine_arc).unwrap_or(0.0);

    path.iter()
        .rev()
        .map(|p| {
            Point::new(
                p.heart_position,
                -p.direction,
                p.normal,
                -p.lateral,
                p.velocity,
                p.normal_force,
                -p.lateral_force,
                total_heart_arc - p.heart_arc,
                total_spine_arc - p.spine_arc,
                p.heart_advance,
                0.0, // Reset friction_origin like C#
                p.roll_speed,
                p.heart_offset,
                p.friction,
                p.resistance,
            )
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;

    fn make_point_with_arc(spine_arc: f32, heart_arc: f32) -> Point {
        Point::new(
            Float3::ZERO,
            Float3::BACK,
            Float3::UP,
            Float3::RIGHT,
            10.0,
            1.0,
            0.0,
            heart_arc,
            spine_arc,
            0.1,
            0.0,
            0.0,
            1.1,
            0.0,
            0.0,
        )
    }

    #[test]
    fn build_empty_path_returns_empty() {
        let result = build(&[]);
        assert_eq!(result.len(), 0);
    }

    #[test]
    fn build_reverses_path_order_and_transforms_arcs() {
        let path = vec![
            make_point_with_arc(0.0, 0.0),
            make_point_with_arc(10.0, 10.0),
            make_point_with_arc(30.0, 30.0),
        ];

        let result = build(&path);

        assert_eq!(result.len(), 3);
        // After reversal, first point (was last) should have arc 0
        assert!((result[0].spine_arc - 0.0).abs() < 0.001);
        assert!((result[0].heart_arc - 0.0).abs() < 0.001);
        // Middle point
        assert!((result[1].spine_arc - 20.0).abs() < 0.001);
        assert!((result[1].heart_arc - 20.0).abs() < 0.001);
        // Last point (was first) should have total arc
        assert!((result[2].spine_arc - 30.0).abs() < 0.001);
        assert!((result[2].heart_arc - 30.0).abs() < 0.001);
    }

    #[test]
    fn build_reverses_direction_for_each_point() {
        let path = vec![Point::DEFAULT];
        let result = build(&path);

        assert_eq!(result[0].direction, -path[0].direction);
        assert_eq!(result[0].lateral, -path[0].lateral);
    }

    #[test]
    fn build_resets_friction_origin() {
        let mut p = Point::DEFAULT;
        p = p.with_friction_origin(5.0);
        let path = vec![p];

        let result = build(&path);

        assert_eq!(result[0].friction_origin, 0.0);
    }
}
