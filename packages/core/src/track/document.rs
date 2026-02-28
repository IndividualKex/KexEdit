use crate::graph::Graph;
use crate::sim::{Float3, Keyframe};
use std::collections::HashMap;

/// Key encoding for scalar/vector/flag lookups.
/// Matches C# Document.InputKey: (nodeId << 8) | inputIndex
pub fn input_key(node_id: u32, input_index: i32) -> u64 {
    ((node_id as u64) << 8) | ((input_index as u8) as u64)
}

/// Key encoding for keyframe lookups.
/// Matches C# KeyframeStore.MakeKey: (nodeId << 8) | propertyId
pub fn keyframe_key(node_id: u32, property_id: u8) -> u64 {
    ((node_id as u64) << 8) | (property_id as u64)
}

/// Read-only view into document data for graph evaluation.
pub struct DocumentView<'a> {
    pub graph: &'a Graph,
    pub scalars: &'a HashMap<u64, f32>,
    pub vectors: &'a HashMap<u64, Float3>,
    pub flags: &'a HashMap<u64, i32>,
    /// Flat keyframe storage
    pub keyframes: &'a [Keyframe],
    /// Maps (node_id << 8 | property_id) -> (start_index, length) in keyframes array
    pub keyframe_ranges: &'a HashMap<u64, (usize, usize)>,
}

impl<'a> DocumentView<'a> {
    pub fn new(
        graph: &'a Graph,
        scalars: &'a HashMap<u64, f32>,
        vectors: &'a HashMap<u64, Float3>,
        flags: &'a HashMap<u64, i32>,
        keyframes: &'a [Keyframe],
        keyframe_ranges: &'a HashMap<u64, (usize, usize)>,
    ) -> Self {
        Self {
            graph,
            scalars,
            vectors,
            flags,
            keyframes,
            keyframe_ranges,
        }
    }

    pub fn get_scalar(&self, node_id: u32, input_index: i32, default: f32) -> f32 {
        let key = input_key(node_id, input_index);
        self.scalars.get(&key).copied().unwrap_or(default)
    }

    pub fn get_vector(&self, node_id: u32, input_index: i32, default: Float3) -> Float3 {
        let key = input_key(node_id, input_index);
        self.vectors.get(&key).copied().unwrap_or(default)
    }

    pub fn get_flag(&self, node_id: u32, property_index: i32) -> i32 {
        let key = input_key(node_id, property_index);
        self.flags.get(&key).copied().unwrap_or(0)
    }

    pub fn get_keyframes(&self, node_id: u32, property_id: u8) -> &[Keyframe] {
        let key = keyframe_key(node_id, property_id);
        match self.keyframe_ranges.get(&key) {
            Some(&(start, len)) => {
                let end = start + len;
                if end <= self.keyframes.len() {
                    &self.keyframes[start..end]
                } else {
                    &[]
                }
            }
            None => &[],
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn input_key_encoding() {
        assert_eq!(input_key(1, 0), (1 << 8) | 0);
        assert_eq!(input_key(100, 5), (100 << 8) | 5);
        assert_eq!(input_key(0xFFFF, 255), (0xFFFF << 8) | 255);
    }

    #[test]
    fn keyframe_key_encoding() {
        assert_eq!(keyframe_key(1, 0), (1 << 8) | 0);
        assert_eq!(keyframe_key(100, 5), (100 << 8) | 5);
    }

    #[test]
    fn get_scalar_returns_default_when_missing() {
        let graph = Graph::new();
        let scalars = HashMap::new();
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframes = vec![];
        let keyframe_ranges = HashMap::new();

        let doc = DocumentView::new(
            &graph,
            &scalars,
            &vectors,
            &flags,
            &keyframes,
            &keyframe_ranges,
        );

        assert_eq!(doc.get_scalar(1, 0, 42.0), 42.0);
    }

    #[test]
    fn get_scalar_returns_stored_value() {
        let graph = Graph::new();
        let mut scalars = HashMap::new();
        scalars.insert(input_key(1, 5), 123.0);
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframes = vec![];
        let keyframe_ranges = HashMap::new();

        let doc = DocumentView::new(
            &graph,
            &scalars,
            &vectors,
            &flags,
            &keyframes,
            &keyframe_ranges,
        );

        assert_eq!(doc.get_scalar(1, 5, 0.0), 123.0);
    }

    #[test]
    fn get_keyframes_returns_empty_when_missing() {
        let graph = Graph::new();
        let scalars = HashMap::new();
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframes = vec![];
        let keyframe_ranges = HashMap::new();

        let doc = DocumentView::new(
            &graph,
            &scalars,
            &vectors,
            &flags,
            &keyframes,
            &keyframe_ranges,
        );

        assert!(doc.get_keyframes(1, 0).is_empty());
    }

    #[test]
    fn get_keyframes_returns_slice() {
        let graph = Graph::new();
        let scalars = HashMap::new();
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframes = vec![
            Keyframe::simple(0.0, 0.0),
            Keyframe::simple(1.0, 1.0),
            Keyframe::simple(2.0, 2.0),
        ];
        let mut keyframe_ranges = HashMap::new();
        keyframe_ranges.insert(keyframe_key(1, 0), (0, 2)); // First 2 keyframes

        let doc = DocumentView::new(
            &graph,
            &scalars,
            &vectors,
            &flags,
            &keyframes,
            &keyframe_ranges,
        );

        assert_eq!(doc.get_keyframes(1, 0).len(), 2);
    }
}
