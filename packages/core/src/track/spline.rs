use crate::sim::{Float3, Point};

/// A resampled point along the spline with uniform arc-length spacing.
/// C-compatible layout for FFI.
#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq)]
pub struct SplinePoint {
    pub arc: f32,
    pub position: Float3,
    pub direction: Float3,
    pub normal: Float3,
    pub lateral: Float3,
}

impl SplinePoint {
    pub const fn new(
        arc: f32,
        position: Float3,
        direction: Float3,
        normal: Float3,
        lateral: Float3,
    ) -> Self {
        Self {
            arc,
            position,
            direction,
            normal,
            lateral,
        }
    }

    pub const DEFAULT: Self =
        Self::new(0.0, Float3::ZERO, Float3::BACK, Float3::DOWN, Float3::RIGHT);
}

impl Default for SplinePoint {
    fn default() -> Self {
        Self::DEFAULT
    }
}

/// Converts a simulation Point to a SplinePoint.
/// Position is computed at the spine (heart_position + normal * heart_offset).
pub fn to_spline_point(point: &Point) -> SplinePoint {
    SplinePoint::new(
        point.spine_arc,
        point.spine_position(point.heart_offset),
        point.direction,
        point.normal,
        point.lateral,
    )
}

/// Linear interpolation between two Float3 values.
fn lerp_float3(a: Float3, b: Float3, t: f32) -> Float3 {
    Float3::new(
        a.x + (b.x - a.x) * t,
        a.y + (b.y - a.y) * t,
        a.z + (b.z - a.z) * t,
    )
}

/// Interpolates a SplinePoint at the given arc position.
/// Uses binary search to find the bracketing segment, then linearly interpolates.
fn interpolate_at_arc(points: &[Point], arc: f32) -> SplinePoint {
    debug_assert!(
        !points.is_empty(),
        "interpolate_at_arc called with empty points"
    );

    // Handle boundary cases: clamp to endpoints
    if arc <= points[0].spine_arc {
        let sp = to_spline_point(&points[0]);
        return SplinePoint::new(arc, sp.position, sp.direction, sp.normal, sp.lateral);
    }

    let last = points.len() - 1;
    if arc >= points[last].spine_arc {
        let sp = to_spline_point(&points[last]);
        return SplinePoint::new(arc, sp.position, sp.direction, sp.normal, sp.lateral);
    }

    // Binary search to find segment [lo, lo+1] where points[lo].spine_arc <= arc < points[lo+1].spine_arc
    let mut lo = 0usize;
    let mut hi = last;

    while lo < hi - 1 {
        let mid = (lo + hi) / 2;
        if points[mid].spine_arc <= arc {
            lo = mid;
        } else {
            hi = mid;
        }
    }

    // Compute interpolation factor
    let a = &points[lo];
    let b = &points[lo + 1];
    let seg_start = a.spine_arc;
    let seg_end = b.spine_arc;
    let seg_len = seg_end - seg_start;
    let t = if seg_len > 0.0 {
        (arc - seg_start) / seg_len
    } else {
        0.0
    };

    // Linear interpolation for position
    let pos_a = a.spine_position(a.heart_offset);
    let pos_b = b.spine_position(b.heart_offset);
    let position = lerp_float3(pos_a, pos_b, t);

    // Lerp and normalize for frame vectors
    let direction = lerp_float3(a.direction, b.direction, t).normalize();
    let normal = lerp_float3(a.normal, b.normal, t).normalize();
    let lateral = lerp_float3(a.lateral, b.lateral, t).normalize();

    SplinePoint::new(arc, position, direction, normal, lateral)
}

