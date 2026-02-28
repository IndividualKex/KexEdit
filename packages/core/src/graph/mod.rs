//! Directed Acyclic Graph (DAG) structure and traversal.
//!
//! This module provides a graph data structure optimized for node-based evaluation
//! with Structure-of-Arrays (SoA) layout for efficient traversal.

mod port_spec;
mod traversal;

pub use port_spec::{PortDataType, PortSpec};

use std::collections::HashMap;

#[derive(Debug, Clone)]
pub struct Graph {
    // Node SoA
    pub node_ids: Vec<u32>,
    pub node_types: Vec<u32>,
    pub node_input_count: Vec<i32>,
    pub node_output_count: Vec<i32>,

    // Port SoA
    pub port_ids: Vec<u32>,
    pub port_types: Vec<u32>,
    pub port_owners: Vec<u32>,
    pub port_is_input: Vec<bool>,

    // Edge SoA
    pub edge_ids: Vec<u32>,
    pub edge_sources: Vec<u32>,
    pub edge_targets: Vec<u32>,

    // Index maps
    node_index: HashMap<u32, usize>,
    port_index: HashMap<u32, usize>,
}

impl Graph {
    pub fn new() -> Self {
        Self {
            node_ids: Vec::new(),
            node_types: Vec::new(),
            node_input_count: Vec::new(),
            node_output_count: Vec::new(),
            port_ids: Vec::new(),
            port_types: Vec::new(),
            port_owners: Vec::new(),
            port_is_input: Vec::new(),
            edge_ids: Vec::new(),
            edge_sources: Vec::new(),
            edge_targets: Vec::new(),
            node_index: HashMap::new(),
            port_index: HashMap::new(),
        }
    }

    /// Construct from raw pointers (FFI entry point).
    ///
    /// # Safety
    /// All pointers must be valid and point to arrays of the specified lengths.
    #[allow(clippy::too_many_arguments)]
    pub unsafe fn from_arrays(
        node_ids: *const u32,
        node_types: *const u32,
        node_input_count: *const i32,
        node_output_count: *const i32,
        node_count: usize,
        port_ids: *const u32,
        port_types: *const u32,
        port_owners: *const u32,
        port_is_input: *const bool,
        port_count: usize,
        edge_ids: *const u32,
        edge_sources: *const u32,
        edge_targets: *const u32,
        edge_count: usize,
    ) -> Self {
        let node_ids = std::slice::from_raw_parts(node_ids, node_count).to_vec();
        let node_types = std::slice::from_raw_parts(node_types, node_count).to_vec();
        let node_input_count = std::slice::from_raw_parts(node_input_count, node_count).to_vec();
        let node_output_count = std::slice::from_raw_parts(node_output_count, node_count).to_vec();

        let port_ids = std::slice::from_raw_parts(port_ids, port_count).to_vec();
        let port_types = std::slice::from_raw_parts(port_types, port_count).to_vec();
        let port_owners = std::slice::from_raw_parts(port_owners, port_count).to_vec();
        let port_is_input = std::slice::from_raw_parts(port_is_input, port_count).to_vec();

        let edge_ids = std::slice::from_raw_parts(edge_ids, edge_count).to_vec();
        let edge_sources = std::slice::from_raw_parts(edge_sources, edge_count).to_vec();
        let edge_targets = std::slice::from_raw_parts(edge_targets, edge_count).to_vec();

        let mut node_index = HashMap::with_capacity(node_count);
        for (i, &id) in node_ids.iter().enumerate() {
            node_index.insert(id, i);
        }

        let mut port_index = HashMap::with_capacity(port_count);
        for (i, &id) in port_ids.iter().enumerate() {
            port_index.insert(id, i);
        }

        Self {
            node_ids,
            node_types,
            node_input_count,
            node_output_count,
            port_ids,
            port_types,
            port_owners,
            port_is_input,
            edge_ids,
            edge_sources,
            edge_targets,
            node_index,
            port_index,
        }
    }

