use super::dispatch::{evaluate_node, map_csharp_node_type};
use super::document::DocumentView;
use super::result::EvaluationResult;

/// Evaluates all nodes in the graph in topological order.
pub fn evaluate_graph(doc: &DocumentView) -> Option<EvaluationResult> {
    let node_count = doc.graph.node_count();
    if node_count == 0 {
        return Some(EvaluationResult::new(0));
    }

    let sorted_nodes = doc.graph.topological_sort()?;
    let mut result = EvaluationResult::new(node_count);

    for &node_id in &sorted_nodes {
        let Some(node_type_raw) = doc.graph.get_node_type(node_id) else {
            continue;
        };

        let Some(node_type) = map_csharp_node_type(node_type_raw) else {
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

    /// Test that simulates the shuttle track cosmetic section chain:
    /// Anchor(6) -> Geo(1) -> Reverse(7) -> CopyPath_cosmetic(8) -> Geo_cosmetic(3)
    ///                    \-> ReversePath(14) -^
    ///
    /// CopyPath needs:
    /// - Anchor from Reverse (which gets it from Geo)
    /// - Path from ReversePath (which gets it from Geo's path output)
    #[test]
    fn evaluate_cosmetic_copypath_chain() {
        use crate::graph::PortDataType;
        use crate::graph::PortSpec;

        // C# node type values (from dispatch.rs map_csharp_node_type):
        // Anchor=7, Geometric=3, Reverse=8, ReversePath=9, CopyPath=5

        // Build a minimal graph that represents the cosmetic chain:
        // Node 6: Anchor
        // Node 1: Geometric (depends on Anchor)
        // Node 7: Reverse (depends on Geo anchor)
        // Node 14: ReversePath (depends on Geo path)
        // Node 8: CopyPath cosmetic (depends on Reverse anchor + ReversePath path)
        // Node 3: Geometric cosmetic (depends on CopyPath anchor)

        let node_ids = vec![6, 1, 7, 14, 8, 3];
        let node_types = vec![7, 3, 8, 9, 5, 3]; // Anchor, Geo, Reverse, ReversePath, CopyPath, Geo
        let node_input_counts = vec![8, 2, 1, 1, 4, 2]; // Schema input counts
        let node_output_counts = vec![1, 2, 1, 1, 2, 2]; // Schema output counts

        // Port spec encoding: (data_type << 8) | local_index
        fn encode_port(data_type: PortDataType, local_index: u8) -> u32 {
            PortSpec::new(data_type, local_index).to_encoded()
        }

        // Ports - create them in node order, inputs then outputs per node
        let mut port_ids = Vec::new();
        let mut port_types = Vec::new();
        let mut port_owners = Vec::new();
        let mut port_is_input = Vec::new();
        let mut next_port_id = 100u32;

        // Track specific port IDs for edges
        let anchor6_out;
        let geo1_anchor_in;
        let geo1_anchor_out;
        let geo1_path_out;
        let reverse7_anchor_in;
        let reverse7_anchor_out;
        let rpath14_path_in;
        let rpath14_path_out;
        let copypath8_anchor_in;
        let copypath8_path_in;
        let copypath8_anchor_out;
        let geo3_anchor_in;

        // Node 6 (Anchor): 8 inputs (Position, Roll, Pitch, Yaw, Velocity, Heart, Friction, Resistance), 1 output (Anchor)
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
        anchor6_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(6);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 1 (Geo): 2 inputs (Anchor, Duration), 2 outputs (Anchor, Path)
        geo1_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(1);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id); // Duration input
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(1);
        port_is_input.push(true);
        next_port_id += 1;

        geo1_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(1);
        port_is_input.push(false);
        next_port_id += 1;

        geo1_path_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(1);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 7 (Reverse): 1 input (Anchor), 1 output (Anchor)
        reverse7_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(7);
        port_is_input.push(true);
        next_port_id += 1;

        reverse7_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(7);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 14 (ReversePath): 1 input (Path), 1 output (Path)
        rpath14_path_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(14);
        port_is_input.push(true);
        next_port_id += 1;

        rpath14_path_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(14);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 8 (CopyPath): 4 inputs (Anchor, Path, Start, End), 2 outputs (Anchor, Path)
        copypath8_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        copypath8_path_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id); // Start input
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id); // End input
        port_types.push(encode_port(PortDataType::Scalar, 1));
        port_owners.push(8);
        port_is_input.push(true);
        next_port_id += 1;

        copypath8_anchor_out = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(8);
        port_is_input.push(false);
        next_port_id += 1;

        port_ids.push(next_port_id); // Path output
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(8);
        port_is_input.push(false);
        next_port_id += 1;

        // Node 3 (Geo cosmetic): 2 inputs (Anchor, Duration), 2 outputs (Anchor, Path)
        geo3_anchor_in = next_port_id;
        port_ids.push(next_port_id);
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(3);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id); // Duration input
        port_types.push(encode_port(PortDataType::Scalar, 0));
        port_owners.push(3);
        port_is_input.push(true);
        next_port_id += 1;

        port_ids.push(next_port_id); // Anchor output
        port_types.push(encode_port(PortDataType::Anchor, 0));
        port_owners.push(3);
        port_is_input.push(false);
        next_port_id += 1;

        port_ids.push(next_port_id); // Path output
        port_types.push(encode_port(PortDataType::Path, 0));
        port_owners.push(3);
        port_is_input.push(false);
        let _ = next_port_id;

        // Edges:
        // 1. Anchor(6) -> Geo(1): anchor6_out -> geo1_anchor_in
        // 2. Geo(1) -> Reverse(7): geo1_anchor_out -> reverse7_anchor_in
        // 3. Geo(1) -> ReversePath(14): geo1_path_out -> rpath14_path_in
        // 4. Reverse(7) -> CopyPath(8): reverse7_anchor_out -> copypath8_anchor_in
        // 5. ReversePath(14) -> CopyPath(8): rpath14_path_out -> copypath8_path_in
        // 6. CopyPath(8) -> Geo(3): copypath8_anchor_out -> geo3_anchor_in
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

        // Set up scalars for duration (default 1.0)
        let mut scalars = HashMap::new();
        // input_key format: (node_id as u64) << 32 | (port_index as u64)
        fn input_key(node_id: u32, port_index: i32) -> u64 {
            (node_id as u64) << 32 | (port_index as u64)
        }
        // Set duration for Geo nodes (port 1)
        scalars.insert(input_key(1, 1), 1.0f32);
        scalars.insert(input_key(3, 1), 1.0f32);
        // Set CopyPath Start/End (ports 2 and 3)
        scalars.insert(input_key(8, 2), 0.0f32); // Start = 0
        scalars.insert(input_key(8, 3), 1.0f32); // End = 1

        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframe_ranges = HashMap::new();

        let doc = make_empty_doc(&graph, &scalars, &vectors, &flags, &keyframe_ranges);

        // Evaluate the graph
        let result = evaluate_graph(&doc);
        assert!(result.is_some(), "evaluate_graph should succeed");
        let result = result.unwrap();

        // Check that all nodes got evaluated
        // Anchor node (6) should produce an anchor
        assert!(
            result.anchors.contains_key(&6),
            "Anchor node 6 should produce an anchor"
        );

        // Geo node (1) should produce an anchor and a path
        assert!(
            result.anchors.contains_key(&1),
            "Geo node 1 should produce an anchor"
        );
        assert!(
            result.paths.contains_key(&1),
            "Geo node 1 should produce a path"
        );

        // Reverse node (7) should produce an anchor (no path)
        assert!(
            result.anchors.contains_key(&7),
            "Reverse node 7 should produce an anchor"
        );

        // ReversePath node (14) should produce an anchor and a path
        assert!(
            result.anchors.contains_key(&14),
            "ReversePath node 14 should produce an anchor"
        );
        assert!(
            result.paths.contains_key(&14),
            "ReversePath node 14 should produce a path"
        );

        // CopyPath cosmetic (8) should produce an anchor and a path
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

        // Geo cosmetic (3) should produce an anchor and a path
        assert!(
            result.anchors.contains_key(&3),
            "Geo cosmetic node 3 should produce an anchor"
        );
        assert!(
            result.paths.contains_key(&3),
            "Geo cosmetic node 3 should produce a path"
        );
    }
}
