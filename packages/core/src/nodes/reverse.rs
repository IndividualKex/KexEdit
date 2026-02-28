use crate::sim::Point;

pub fn build(anchor: &Point) -> Point {
    Point::new(
        anchor.heart_position,
        -anchor.direction,
        anchor.normal,
        -anchor.lateral,
        anchor.velocity,
        anchor.normal_force,
        anchor.lateral_force,
        anchor.heart_arc,
        anchor.spine_arc,
        anchor.heart_advance,
        anchor.friction_origin,
        anchor.roll_speed,
        anchor.heart_offset,
        anchor.friction,
        anchor.resistance,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn build_reverses_direction() {
        let anchor = Point::DEFAULT;
        let result = build(&anchor);

        assert_eq!(result.direction, -anchor.direction);
        assert_eq!(result.lateral, -anchor.lateral);
        assert_eq!(result.normal, anchor.normal);
    }

    #[test]
    fn build_preserves_position_and_velocity() {
        let anchor = Point::DEFAULT;
        let result = build(&anchor);

        assert_eq!(result.heart_position, anchor.heart_position);
        assert_eq!(result.velocity, anchor.velocity);
    }
}