    /// Construct from owned vectors.
    #[allow(clippy::too_many_arguments)]
    pub fn from_vecs(
        node_ids: Vec<u32>,
        node_types: Vec<u32>,
        node_input_count: Vec<i32>,
        node_output_count: Vec<i32>,
        port_ids: Vec<u32>,
        port_types: Vec<u32>,
        port_owners: Vec<u32>,
        port_is_input: Vec<bool>,
        edge_ids: Vec<u32>,
        edge_sources: Vec<u32>,
        edge_targets: Vec<u32>,
    ) -> Self {
        let mut node_index = HashMap::with_capacity(node_ids.len());
        for (i, &id) in node_ids.iter().enumerate() {
            node_index.insert(id, i);
        }

        let mut port_index = HashMap::with_capacity(port_ids.len());
        for (i, &id) in port_ids.iter().enumerate() {
            port_index.insert(id, i);
        }

        Self {
            node_ids,
            node_types,
            node_input_count,
            node_output_count,
            port_ids,
            port_types,
            port_owners,
            port_is_input,
            edge_ids,
            edge_sources,
            edge_targets,
            node_index,
            port_index,
        }
    }

    pub fn node_count(&self) -> usize {
        self.node_ids.len()
    }

    pub fn port_count(&self) -> usize {
        self.port_ids.len()
    }

    pub fn edge_count(&self) -> usize {
        self.edge_ids.len()
    }

    // --- Node lookup ---

    pub fn get_node_index(&self, node_id: u32) -> Option<usize> {
        self.node_index.get(&node_id).copied()
    }

    pub fn get_node_type(&self, node_id: u32) -> Option<u32> {
        self.get_node_index(node_id).map(|i| self.node_types[i])
    }

    // --- Port lookup ---

    pub fn get_port_index(&self, port_id: u32) -> Option<usize> {
        self.port_index.get(&port_id).copied()
    }

    pub fn get_port_spec(&self, port_id: u32) -> Option<PortSpec> {
        self.get_port_index(port_id)
            .map(|i| PortSpec::from_encoded(self.port_types[i]))
    }

    pub fn get_port_owner(&self, port_id: u32) -> Option<u32> {
        self.get_port_index(port_id).map(|i| self.port_owners[i])
    }

    pub fn get_input_ports(&self, node_id: u32) -> Vec<u32> {
        let mut result = Vec::new();
        for i in 0..self.port_ids.len() {
            if self.port_owners[i] == node_id && self.port_is_input[i] {
                result.push(self.port_ids[i]);
            }
        }
        result
    }

    pub fn get_output_ports(&self, node_id: u32) -> Vec<u32> {
        let mut result = Vec::new();
        for i in 0..self.port_ids.len() {
            if self.port_owners[i] == node_id && !self.port_is_input[i] {
                result.push(self.port_ids[i]);
            }
        }
        result
    }

    /// Get nth input port for a node (by ordinal index).
    pub fn try_get_input(&self, node_id: u32, index: usize) -> Option<u32> {
        self.get_input_ports(node_id).get(index).copied()
    }

    /// Get nth output port for a node (by ordinal index).
    pub fn try_get_output(&self, node_id: u32, index: usize) -> Option<u32> {
        self.get_output_ports(node_id).get(index).copied()
    }

    /// Get input port by type specification.
    /// Finds the nth port matching the data type.
    pub fn try_get_input_by_spec(
        &self,
        node_id: u32,
        data_type: PortDataType,
        local_index: usize,
    ) -> Option<u32> {
        let input_ports = self.get_input_ports(node_id);
        let mut match_count = 0;

        for port_id in input_ports {
            if let Some(idx) = self.get_port_index(port_id) {
                let spec = PortSpec::from_encoded(self.port_types[idx]);
                if spec.data_type == data_type {
                    if match_count == local_index {
                        return Some(port_id);
                    }
                    match_count += 1;
                }
            }
        }
        None
    }

    /// Get output port by type specification.
    /// Finds the nth port matching the data type.
    pub fn try_get_output_by_spec(
        &self,
        node_id: u32,
        data_type: PortDataType,
        local_index: usize,
    ) -> Option<u32> {
        let output_ports = self.get_output_ports(node_id);
        let mut match_count = 0;

        for port_id in output_ports {
            if let Some(idx) = self.get_port_index(port_id) {
                let spec = PortSpec::from_encoded(self.port_types[idx]);
                if spec.data_type == data_type {
                    if match_count == local_index {
                        return Some(port_id);
                    }
                    match_count += 1;
                }
            }
        }
        None
    }

    // --- Edge operations ---

    pub fn edge_source(&self, edge_id: u32) -> Option<u32> {
        self.edge_ids
            .iter()
            .position(|&id| id == edge_id)
            .map(|i| self.edge_sources[i])
    }

    pub fn edge_target(&self, edge_id: u32) -> Option<u32> {
        self.edge_ids
            .iter()
            .position(|&id| id == edge_id)
            .map(|i| self.edge_targets[i])
    }

