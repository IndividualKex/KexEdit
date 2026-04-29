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
    use crate::graph::{Graph, GraphBuilder, PortDataType};
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
        let graph = Graph::new();
        let scalars = HashMap::new();
        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframe_ranges = HashMap::new();
        let doc = make_empty_doc(&graph, &scalars, &vectors, &flags, &keyframe_ranges);

        let result = evaluate_graph(&doc).unwrap();
        assert!(result.anchors.is_empty());
        assert!(result.paths.is_empty());
    }

    /// Anchor → Geo → Reverse → CopyPath → Geo_cosmetic
    ///              \→ ReversePath -^
    ///
    /// CopyPath needs:
    /// - Anchor from Reverse (which gets it from Geo)
    /// - Path from ReversePath (which gets it from Geo's path output)
    #[test]
    fn evaluate_cosmetic_copypath_chain() {
        use crate::track::document::input_key;

        let mut b = GraphBuilder::new();
        let scalar = PortDataType::Scalar;
        let vector = PortDataType::Vector;
        let anchor_t = PortDataType::Anchor;
        let path_t = PortDataType::Path;

        let anchor = b.add_node(
            NodeType::Anchor,
            &[vector, scalar, scalar, scalar, scalar, scalar, scalar, scalar],
            &[anchor_t],
        );
        let geo = b.add_node(NodeType::Geometric, &[anchor_t, scalar], &[anchor_t, path_t]);
        let reverse = b.add_node(NodeType::Reverse, &[anchor_t], &[anchor_t]);
        let reverse_path = b.add_node(NodeType::ReversePath, &[path_t], &[path_t]);
        let copy = b.add_node(
            NodeType::CopyPath,
            &[anchor_t, path_t, scalar, scalar],
            &[anchor_t, path_t],
        );
        let geo_cosmetic = b.add_node(NodeType::Geometric, &[anchor_t, scalar], &[anchor_t, path_t]);

        b.connect(anchor.output(0), geo.input(0));
        b.connect(geo.output(0), reverse.input(0));
        b.connect(geo.output(1), reverse_path.input(0));
        b.connect(reverse.output(0), copy.input(0));
        b.connect(reverse_path.output(0), copy.input(1));
        b.connect(copy.output(0), geo_cosmetic.input(0));

        let graph = b.build();

        let mut scalars = HashMap::new();
        scalars.insert(input_key(geo.id, 1), 1.0f32);
        scalars.insert(input_key(geo_cosmetic.id, 1), 1.0f32);
        scalars.insert(input_key(copy.id, 2), 0.0f32);
        scalars.insert(input_key(copy.id, 3), 1.0f32);

        let vectors = HashMap::new();
        let flags = HashMap::new();
        let keyframe_ranges = HashMap::new();
        let doc = make_empty_doc(&graph, &scalars, &vectors, &flags, &keyframe_ranges);

        let result = evaluate_graph(&doc).expect("evaluate_graph");

        assert!(result.anchors.contains_key(&anchor.id));
        assert!(result.anchors.contains_key(&geo.id));
        assert!(result.paths.contains_key(&geo.id));
        assert!(result.anchors.contains_key(&reverse.id));
        assert!(result.anchors.contains_key(&reverse_path.id));
        assert!(result.paths.contains_key(&reverse_path.id));
        assert!(
            result.anchors.contains_key(&copy.id),
            "CopyPath should produce an anchor"
        );
        assert!(
            result.paths.contains_key(&copy.id),
            "CopyPath should produce a path"
        );
        assert!(result.anchors.contains_key(&geo_cosmetic.id));
        assert!(result.paths.contains_key(&geo_cosmetic.id));
    }
}
