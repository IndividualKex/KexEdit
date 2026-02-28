use super::Graph;
use std::collections::{HashSet, VecDeque};

impl Graph {
    /// Find all nodes with no incoming edges (source/root nodes).
    pub fn find_source_nodes(&self) -> Vec<u32> {
        let mut result = Vec::new();
        for &node_id in &self.node_ids {
            if self.get_incoming_edges(node_id).is_empty() {
                result.push(node_id);
            }
        }
        result
    }

    /// Find all nodes with no outgoing edges (sink/leaf nodes).
    pub fn find_sink_nodes(&self) -> Vec<u32> {
        let mut result = Vec::new();
        for &node_id in &self.node_ids {
            if self.get_outgoing_edges(node_id).is_empty() {
                result.push(node_id);
            }
        }
        result
    }

    /// Get all nodes directly connected downstream from this node.
    /// Returns unique node IDs (deduplicates multiple edges to same node).
    pub fn get_successor_nodes(&self, node_id: u32) -> Vec<u32> {
        let edges = self.get_outgoing_edges(node_id);
        if edges.is_empty() {
            return Vec::new();
        }

        let mut result = Vec::new();
        let mut seen = HashSet::new();

        for edge_id in edges {
            if let Some(edge_idx) = self.edge_ids.iter().position(|&id| id == edge_id) {
                let target_port_id = self.edge_targets[edge_idx];
                if let Some(port_idx) = self.get_port_index(target_port_id) {
                    let target_node_id = self.port_owners[port_idx];
                    if seen.insert(target_node_id) {
                        result.push(target_node_id);
                    }
                }
            }
        }
        result
    }

    /// Get all nodes directly connected upstream to this node.
    /// Returns unique node IDs (deduplicates multiple edges from same node).
    pub fn get_predecessor_nodes(&self, node_id: u32) -> Vec<u32> {
        let edges = self.get_incoming_edges(node_id);
        if edges.is_empty() {
            return Vec::new();
        }

        let mut result = Vec::new();
        let mut seen = HashSet::new();

        for edge_id in edges {
            if let Some(edge_idx) = self.edge_ids.iter().position(|&id| id == edge_id) {
                let source_port_id = self.edge_sources[edge_idx];
                if let Some(port_idx) = self.get_port_index(source_port_id) {
                    let source_node_id = self.port_owners[port_idx];
                    if seen.insert(source_node_id) {
                        result.push(source_node_id);
                    }
                }
            }
        }
        result
    }

    /// Topological sort using Kahn's algorithm (BFS).
    /// Returns nodes in evaluation order.
    /// Returns None if the graph contains a cycle.
    pub fn topological_sort(&self) -> Option<Vec<u32>> {
        let mut sorted = Vec::with_capacity(self.node_count());
        let sources = self.find_source_nodes();
        let mut queue: VecDeque<u32> = sources.into_iter().collect();
        let mut visited = HashSet::with_capacity(self.node_count());

        while let Some(node_id) = queue.pop_front() {
            if !visited.insert(node_id) {
                continue;
            }
            sorted.push(node_id);

            for succ in self.get_successor_nodes(node_id) {
                if visited.contains(&succ) {
                    continue;
                }

                let preds = self.get_predecessor_nodes(succ);
                let all_pred_visited = preds.iter().all(|p| visited.contains(p));

                if all_pred_visited {
                    queue.push_back(succ);
                }
            }
        }

        // If we haven't visited all nodes, there's a cycle
        if sorted.len() == self.node_count() {
            Some(sorted)
        } else {
            None
        }
    }

    /// Check if graph contains a cycle.
    pub fn has_cycle(&self) -> bool {
        self.topological_sort().is_none()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::{PortDataType, PortSpec};

    fn make_linear_chain() -> Graph {
        // Linear chain: A -> B -> C
        Graph::from_vecs(
            vec![1, 2, 3],            // node_ids
            vec![7, 2, 2],            // node_types
            vec![0, 1, 1],            // node_input_count
            vec![1, 1, 0],            // node_output_count
            vec![101, 201, 202, 301], // port_ids
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            ],
            vec![1, 2, 2, 3],               // port_owners
            vec![false, true, false, true], // port_is_input
            vec![401, 402],                 // edge_ids
            vec![101, 202],                 // edge_sources
            vec![201, 301],                 // edge_targets
        )
    }

