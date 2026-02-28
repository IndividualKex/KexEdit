use crate::sim::Point;
use std::collections::HashMap;

/// Result of graph evaluation containing all computed anchors and paths.
pub struct EvaluationResult {
    /// Output anchor for each node that produces one (nodeId -> Point)
    pub anchors: HashMap<u32, Point>,
    /// Output path for each node that produces one (nodeId -> Vec<Point>)
    pub paths: HashMap<u32, Vec<Point>>,
}

impl EvaluationResult {
    pub fn new(capacity: usize) -> Self {
        Self {
            anchors: HashMap::with_capacity(capacity),
            paths: HashMap::with_capacity(capacity),
        }
    }
}

impl Default for EvaluationResult {
    fn default() -> Self {
        Self::new(0)
    }
}
