use super::dispatch::evaluate_node;
use super::document::DocumentView;
use super::result::EvaluationResult;

/// Evaluate all nodes in topological order.
pub fn evaluate_graph(doc: &DocumentView) -> Option<EvaluationResult> {
    let node_count = doc.graph.node_count();
    if node_count == 0 {
        return Some(EvaluationResult::new(0));
    }

    let sorted_nodes = doc.graph.topological_sort()?;
    let mut result = EvaluationResult::new(node_count);

    for &node_id in &sorted_nodes {
        let Some(node_type) = doc.graph.get_node_type(node_id) else {
            continue;
        };
        evaluate_node(doc, node_id, node_type, &mut result);
    }

    Some(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::Graph;
    use crate::nodes::NodeType;
    use crate::sim::Float3;
    use std::collections::HashMap;

    fn make_empty_doc<'a>(
        graph: &'a Graph,
        scalars: &'a HashMap<u64, f32>,
        vectors: &'a HashMap<u64, Float3>,
        flags: &'a HashMap<u64, i32>,
        keyframe_ranges: &'a HashMap<u64, (usize, usize)>,
    ) -> DocumentView<'a> {
        DocumentView {
            graph,
            scalars,
            vectors,
            flags,
            keyframes: &[],
            keyframe_ranges,
        }
    }

    #[test]
    fn evaluate_empty_graph() {
        let graph = Graph::from_vecs(
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
            vec![],
        );
        let scalars = HashMap::new();
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframe_ranges = HashMap::new();
        let doc = make_empty_doc(&graph, &scalars, &vectors, &flags, &keyframe_ranges);

        let result = evaluate_graph(&doc).unwrap();
        assert!(result.anchors.is_empty());
        assert!(result.paths.is_empty());
    }

    /// Anchor(6) -> Geo(1) -> Reverse(7) -> CopyPath(8) -> Geo_cosmetic(3)
    ///                    \-> ReversePath(14) -^
    ///
    /// CopyPath needs:
    /// - Anchor from Reverse (which gets it from Geo)
    /// - Path from ReversePath (which gets it from Geo's path output)
    #[test]
    fn evaluate_cosmetic_copypath_chain() {
        use crate::graph::PortDataType;
        use crate::graph::PortSpec;

        let node_ids = vec![6, 1, 7, 14, 8, 3];
        let node_types = vec![
            NodeType::Anchor as u8,
            NodeType::Geometric as u8,
            NodeType::Reverse as u8,
            NodeType::ReversePath as u8,
            NodeType::CopyPath as u8,
            NodeType::Geometric as u8,
        ];
        let node_input_counts = vec![8, 2, 1, 1, 4, 2];
        let node_output_counts = vec![1, 2, 1, 1, 2, 2];

        fn encode_port(data_type: PortDataType, local_index: u8) -> u32 {
            PortSpec::new(data_type, local_index).to_encoded()
        }

        let mut port_ids = Vec::new();
        let mut port_types = Vec::new();
        let mut port_owners = Vec::new();
        let mut port_is_input = Vec::new();
        let mut next_port_id = 100u32;

        // Node 6 (Anchor): 8 inputs, 1 output
        for i in 0..8 {
            port_ids.push(next_port_id);
            port_types.push(encode_port(
                if i == 0 {
                    PortDataType::Vector
                } else {
                    PortDataType::Scalar
                },
                i as u8,
            ));
            port_owners.push(6);
            port_is_input.push(true);
            next_port_id += 1;
        }
        let anchor6_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(6);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 1 (Geo): 2 inputs (Anchor, Duration), 2 outputs (Anchor, Path)
        let geo1_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(1);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(1);
        port_is_input.push(true);
        next_port_id += 1;

        let geo1_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(1);
        port_is_input.push(false);
        next_port_id += 1;

        let geo1_path_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(1);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 7 (Reverse): 1 input, 1 output
        let reverse7_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(7);
        port_is_input.push(true);
        next_port_id += 1;

        let reverse7_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(7);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 14 (ReversePath): 1 input, 1 output
        let rpath14_path_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(14);
        port_is_input.push(true);
        next_port_id += 1;

        let rpath14_path_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(14);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 8 (CopyPath): 4 inputs, 2 outputs
        let copypath8_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        let copypath8_path_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Scalar, 1));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        let copypath8_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(8);
        port_is_input.push(false);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(8);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 3 (Geo cosmetic): 2 inputs, 2 outputs
        let geo3_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(3);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(3);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(3);
        port_is_input.push(false);
        next_port_id += 1;

        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(3);
        port_is_input.push(false);
        let _ = next_port_id;

        let edge_ids = vec![1, 2, 3, 4, 5, 6];
        let edge_sources = vec![
            anchor6_out,
            geo1_anchor_out,
            geo1_path_out,
            reverse7_anchor_out,
            rpath14_path_out,
            copypath8_anchor_out,
        ];
        let edge_targets = vec![
            geo1_anchor_in,
            reverse7_anchor_in,
            rpath14_path_in,
            copypath8_anchor_in,
            copypath8_path_in,
            geo3_anchor_in,
        ];

        let graph = Graph::from_vecs(
            node_ids,
            node_types,
            node_input_counts,
            node_output_counts,
            port_ids,
            port_types,
            port_owners,
            port_is_input,
            edge_ids,
            edge_sources,
            edge_targets,
        );

        let mut scalars = HashMap::new();
        use crate::track::document::input_key;
        scalars.insert(input_key(1, 1), 1.0f32);
        scalars.insert(input_key(3, 1), 1.0f32);
        scalars.insert(input_key(8, 2), 0.0f32); // Start = 0
        scalars.insert(input_key(8, 3), 1.0f32); // End = 1

        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframe_ranges = HashMap::new();

        let doc = make_empty_doc(&graph, &scalars, &vectors, &flags, &keyframe_ranges);

        let result = evaluate_graph(&doc);
        assert!(result.is_some(), "evaluate_graph should succeed");
        let result = result.unwrap();

        assert!(result.anchors.contains_key(&6));
        assert!(result.anchors.contains_key(&1));
        assert!(result.paths.contains_key(&1));
        assert!(result.anchors.contains_key(&7));
        assert!(result.anchors.contains_key(&14));
        assert!(result.paths.contains_key(&14));
        assert!(
            result.anchors.contains_key(&8),
            "CopyPath node 8 should produce an anchor. anchors: {:?}",
            result.anchors.keys().collect::<Vec<_>>()
        );
        assert!(
            result.paths.contains_key(&8),
            "CopyPath node 8 should produce a path. paths: {:?}",
            result.paths.keys().collect::<Vec<_>>()
        );
        assert!(result.anchors.contains_key(&3));
        assert!(result.paths.contains_key(&3));
    }
}