    fn make_diamond_graph() -> Graph {
        // Diamond: A -> B, A -> C, B -> D, C -> D
        Graph::from_vecs(
            vec![1, 2, 3, 4], // node_ids: A, B, C, D
            vec![7, 2, 2, 2], // node_types
            vec![0, 1, 1, 2], // node_input_count
            vec![2, 1, 1, 0], // node_output_count
            vec![
                101, 102, // A outputs
                201, 202, // B input, output
                301, 302, // C input, output
                401, 402, // D inputs
            ],
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 1).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 1).to_encoded(),
            ],
            vec![1, 1, 2, 2, 3, 3, 4, 4], // port_owners
            vec![false, false, true, false, true, false, true, true], // port_is_input
            vec![501, 502, 503, 504],     // edge_ids
            vec![101, 102, 202, 302],     // edge_sources: A->B, A->C, B->D, C->D
            vec![201, 301, 401, 402],     // edge_targets
        )
    }

    fn make_cycle_graph() -> Graph {
        // Cycle: A -> B -> A (simple 2-node cycle)
        Graph::from_vecs(
            vec![1, 2],               // node_ids
            vec![2, 2],               // node_types
            vec![1, 1],               // node_input_count
            vec![1, 1],               // node_output_count
            vec![101, 102, 201, 202], // port_ids
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            ],
            vec![1, 1, 2, 2],               // port_owners
            vec![true, false, true, false], // port_is_input: A(in,out), B(in,out)
            vec![301, 302],                 // edge_ids
            vec![102, 202],                 // edge_sources: A->B, B->A
            vec![201, 101],                 // edge_targets
        )
    }

    #[test]
    fn find_source_nodes_returns_roots() {
        let graph = make_linear_chain();
        let sources = graph.find_source_nodes();
        assert_eq!(sources, vec![1]);
    }

    #[test]
    fn find_source_nodes_empty_graph_returns_empty() {
        let graph = Graph::new();
        assert!(graph.find_source_nodes().is_empty());
    }

    #[test]
    fn find_sink_nodes_returns_leaves() {
        let graph = make_linear_chain();
        let sinks = graph.find_sink_nodes();
        assert_eq!(sinks, vec![3]);
    }

    #[test]
    fn get_successor_nodes_returns_downstream() {
        let graph = make_linear_chain();
        assert_eq!(graph.get_successor_nodes(1), vec![2]);
        assert_eq!(graph.get_successor_nodes(2), vec![3]);
        assert!(graph.get_successor_nodes(3).is_empty());
    }

    #[test]
    fn get_successor_nodes_deduplicates() {
        let graph = make_diamond_graph();
        let succs = graph.get_successor_nodes(1);
        assert_eq!(succs.len(), 2);
        assert!(succs.contains(&2));
        assert!(succs.contains(&3));
    }

    #[test]
    fn get_predecessor_nodes_returns_upstream() {
        let graph = make_linear_chain();
        assert!(graph.get_predecessor_nodes(1).is_empty());
        assert_eq!(graph.get_predecessor_nodes(2), vec![1]);
        assert_eq!(graph.get_predecessor_nodes(3), vec![2]);
    }

    #[test]
    fn topological_sort_linear_chain() {
        let graph = make_linear_chain();
        let sorted = graph.topological_sort().unwrap();
        assert_eq!(sorted, vec![1, 2, 3]);
    }

    #[test]
    fn topological_sort_diamond_graph() {
        let graph = make_diamond_graph();
        let sorted = graph.topological_sort().unwrap();

        assert_eq!(sorted[0], 1);
        assert_eq!(sorted[3], 4);

        let b_pos = sorted.iter().position(|&x| x == 2).unwrap();
        let c_pos = sorted.iter().position(|&x| x == 3).unwrap();
        assert!(b_pos > 0 && b_pos < 3);
        assert!(c_pos > 0 && c_pos < 3);
    }

    #[test]
    fn topological_sort_returns_none_for_cycle() {
        let graph = make_cycle_graph();
        assert!(graph.topological_sort().is_none());
    }

    #[test]
    fn has_cycle_detects_simple_cycle() {
        let graph = make_cycle_graph();
        assert!(graph.has_cycle());
    }

    #[test]
    fn has_cycle_returns_false_for_dag() {
        let graph = make_linear_chain();
        assert!(!graph.has_cycle());
    }
}
