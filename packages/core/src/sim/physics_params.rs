#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PhysicsParams {
    pub heart_offset: f32,
    pub friction: f32,
    pub resistance: f32,
    pub delta_roll: f32,
    pub driven: bool,
}

impl PhysicsParams {
    pub fn new(
        heart_offset: f32,
        friction: f32,
        resistance: f32,
        delta_roll: f32,
        driven: bool,
    ) -> Self {
        Self {
            heart_offset,
            friction,
            resistance,
            delta_roll,
            driven,
        }
    }
}

impl Default for PhysicsParams {
    fn default() -> Self {
        Self {
            heart_offset: 1.1,
            friction: 0.0,
            resistance: 0.0,
            delta_roll: 0.0,
            driven: false,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_new() {
        let params = PhysicsParams::new(1.5, 0.01, 0.02, 0.1, true);
        assert_eq!(params.heart_offset, 1.5);
        assert_eq!(params.friction, 0.01);
        assert_eq!(params.resistance, 0.02);
        assert_eq!(params.delta_roll, 0.1);
        assert!(params.driven);
    }

    #[test]
    fn test_default_values() {
        let params = PhysicsParams::default();
        assert_eq!(params.heart_offset, 1.1);
        assert_eq!(params.friction, 0.0);
        assert_eq!(params.resistance, 0.0);
        assert_eq!(params.delta_roll, 0.0);
        assert!(!params.driven);
    }

    #[test]
    fn test_clone() {
        let params1 = PhysicsParams::new(1.5, 0.01, 0.02, 0.1, true);
        let params2 = params1;
        assert_eq!(params1, params2);
    }

    #[test]
    fn test_debug() {
        let params = PhysicsParams::new(1.1, 0.0, 0.0, 0.0, false);
        let debug_str = format!("{:?}", params);
        assert!(debug_str.contains("PhysicsParams"));
        assert!(debug_str.contains("heart_offset"));
    }
}