/// Interpolates physics data (velocity, normal_force, lateral_force, roll_speed)
/// at the given arc position using binary search and linear interpolation.
pub fn interpolate_physics(points: &[Point], arc: f32) -> (f32, f32, f32, f32) {
    if points.is_empty() {
        return (0.0, 0.0, 0.0, 0.0);
    }

    if points.len() == 1 {
        let p = &points[0];
        return (p.velocity, p.normal_force, p.lateral_force, p.roll_speed);
    }

    // Handle boundary cases: clamp to endpoints
    if arc <= points[0].spine_arc {
        let p = &points[0];
        return (p.velocity, p.normal_force, p.lateral_force, p.roll_speed);
    }

    let last = points.len() - 1;
    if arc >= points[last].spine_arc {
        let p = &points[last];
        return (p.velocity, p.normal_force, p.lateral_force, p.roll_speed);
    }

    // Binary search to find segment
    let mut lo = 0usize;
    let mut hi = last;

    while lo < hi - 1 {
        let mid = (lo + hi) / 2;
        if points[mid].spine_arc <= arc {
            lo = mid;
        } else {
            hi = mid;
        }
    }

    // Compute interpolation factor
    let a = &points[lo];
    let b = &points[lo + 1];
    let seg_start = a.spine_arc;
    let seg_end = b.spine_arc;
    let seg_len = seg_end - seg_start;
    let t = if seg_len > 0.0 {
        (arc - seg_start) / seg_len
    } else {
        0.0
    };

    fn lerp(a: f32, b: f32, t: f32) -> f32 {
        a + (b - a) * t
    }

    (
        lerp(a.velocity, b.velocity, t),
        lerp(a.normal_force, b.normal_force, t),
        lerp(a.lateral_force, b.lateral_force, t),
        lerp(a.roll_speed, b.roll_speed, t),
    )
}

