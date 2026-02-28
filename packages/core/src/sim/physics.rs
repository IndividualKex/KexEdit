use std::f32::consts::PI;

pub const G: f32 = 9.80665;
pub const HZ: f32 = 100.0;
pub const DT: f32 = 1.0 / HZ;
pub const EPSILON: f32 = 1.192_093e-7;
pub const MIN_VELOCITY: f32 = 0.1;
pub const MAX_VELOCITY: f32 = 150.0;
pub const MAX_FORCE: f32 = 10.0;

pub fn wrap_angle(rad: f32) -> f32 {
    if (-PI..=PI).contains(&rad) {
        return rad;
    }
    const TWO_PI: f32 = 2.0 * PI;
    const THREE_PI: f32 = 3.0 * PI;
    (rad + THREE_PI) % TWO_PI - PI
}

/// Update velocity using delta-based formulation (numerically stable).
///
/// This avoids catastrophic cancellation by working with small deltas
/// rather than large absolute energy values.
///
/// # Arguments
/// * `prev_velocity` - Velocity at the previous timestep
/// * `delta_y` - Change in center Y position (curr_center_y - prev_center_y)
/// * `delta_distance` - Distance traveled this step (for friction calculation)
/// * `friction` - Friction coefficient
/// * `resistance` - Air resistance coefficient
pub fn update_velocity(
    prev_velocity: f32,
    delta_y: f32,
    delta_distance: f32,
    friction: f32,
    resistance: f32,
) -> f32 {
    // Change in potential energy: gravity + friction work
    let delta_pe = G * delta_y + G * friction * delta_distance;
    // Energy lost to air resistance (drag)
    let drag_loss = prev_velocity * prev_velocity * prev_velocity * resistance * DT;
    // New kinetic energy = old KE - delta_PE - drag_loss
    // KE = 0.5 * v^2, so v^2 = 2 * KE
    let v_squared = prev_velocity * prev_velocity - 2.0 * delta_pe - 2.0 * drag_loss;
    v_squared.max(0.0).sqrt()
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-4;

    #[test]
    fn constants_gravity_matches_standard() {
        assert_relative_eq!(G, 9.80665, epsilon = TOLERANCE);
    }

    #[test]
    fn constants_simulation_rate() {
        assert_relative_eq!(HZ, 100.0, epsilon = TOLERANCE);
    }

    #[test]
    fn dt_equals_one_over_hz() {
        assert_relative_eq!(DT, 1.0 / HZ, epsilon = TOLERANCE);
        assert_relative_eq!(DT, 0.01, epsilon = TOLERANCE);
    }

    #[test]
    fn constants_min_velocity() {
        assert_relative_eq!(MIN_VELOCITY, 0.1, epsilon = TOLERANCE);
    }

    #[test]
    fn wrap_angle_in_range_unchanged() {
        let angles = [0.0, 0.5, -0.5, PI - 0.1, -PI + 0.1];
        for angle in angles {
            let wrapped = wrap_angle(angle);
            assert_relative_eq!(wrapped, angle, epsilon = TOLERANCE);
        }
    }

    #[test]
    fn wrap_angle_greater_than_pi_wraps_negative() {
        let angle = PI + 0.5;
        let wrapped = wrap_angle(angle);

        assert!(wrapped > -PI);
        assert!(wrapped <= PI);
        assert!(wrapped < 0.0);
    }

    #[test]
    fn wrap_angle_less_than_negative_pi_wraps_positive() {
        let angle = -PI - 0.5;
        let wrapped = wrap_angle(angle);

        assert!(wrapped > -PI);
        assert!(wrapped <= PI);
        assert!(wrapped > 0.0);
    }

    #[test]
    fn wrap_angle_exactly_pi_returns_valid_range() {
        let wrapped = wrap_angle(PI);
        assert!(wrapped >= -PI);
        assert!(wrapped <= PI);
    }

    #[test]
    fn wrap_angle_large_positive_wraps_correctly() {
        let angle = 2.0 * PI + 0.3;
        let wrapped = wrap_angle(angle);

        assert!(wrapped > -PI);
        assert!(wrapped <= PI);
        assert_relative_eq!(wrapped, 0.3, epsilon = TOLERANCE);
    }

    #[test]
    fn wrap_angle_moderate_negative_wraps_correctly() {
        let angle = -2.0 * PI - 0.3;
        let wrapped = wrap_angle(angle);

        assert!(wrapped > -PI);
        assert!(wrapped <= PI);
    }

    #[test]
    fn update_velocity_no_change_when_flat() {
        let prev_velocity = 10.0;
        let new_velocity = update_velocity(prev_velocity, 0.0, 0.0, 0.0, 0.0);
        assert_relative_eq!(new_velocity, prev_velocity, epsilon = TOLERANCE);
    }

    #[test]
    fn update_velocity_increases_going_downhill() {
        let prev_velocity = 10.0;
        let delta_y = -1.0;
        let new_velocity = update_velocity(prev_velocity, delta_y, 0.0, 0.0, 0.0);
        assert!(new_velocity > prev_velocity);
    }

    #[test]
    fn update_velocity_decreases_going_uphill() {
        let prev_velocity = 10.0;
        let delta_y = 1.0;
        let new_velocity = update_velocity(prev_velocity, delta_y, 0.0, 0.0, 0.0);
        assert!(new_velocity < prev_velocity);
    }

    #[test]
    fn update_velocity_friction_reduces_speed() {
        let prev_velocity = 10.0;
        let delta_distance = 1.0;
        let friction = 0.1;

        let vel_no_friction = update_velocity(prev_velocity, 0.0, delta_distance, 0.0, 0.0);
        let vel_with_friction = update_velocity(prev_velocity, 0.0, delta_distance, friction, 0.0);

        assert!(vel_with_friction < vel_no_friction);
    }

    #[test]
    fn update_velocity_resistance_reduces_speed() {
        let prev_velocity = 20.0;
        let resistance = 0.001;

        let vel_no_resistance = update_velocity(prev_velocity, 0.0, 0.0, 0.0, 0.0);
        let vel_with_resistance = update_velocity(prev_velocity, 0.0, 0.0, 0.0, resistance);

        assert!(vel_with_resistance < vel_no_resistance);
    }

    #[test]
    fn update_velocity_clamps_to_zero() {
        let prev_velocity = 1.0;
        let delta_y = 10.0;

        let new_velocity = update_velocity(prev_velocity, delta_y, 0.0, 0.0, 0.0);
        assert_relative_eq!(new_velocity, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn update_velocity_conserves_energy_no_losses() {
        let prev_velocity = 10.0;
        let delta_y = -1.0;

        let new_velocity = update_velocity(prev_velocity, delta_y, 0.0, 0.0, 0.0);

        let expected_v_squared = prev_velocity * prev_velocity - 2.0 * G * delta_y;
        let expected_velocity = expected_v_squared.sqrt();

        assert_relative_eq!(new_velocity, expected_velocity, epsilon = TOLERANCE);
    }

    #[test]
    fn update_velocity_cubic_resistance_scaling() {
        let slow_velocity = 5.0;
        let fast_velocity = 20.0;
        let resistance = 0.001;

        let slow_new = update_velocity(slow_velocity, 0.0, 0.0, 0.0, resistance);
        let fast_new = update_velocity(fast_velocity, 0.0, 0.0, 0.0, resistance);

        let slow_ke_loss = slow_velocity * slow_velocity - slow_new * slow_new;
        let fast_ke_loss = fast_velocity * fast_velocity - fast_new * fast_new;

        let expected_ratio = (fast_velocity / slow_velocity).powi(3);
        let actual_ratio = fast_ke_loss / slow_ke_loss;

        assert_relative_eq!(actual_ratio, expected_ratio, epsilon = 1.0);
    }
}
