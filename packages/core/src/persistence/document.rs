//! Owned document data for persistence.

use crate::graph::Graph;
use crate::sim::{Float3, Keyframe};
use crate::track::DocumentView;
use std::collections::HashMap;

/// Owned document data for serialization/deserialization.
/// Unlike `DocumentView`, this owns all data.
#[derive(Debug, Clone)]
pub struct Document {
    pub graph: Graph,
    pub scalars: HashMap<u64, f32>,
    pub vectors: HashMap<u64, Float3>,
    pub flags: HashMap<u64, i32>,
    pub keyframes: Vec<Keyframe>,
    pub keyframe_ranges: HashMap<u64, (usize, usize)>,
    pub next_node_id: u32,
    pub next_port_id: u32,
    pub next_edge_id: u32,
}

impl Document {
    pub fn new() -> Self {
        Self {
            graph: Graph::new(),
            scalars: HashMap::new(),
            vectors: HashMap::new(),
            flags: HashMap::new(),
            keyframes: Vec::new(),
            keyframe_ranges: HashMap::new(),
            next_node_id: 1,
            next_port_id: 1,
            next_edge_id: 1,
        }
    }

    /// Create a read-only view for graph evaluation.
    pub fn as_view(&self) -> DocumentView {
        DocumentView::new(
            &self.graph,
            &self.scalars,
            &self.vectors,
            &self.flags,
            &self.keyframes,
            &self.keyframe_ranges,
        )
    }
}

impl Default for Document {
    fn default() -> Self {
        Self::new()
    }
}