/// Resamples a path of Points into uniformly-spaced SplinePoints.
///
/// # Arguments
/// * `points` - Input simulation points (must be sorted by spine_arc)
/// * `resolution` - Target arc-length spacing between samples
///
/// # Returns
/// A vector of SplinePoints with approximately uniform arc spacing.
/// Returns empty if input is empty.
/// Returns single point if input has one point or zero/negative total length.
pub fn resample(points: &[Point], resolution: f32) -> Vec<SplinePoint> {
    if points.is_empty() {
        return Vec::new();
    }

    if points.len() == 1 {
        return vec![to_spline_point(&points[0])];
    }

    let start_arc = points[0].spine_arc;
    let end_arc = points[points.len() - 1].spine_arc;
    let total_length = end_arc - start_arc;

    // Handle zero or negative length (degenerate case)
    if total_length <= 0.0 {
        return vec![to_spline_point(&points[0])];
    }

    // Calculate number of samples: at least 2, ceil(length/resolution) + 1
    let num_samples = 2.max((total_length / resolution).ceil() as usize + 1);

    let mut result = Vec::with_capacity(num_samples);

    for i in 0..num_samples {
        let t = i as f32 / (num_samples - 1) as f32;
        let target_arc = start_arc + t * total_length;
        result.push(interpolate_at_arc(points, target_arc));
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    const TOLERANCE: f32 = 1e-5;

    /// Creates a test Point with specified position and spine_arc.
    fn make_test_point(pos: Float3, spine_arc: f32) -> Point {
        Point::new(
            pos,           // heart_position
            Float3::BACK,  // direction
            Float3::DOWN,  // normal
            Float3::RIGHT, // lateral
            10.0,          // velocity
            1.0,           // normal_force
            0.0,           // lateral_force
            0.0,           // heart_arc
            spine_arc,     // spine_arc
            0.0,           // heart_advance
            0.0,           // friction_origin
            0.0,           // roll_speed
            0.0,           // heart_offset (spine = heart when 0)
            0.0,           // friction
            0.0,           // resistance
        )
    }

    #[test]
    fn resample_empty_returns_empty() {
        let points: &[Point] = &[];
        let result = resample(points, 1.0);
        assert!(result.is_empty());
    }

    #[test]
    fn resample_single_point_returns_single() {
        let points = [make_test_point(Float3::new(0.0, 5.0, 0.0), 0.0)];
        let result = resample(&points, 1.0);

        assert_eq!(result.len(), 1);
        assert_relative_eq!(result[0].arc, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(result[0].position.y, 5.0, epsilon = TOLERANCE);
    }

    #[test]
    fn resample_two_points_interpolates_correctly() {
        let points = [
            make_test_point(Float3::new(0.0, 0.0, 0.0), 0.0),
            make_test_point(Float3::new(10.0, 0.0, 0.0), 10.0),
        ];
        let result = resample(&points, 5.0); // Should get 3 samples: 0, 5, 10

        assert_eq!(result.len(), 3);

        // First sample at arc=0
        assert_relative_eq!(result[0].arc, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(result[0].position.x, 0.0, epsilon = TOLERANCE);

        // Middle sample at arc=5
        assert_relative_eq!(result[1].arc, 5.0, epsilon = TOLERANCE);
        assert_relative_eq!(result[1].position.x, 5.0, epsilon = TOLERANCE);

        // Last sample at arc=10
        assert_relative_eq!(result[2].arc, 10.0, epsilon = TOLERANCE);
        assert_relative_eq!(result[2].position.x, 10.0, epsilon = TOLERANCE);
    }

    #[test]
    fn resample_uniform_spacing() {
        let points = [
            make_test_point(Float3::new(0.0, 0.0, 0.0), 0.0),
            make_test_point(Float3::new(100.0, 0.0, 0.0), 100.0),
        ];
        let resolution = 10.0;
        let result = resample(&points, resolution);

        // Should have 11 samples for 100m at 10m resolution
        assert_eq!(result.len(), 11);

        // Verify uniform spacing
        for i in 1..result.len() {
            let spacing = result[i].arc - result[i - 1].arc;
            assert_relative_eq!(spacing, 10.0, epsilon = TOLERANCE);
        }
    }

    #[test]
    fn interpolate_at_arc_boundaries() {
        let points = [
            make_test_point(Float3::new(0.0, 0.0, 0.0), 0.0),
            make_test_point(Float3::new(10.0, 0.0, 0.0), 10.0),
            make_test_point(Float3::new(20.0, 0.0, 0.0), 20.0),
        ];

        // Below minimum arc - clamps to first point position
        let below = interpolate_at_arc(&points, -5.0);
        assert_relative_eq!(below.arc, -5.0, epsilon = TOLERANCE);
        assert_relative_eq!(below.position.x, 0.0, epsilon = TOLERANCE);

        // At minimum arc
        let at_min = interpolate_at_arc(&points, 0.0);
        assert_relative_eq!(at_min.arc, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(at_min.position.x, 0.0, epsilon = TOLERANCE);

        // At maximum arc
        let at_max = interpolate_at_arc(&points, 20.0);
        assert_relative_eq!(at_max.arc, 20.0, epsilon = TOLERANCE);
        assert_relative_eq!(at_max.position.x, 20.0, epsilon = TOLERANCE);

        // Above maximum arc - clamps to last point position
        let above = interpolate_at_arc(&points, 25.0);
        assert_relative_eq!(above.arc, 25.0, epsilon = TOLERANCE);
        assert_relative_eq!(above.position.x, 20.0, epsilon = TOLERANCE);
    }

    #[test]
    fn to_spline_point_uses_spine_position() {
        let mut point = make_test_point(Float3::new(0.0, 5.0, 0.0), 10.0);
        point.heart_offset = 1.0; // Spine is 1m below heart (normal is DOWN)

        let sp = to_spline_point(&point);

        assert_relative_eq!(sp.arc, 10.0, epsilon = TOLERANCE);
        // Position should be heart + normal * offset = (0, 5, 0) + (0, -1, 0) * 1.0 = (0, 4, 0)
        assert_relative_eq!(sp.position.x, 0.0, epsilon = TOLERANCE);
        assert_relative_eq!(sp.position.y, 4.0, epsilon = TOLERANCE);
        assert_relative_eq!(sp.position.z, 0.0, epsilon = TOLERANCE);
    }

    #[test]
    fn resample_zero_length_returns_single() {
        // Two points at same arc position
        let points = [
            make_test_point(Float3::new(0.0, 0.0, 0.0), 5.0),
            make_test_point(Float3::new(1.0, 0.0, 0.0), 5.0),
        ];
        let result = resample(&points, 1.0);

        assert_eq!(result.len(), 1);
        assert_relative_eq!(result[0].arc, 5.0, epsilon = TOLERANCE);
    }

    #[test]
    fn interpolate_normalizes_frame_vectors() {
        // Points with different directions that will interpolate
        let mut p1 = make_test_point(Float3::new(0.0, 0.0, 0.0), 0.0);
        let mut p2 = make_test_point(Float3::new(10.0, 0.0, 0.0), 10.0);

        p1.direction = Float3::new(0.0, 0.0, -1.0);
        p2.direction = Float3::new(-1.0, 0.0, 0.0);

        let sp = interpolate_at_arc(&[p1, p2], 5.0);

        // Direction should be normalized
        let mag = sp.direction.magnitude();
        assert_relative_eq!(mag, 1.0, epsilon = TOLERANCE);
    }
}