    /// Get all edges where source is from this node's output ports.
    pub fn get_outgoing_edges(&self, node_id: u32) -> Vec<u32> {
        let output_ports = self.get_output_ports(node_id);
        let mut result = Vec::new();

        for i in 0..self.edge_ids.len() {
            if output_ports.contains(&self.edge_sources[i]) {
                result.push(self.edge_ids[i]);
            }
        }
        result
    }

    /// Get all edges where target is this node's input ports.
    pub fn get_incoming_edges(&self, node_id: u32) -> Vec<u32> {
        let input_ports = self.get_input_ports(node_id);
        let mut result = Vec::new();

        for i in 0..self.edge_ids.len() {
            if input_ports.contains(&self.edge_targets[i]) {
                result.push(self.edge_ids[i]);
            }
        }
        result
    }
}

impl Default for Graph {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_simple_graph() -> Graph {
        // Simple graph: Node1 -> Node2
        // Node1 (id=1, type=7/Anchor) has output port 101
        // Node2 (id=2, type=2/Force) has input port 201, output port 202
        // Edge 301 connects port 101 -> port 201
        Graph::from_vecs(
            vec![1, 2],          // node_ids
            vec![7, 2],          // node_types (Anchor, Force)
            vec![0, 1],          // node_input_count
            vec![1, 2],          // node_output_count
            vec![101, 201, 202], // port_ids
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(), // 101: Anchor output
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(), // 201: Anchor input
                PortSpec::new(PortDataType::Path, 0).to_encoded(),   // 202: Path output
            ],
            vec![1, 2, 2],            // port_owners
            vec![false, true, false], // port_is_input
            vec![301],                // edge_ids
            vec![101],                // edge_sources
            vec![201],                // edge_targets
        )
    }

    #[test]
    fn empty_graph_has_zero_counts() {
        let graph = Graph::new();
        assert_eq!(graph.node_count(), 0);
        assert_eq!(graph.port_count(), 0);
        assert_eq!(graph.edge_count(), 0);
    }

    #[test]
    fn get_node_index_returns_correct_index() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_node_index(1), Some(0));
        assert_eq!(graph.get_node_index(2), Some(1));
        assert_eq!(graph.get_node_index(999), None);
    }

    #[test]
    fn get_node_type_returns_correct_type() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_node_type(1), Some(7)); // Anchor
        assert_eq!(graph.get_node_type(2), Some(2)); // Force
    }

    #[test]
    fn try_get_input_returns_correct_port() {
        let graph = make_simple_graph();
        assert_eq!(graph.try_get_input(2, 0), Some(201));
        assert_eq!(graph.try_get_input(2, 1), None); // out of bounds
        assert_eq!(graph.try_get_input(1, 0), None); // node 1 has no inputs
    }

    #[test]
    fn try_get_output_returns_correct_port() {
        let graph = make_simple_graph();
        assert_eq!(graph.try_get_output(1, 0), Some(101));
        assert_eq!(graph.try_get_output(2, 0), Some(202));
    }

    #[test]
    fn try_get_output_by_spec_finds_anchor_output() {
        let graph = make_simple_graph();
        assert_eq!(
            graph.try_get_output_by_spec(1, PortDataType::Anchor, 0),
            Some(101)
        );
    }

    #[test]
    fn try_get_output_by_spec_finds_path_output() {
        let graph = make_simple_graph();
        assert_eq!(
            graph.try_get_output_by_spec(2, PortDataType::Path, 0),
            Some(202)
        );
    }

    #[test]
    fn get_input_ports_returns_all_inputs() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_input_ports(1), Vec::<u32>::new());
        assert_eq!(graph.get_input_ports(2), vec![201]);
    }

    #[test]
    fn get_output_ports_returns_all_outputs() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_output_ports(1), vec![101]);
        assert_eq!(graph.get_output_ports(2), vec![202]);
    }

    #[test]
    fn get_outgoing_edges_returns_correct_edges() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_outgoing_edges(1), vec![301]);
        assert_eq!(graph.get_outgoing_edges(2), Vec::<u32>::new());
    }

    #[test]
    fn get_incoming_edges_returns_correct_edges() {
        let graph = make_simple_graph();
        assert_eq!(graph.get_incoming_edges(1), Vec::<u32>::new());
        assert_eq!(graph.get_incoming_edges(2), vec![301]);
    }
}
