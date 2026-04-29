//! Directed Acyclic Graph (DAG) structure and traversal.
//!
//! Structure-of-Arrays (SoA) layout for efficient node-based evaluation.
//! Auxiliary index maps are precomputed in [`Graph::from_vecs`] so per-node
//! and per-port queries are O(1) / O(k) instead of linear scans.

mod port_spec;
mod traversal;

pub use port_spec::{PortDataType, PortSpec};

use crate::nodes::NodeType;
use std::collections::HashMap;

#[derive(Debug, Clone)]
pub struct Graph {
    // Node SoA
    pub node_ids: Vec<u32>,
    pub node_types: Vec<u8>,
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
    edge_index: HashMap<u32, usize>,

    // Per-node port lists (insertion order). Indexed by node_index.
    node_input_ports: Vec<Vec<u32>>,
    node_output_ports: Vec<Vec<u32>>,

    // Per-node edge lists. Indexed by node_index.
    node_incoming_edges: Vec<Vec<usize>>,
    node_outgoing_edges: Vec<Vec<usize>>,

    // Per-port edge lookup. Multiple edges can share a port.
    incoming_edges_by_port: HashMap<u32, Vec<usize>>,
    outgoing_edges_by_port: HashMap<u32, Vec<usize>>,
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
            edge_index: HashMap::new(),
            node_input_ports: Vec::new(),
            node_output_ports: Vec::new(),
            node_incoming_edges: Vec::new(),
            node_outgoing_edges: Vec::new(),
            incoming_edges_by_port: HashMap::new(),
            outgoing_edges_by_port: HashMap::new(),
        }
    }

    /// Construct from owned vectors. Builds all auxiliary index maps once.
    #[allow(clippy::too_many_arguments)]
    pub fn from_vecs(
        node_ids: Vec<u32>,
        node_types: Vec<u8>,
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
        let node_count = node_ids.len();

        let mut node_index = HashMap::with_capacity(node_count);
        for (i, &id) in node_ids.iter().enumerate() {
            node_index.insert(id, i);
        }

        let mut port_index = HashMap::with_capacity(port_ids.len());
        for (i, &id) in port_ids.iter().enumerate() {
            port_index.insert(id, i);
        }

        let mut edge_index = HashMap::with_capacity(edge_ids.len());
        for (i, &id) in edge_ids.iter().enumerate() {
            edge_index.insert(id, i);
        }

        let mut node_input_ports: Vec<Vec<u32>> = vec![Vec::new(); node_count];
        let mut node_output_ports: Vec<Vec<u32>> = vec![Vec::new(); node_count];
        for (i, &port_id) in port_ids.iter().enumerate() {
            if let Some(&owner_idx) = node_index.get(&port_owners[i]) {
                if port_is_input[i] {
                    node_input_ports[owner_idx].push(port_id);
                } else {
                    node_output_ports[owner_idx].push(port_id);
                }
            }
        }

        let mut node_incoming_edges: Vec<Vec<usize>> = vec![Vec::new(); node_count];
        let mut node_outgoing_edges: Vec<Vec<usize>> = vec![Vec::new(); node_count];
        let mut incoming_edges_by_port: HashMap<u32, Vec<usize>> = HashMap::new();
        let mut outgoing_edges_by_port: HashMap<u32, Vec<usize>> = HashMap::new();
        for i in 0..edge_ids.len() {
            let src = edge_sources[i];
            let tgt = edge_targets[i];
            outgoing_edges_by_port.entry(src).or_default().push(i);
            incoming_edges_by_port.entry(tgt).or_default().push(i);
            if let Some(&pi) = port_index.get(&src) {
                if let Some(&ni) = node_index.get(&port_owners[pi]) {
                    node_outgoing_edges[ni].push(i);
                }
            }
            if let Some(&pi) = port_index.get(&tgt) {
                if let Some(&ni) = node_index.get(&port_owners[pi]) {
                    node_incoming_edges[ni].push(i);
                }
            }
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
            edge_index,
            node_input_ports,
            node_output_ports,
            node_incoming_edges,
            node_outgoing_edges,
            incoming_edges_by_port,
            outgoing_edges_by_port,
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

    pub fn get_node_type(&self, node_id: u32) -> Option<NodeType> {
        let i = self.get_node_index(node_id)?;
        NodeType::from_u8(self.node_types[i])
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
        match self.get_node_index(node_id) {
            Some(i) => self.node_input_ports[i].clone(),
            None => Vec::new(),
        }
    }

    pub fn get_output_ports(&self, node_id: u32) -> Vec<u32> {
        match self.get_node_index(node_id) {
            Some(i) => self.node_output_ports[i].clone(),
            None => Vec::new(),
        }
    }

    /// Get nth input port for a node (by ordinal index).
    pub fn try_get_input(&self, node_id: u32, index: usize) -> Option<u32> {
        let i = self.get_node_index(node_id)?;
        self.node_input_ports[i].get(index).copied()
    }

    /// Get nth output port for a node (by ordinal index).
    pub fn try_get_output(&self, node_id: u32, index: usize) -> Option<u32> {
        let i = self.get_node_index(node_id)?;
        self.node_output_ports[i].get(index).copied()
    }

    /// Get input port by type specification. Finds the nth port matching the data type.
    pub fn try_get_input_by_spec(
        &self,
        node_id: u32,
        data_type: PortDataType,
        local_index: usize,
    ) -> Option<u32> {
        let ni = self.get_node_index(node_id)?;
        let mut match_count = 0;
        for &port_id in &self.node_input_ports[ni] {
            let pi = self.port_index.get(&port_id).copied()?;
            let spec = PortSpec::from_encoded(self.port_types[pi]);
            if spec.data_type == data_type {
                if match_count == local_index {
                    return Some(port_id);
                }
                match_count += 1;
            }
        }
        None
    }

    /// Get output port by type specification. Finds the nth port matching the data type.
    pub fn try_get_output_by_spec(
        &self,
        node_id: u32,
        data_type: PortDataType,
        local_index: usize,
    ) -> Option<u32> {
        let ni = self.get_node_index(node_id)?;
        let mut match_count = 0;
        for &port_id in &self.node_output_ports[ni] {
            let pi = self.port_index.get(&port_id).copied()?;
            let spec = PortSpec::from_encoded(self.port_types[pi]);
            if spec.data_type == data_type {
                if match_count == local_index {
                    return Some(port_id);
                }
                match_count += 1;
            }
        }
        None
    }

    // --- Edge operations ---

    pub fn edge_source(&self, edge_id: u32) -> Option<u32> {
        self.edge_index.get(&edge_id).map(|&i| self.edge_sources[i])
    }

    pub fn edge_target(&self, edge_id: u32) -> Option<u32> {
        self.edge_index.get(&edge_id).map(|&i| self.edge_targets[i])
    }

    /// Get all edges where source is from this node's output ports.
    pub fn get_outgoing_edges(&self, node_id: u32) -> Vec<u32> {
        match self.get_node_index(node_id) {
            Some(i) => self.node_outgoing_edges[i]
                .iter()
                .map(|&ei| self.edge_ids[ei])
                .collect(),
            None => Vec::new(),
        }
    }

    /// Get all edges where target is this node's input ports.
    pub fn get_incoming_edges(&self, node_id: u32) -> Vec<u32> {
        match self.get_node_index(node_id) {
            Some(i) => self.node_incoming_edges[i]
                .iter()
                .map(|&ei| self.edge_ids[ei])
                .collect(),
            None => Vec::new(),
        }
    }

    // --- Internal accessors used by traversal + dispatch ---

    pub(crate) fn node_incoming_edge_indices(&self, node_idx: usize) -> &[usize] {
        &self.node_incoming_edges[node_idx]
    }

    pub(crate) fn node_outgoing_edge_indices(&self, node_idx: usize) -> &[usize] {
        &self.node_outgoing_edges[node_idx]
    }

    /// Edge indices targeting `port_id` (input port). Empty if unconnected.
    pub(crate) fn incoming_edge_indices_to_port(&self, port_id: u32) -> &[usize] {
        self.incoming_edges_by_port
            .get(&port_id)
            .map(Vec::as_slice)
            .unwrap_or(&[])
    }

    /// Edge indices sourced from `port_id` (output port). Empty if unconnected.
    pub(crate) fn outgoing_edge_indices_from_port(&self, port_id: u32) -> &[usize] {
        self.outgoing_edges_by_port
            .get(&port_id)
            .map(Vec::as_slice)
            .unwrap_or(&[])
    }
}

impl Default for Graph {
    fn default() -> Self {
        Self::new()
    }
}

/// Test-only builder for assembling graphs without per-port boilerplate.
///
/// Each `add_node` call appends a node, allocates input/output ports in order
/// of the supplied `PortDataType` slices, and returns a [`NodeRef`] handle.
/// `connect(source.output(i), target.input(j))` wires an edge between them.
/// Call [`GraphBuilder::build`] to produce the [`Graph`].
#[cfg(test)]
pub struct GraphBuilder {
    next_node_id: u32,
    next_port_id: u32,
    next_edge_id: u32,
    node_ids: Vec<u32>,
    node_types: Vec<u8>,
    node_input_count: Vec<i32>,
    node_output_count: Vec<i32>,
    port_ids: Vec<u32>,
    port_types: Vec<u32>,
    port_owners: Vec<u32>,
    port_is_input: Vec<bool>,
    edge_ids: Vec<u32>,
    edge_sources: Vec<u32>,
    edge_targets: Vec<u32>,
}

#[cfg(test)]
#[derive(Debug, Clone)]
pub struct NodeRef {
    pub id: u32,
    pub inputs: Vec<u32>,
    pub outputs: Vec<u32>,
}

#[cfg(test)]
impl NodeRef {
    pub fn input(&self, i: usize) -> u32 {
        self.inputs[i]
    }
    pub fn output(&self, i: usize) -> u32 {
        self.outputs[i]
    }
}

#[cfg(test)]
impl GraphBuilder {
    pub fn new() -> Self {
        Self {
            next_node_id: 1,
            next_port_id: 100,
            next_edge_id: 1,
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
        }
    }

    pub fn add_node(
        &mut self,
        node_type: crate::nodes::NodeType,
        inputs: &[PortDataType],
        outputs: &[PortDataType],
    ) -> NodeRef {
        let node_id = self.next_node_id;
        self.next_node_id += 1;
        self.node_ids.push(node_id);
        self.node_types.push(node_type as u8);
        self.node_input_count.push(inputs.len() as i32);
        self.node_output_count.push(outputs.len() as i32);

        let mut input_ids = Vec::with_capacity(inputs.len());
        for (i, &dt) in inputs.iter().enumerate() {
            let pid = self.next_port_id;
            self.next_port_id += 1;
            self.port_ids.push(pid);
            self.port_types.push(PortSpec::new(dt, i as u8).to_encoded());
            self.port_owners.push(node_id);
            self.port_is_input.push(true);
            input_ids.push(pid);
        }

        let mut output_ids = Vec::with_capacity(outputs.len());
        for (i, &dt) in outputs.iter().enumerate() {
            let pid = self.next_port_id;
            self.next_port_id += 1;
            self.port_ids.push(pid);
            self.port_types.push(PortSpec::new(dt, i as u8).to_encoded());
            self.port_owners.push(node_id);
            self.port_is_input.push(false);
            output_ids.push(pid);
        }

        NodeRef {
            id: node_id,
            inputs: input_ids,
            outputs: output_ids,
        }
    }

    pub fn connect(&mut self, source_port: u32, target_port: u32) -> u32 {
        let edge_id = self.next_edge_id;
        self.next_edge_id += 1;
        self.edge_ids.push(edge_id);
        self.edge_sources.push(source_port);
        self.edge_targets.push(target_port);
        edge_id
    }

    pub fn build(self) -> Graph {
        Graph::from_vecs(
            self.node_ids,
            self.node_types,
            self.node_input_count,
            self.node_output_count,
            self.port_ids,
            self.port_types,
            self.port_owners,
            self.port_is_input,
            self.edge_ids,
            self.edge_sources,
            self.edge_targets,
        )
    }
}

#[cfg(test)]
impl Default for GraphBuilder {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_simple_graph() -> Graph {
        // Anchor (id=1) -> Force (id=2). Anchor outputs port 101; Force has input
        // port 201 and output port 202. Edge 301 connects 101 -> 201.
        Graph::from_vecs(
            vec![1, 2],
            vec![NodeType::Anchor as u8, NodeType::Force as u8],
            vec![0, 1],
            vec![1, 2],
            vec![101, 201, 202],
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Path, 0).to_encoded(),
            ],
            vec![1, 2, 2],
            vec![false, true, false],
            vec![301],
            vec![101],
            vec![201],
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
        assert_eq!(graph.get_node_type(1), Some(NodeType::Anchor));
        assert_eq!(graph.get_node_type(2), Some(NodeType::Force));
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

    #[test]
    fn edge_source_and_target_lookup() {
        let graph = make_simple_graph();
        assert_eq!(graph.edge_source(301), Some(101));
        assert_eq!(graph.edge_target(301), Some(201));
        assert_eq!(graph.edge_source(999), None);
    }
}
