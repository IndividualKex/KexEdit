use std::collections::HashMap;

use crate::graph::{Graph, PortDataType};
use crate::nodes::{NodeMeta, NodeType};
use crate::sim::Point;

use super::document::DocumentView;

/// Link to another section with connection metadata.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default, PartialEq)]
pub struct SectionLink {
    pub index: i32,
    pub flags: u8,
}

impl SectionLink {
    pub const NONE: Self = Self {
        index: -1,
        flags: 0,
    };
    pub const FLAG_AT_START: u8 = 0x01;
    pub const FLAG_FLIP: u8 = 0x02;

    pub fn new(index: i32, at_start: bool, flip: bool) -> Self {
        let mut flags = 0u8;
        if at_start {
            flags |= Self::FLAG_AT_START;
        }
        if flip {
            flags |= Self::FLAG_FLIP;
        }
        Self { index, flags }
    }

    pub fn at_start(&self) -> bool {
        (self.flags & Self::FLAG_AT_START) != 0
    }

    pub fn flip(&self) -> bool {
        (self.flags & Self::FLAG_FLIP) != 0
    }

    pub fn is_valid(&self) -> bool {
        self.index >= 0
    }
}

/// A section represents a contiguous segment of track points from a single node.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct Section {
    pub start_index: i32,
    pub end_index: i32,
    pub arc_start: f32,
    pub arc_end: f32,
    pub flags: u8,
    pub next: SectionLink,
    pub prev: SectionLink,
    pub spline_start_index: i32,
    pub spline_end_index: i32,
}

impl Section {
    pub const FLAG_REVERSED: u8 = 0x01;
    pub const FLAG_RENDERED: u8 = 0x02;

    pub fn invalid() -> Self {
        Self {
            start_index: -1,
            end_index: -1,
            arc_start: 0.0,
            arc_end: 0.0,
            flags: 0,
            next: SectionLink::NONE,
            prev: SectionLink::NONE,
            spline_start_index: -1,
            spline_end_index: -1,
        }
    }

    pub fn is_valid(&self) -> bool {
        self.start_index >= 0
    }

    pub fn length(&self) -> i32 {
        if self.is_valid() {
            self.end_index - self.start_index + 1
        } else {
            0
        }
    }

    pub fn is_reversed(&self) -> bool {
        (self.flags & Self::FLAG_REVERSED) != 0
    }

    pub fn is_rendered(&self) -> bool {
        (self.flags & Self::FLAG_RENDERED) != 0
    }
}

impl Default for Section {
    fn default() -> Self {
        Self::invalid()
    }
}

fn produces_section(node_type: NodeType) -> bool {
    matches!(
        node_type,
        NodeType::Force
            | NodeType::Geometric
            | NodeType::Curved
            | NodeType::CopyPath
            | NodeType::Bridge
    )
}

/// Collect section-producing nodes from the topologically sorted list.
pub fn collect_sections(
    sorted: &[u32],
    graph: &Graph,
    paths: &HashMap<u32, Vec<Point>>,
) -> (Vec<u32>, HashMap<u32, usize>) {
    let mut section_nodes = Vec::new();
    let mut node_to_section = HashMap::new();

    for &node_id in sorted {
        let Some(node_type) = graph.get_node_type(node_id) else {
            continue;
        };

        if !produces_section(node_type) {
            continue;
        }

        if !paths.contains_key(&node_id) {
            continue;
        }

        let section_index = section_nodes.len();
        node_to_section.insert(node_id, section_index);
        section_nodes.push(node_id);
    }

    (section_nodes, node_to_section)
}

/// Build sections from section nodes, accumulating points.
pub fn build_sections(
    section_nodes: &[u32],
    paths: &HashMap<u32, Vec<Point>>,
    doc: &DocumentView,
) -> (Vec<Point>, Vec<Section>) {
    let mut points = Vec::new();
    let mut sections = Vec::with_capacity(section_nodes.len());

    for &node_id in section_nodes {
        let Some(path) = paths.get(&node_id) else {
            sections.push(Section::invalid());
            continue;
        };

        if path.len() < 2 {
            sections.push(Section::invalid());
            continue;
        }

        let start_index = points.len() as i32;
        points.extend_from_slice(path);
        let end_index = points.len() as i32 - 1;

        let facing = doc.get_meta_flag(node_id, NodeMeta::Facing);
        let render_hidden = doc.get_meta_flag(node_id, NodeMeta::Render);

        let mut flags = 0u8;
        if facing < 0 {
            flags |= Section::FLAG_REVERSED;
        }
        // Render = 0 (or unset) means rendered; Render = 1 means hidden.
        if render_hidden == 0 {
            flags |= Section::FLAG_RENDERED;
        }

        let arc_start = path.first().map(|p| p.spine_arc).unwrap_or(0.0);
        let arc_end = path.last().map(|p| p.spine_arc).unwrap_or(0.0);

        sections.push(Section {
            start_index,
            end_index,
            arc_start,
            arc_end,
            flags,
            next: SectionLink::NONE,
            prev: SectionLink::NONE,
            spline_start_index: -1,
            spline_end_index: -1,
        });
    }

    (points, sections)
}

/// Build traversal order by sorting sections by priority (descending).
/// Cosmetic sections (priority < 0) are excluded.
pub fn build_traversal_order(
    section_nodes: &[u32],
    sections: &[Section],
    doc: &DocumentView,
) -> Vec<i32> {
    let mut candidates: Vec<(usize, i32)> = Vec::new();

    for (i, (&node_id, section)) in section_nodes.iter().zip(sections.iter()).enumerate() {
        if !section.is_valid() {
            continue;
        }

        let priority = doc.get_meta_scalar(node_id, NodeMeta::Priority, 0.0) as i32;
        if priority < 0 {
            continue;
        }

        candidates.push((i, priority));
    }

    // Insertion sort by priority (descending).
    for i in 1..candidates.len() {
        let mut j = i;
        while j > 0 && candidates[j].1 > candidates[j - 1].1 {
            candidates.swap(j, j - 1);
            j -= 1;
        }
    }

    candidates.into_iter().map(|(idx, _)| idx as i32).collect()
}

/// Compute graph-based continuations (next/prev links) using document edges.
pub fn compute_continuations(
    section_nodes: &[u32],
    node_to_section: &HashMap<u32, usize>,
    sections: &mut [Section],
    doc: &DocumentView,
) {
    for i in 0..sections.len() {
        if !sections[i].is_valid() || !sections[i].is_rendered() {
            continue;
        }

        let node_id = section_nodes[i];
        if let Some(next_section_idx) = find_next_section(doc, node_id, node_to_section) {
            if sections[next_section_idx].is_rendered() {
                sections[i].next = SectionLink::new(next_section_idx as i32, true, false);
                sections[next_section_idx].prev = SectionLink::new(i as i32, false, false);
            }
        }
    }
}

fn find_next_section(
    doc: &DocumentView,
    node_id: u32,
    node_to_section: &HashMap<u32, usize>,
) -> Option<usize> {
    let output_port = doc
        .graph
        .try_get_output_by_spec(node_id, PortDataType::Anchor, 0)?;

    for &ei in doc.graph.outgoing_edge_indices_from_port(output_port) {
        let target_port = doc.graph.edge_targets[ei];
        let target_port_idx = doc.graph.get_port_index(target_port)?;
        let target_node = doc.graph.port_owners[target_port_idx];
        let target_type = doc.graph.get_node_type(target_node)?;

        // Reverse / ReversePath don't propagate forward connections.
        if matches!(target_type, NodeType::Reverse | NodeType::ReversePath) {
            continue;
        }

        if let Some(&section_idx) = node_to_section.get(&target_node) {
            return Some(section_idx);
        }

        // Anchors pass through to whatever follows them.
        if target_type == NodeType::Anchor {
            if let Some(idx) = find_next_section(doc, target_node, node_to_section) {
                return Some(idx);
            }
        }
    }

    None
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::sim::Float3;

    #[test]
    fn section_link_none_is_invalid() {
        assert!(!SectionLink::NONE.is_valid());
        assert_eq!(SectionLink::NONE.index, -1);
    }

    #[test]
    fn section_link_flags_encode_correctly() {
        let link = SectionLink::new(5, true, false);
        assert!(link.is_valid());
        assert!(link.at_start());
        assert!(!link.flip());
        assert_eq!(link.index, 5);

        let link2 = SectionLink::new(3, false, true);
        assert!(!link2.at_start());
        assert!(link2.flip());

        let link3 = SectionLink::new(7, true, true);
        assert!(link3.at_start());
        assert!(link3.flip());
    }

    #[test]
    fn section_invalid_has_negative_indices() {
        let section = Section::invalid();
        assert!(!section.is_valid());
        assert_eq!(section.start_index, -1);
        assert_eq!(section.end_index, -1);
        assert_eq!(section.length(), 0);
    }

    #[test]
    fn section_flags_encode_correctly() {
        let mut section = Section::invalid();
        section.start_index = 0;
        section.end_index = 10;
        section.flags = Section::FLAG_REVERSED | Section::FLAG_RENDERED;

        assert!(section.is_valid());
        assert!(section.is_reversed());
        assert!(section.is_rendered());
        assert_eq!(section.length(), 11);
    }

    fn make_test_graph() -> Graph {
        // Anchor(1) -> Force(2) -> Geometric(3)
        Graph::from_vecs(
            vec![1, 2, 3],
            vec![
                NodeType::Anchor as u8,
                NodeType::Force as u8,
                NodeType::Geometric as u8,
            ],
            vec![0, 1, 1],
            vec![1, 2, 1],
            vec![101, 102, 201, 202, 301],
            vec![
                crate::graph::PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                crate::graph::PortSpec::new(PortDataType::Path, 0).to_encoded(),
                crate::graph::PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                crate::graph::PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                crate::graph::PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            ],
            vec![1, 1, 2, 2, 3],
            vec![false, false, true, false, true],
            vec![401, 402],
            vec![101, 202],
            vec![201, 301],
        )
    }

    fn make_test_point(arc: f32, pos: Float3, dir: Float3) -> Point {
        Point {
            heart_position: pos,
            direction: dir,
            normal: Float3::new(0.0, 1.0, 0.0),
            lateral: Float3::new(1.0, 0.0, 0.0),
            velocity: 10.0,
            normal_force: 1.0,
            lateral_force: 0.0,
            heart_arc: arc,
            spine_arc: arc,
            heart_advance: 0.0,
            friction_origin: 0.0,
            roll_speed: 0.0,
            heart_offset: 1.1,
            friction: 0.021,
            resistance: 2e-5,
        }
    }

    #[test]
    fn collect_sections_filters_correct_types() {
        let graph = make_test_graph();
        let sorted = vec![1, 2, 3];

        let mut paths = HashMap::new();
        paths.insert(
            2,
            vec![
                make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
                make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            ],
        );
        paths.insert(
            3,
            vec![
                make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
                make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
            ],
        );

        let (section_nodes, node_to_section) = collect_sections(&sorted, &graph, &paths);

        assert_eq!(section_nodes.len(), 2);
        assert_eq!(section_nodes[0], 2);
        assert_eq!(section_nodes[1], 3);
        assert_eq!(node_to_section.get(&2), Some(&0));
        assert_eq!(node_to_section.get(&3), Some(&1));
        assert_eq!(node_to_section.get(&1), None);
    }

    #[test]
    fn collect_sections_skips_missing_paths() {
        let graph = make_test_graph();
        let sorted = vec![1, 2, 3];

        let mut paths = HashMap::new();
        paths.insert(
            2,
            vec![
                make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
                make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            ],
        );

        let (section_nodes, _) = collect_sections(&sorted, &graph, &paths);
        assert_eq!(section_nodes.len(), 1);
        assert_eq!(section_nodes[0], 2);
    }

    #[test]
    fn build_sections_accumulates_points() {
        let graph = make_test_graph();

        let mut paths = HashMap::new();
        paths.insert(
            2,
            vec![
                make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
                make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            ],
        );
        paths.insert(
            3,
            vec![
                make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
                make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
                make_test_point(3.0, Float3::new(0.0, 0.0, 3.0), Float3::new(0.0, 0.0, 1.0)),
            ],
        );

        let section_nodes = vec![2, 3];
        let doc = DocumentView {
            graph: &graph,
            scalars: &HashMap::new(),
            vectors: &HashMap::new(),
            flags: &HashMap::new(),
            keyframes: &[],
            keyframe_ranges: &HashMap::new(),
        };

        let (points, sections) = build_sections(&section_nodes, &paths, &doc);

        assert_eq!(points.len(), 5);
        assert_eq!(sections.len(), 2);

        assert_eq!(sections[0].start_index, 0);
        assert_eq!(sections[0].end_index, 1);
        assert_eq!(sections[0].length(), 2);

        assert_eq!(sections[1].start_index, 2);
        assert_eq!(sections[1].end_index, 4);
        assert_eq!(sections[1].length(), 3);
    }

    #[test]
    fn build_sections_invalid_for_short_paths() {
        let graph = make_test_graph();

        let mut paths = HashMap::new();
        paths.insert(
            2,
            vec![make_test_point(
                0.0,
                Float3::ZERO,
                Float3::new(0.0, 0.0, 1.0),
            )],
        );

        let section_nodes = vec![2];
        let doc = DocumentView {
            graph: &graph,
            scalars: &HashMap::new(),
            vectors: &HashMap::new(),
            flags: &HashMap::new(),
            keyframes: &[],
            keyframe_ranges: &HashMap::new(),
        };

        let (points, sections) = build_sections(&section_nodes, &paths, &doc);

        assert_eq!(points.len(), 0);
        assert_eq!(sections.len(), 1);
        assert!(!sections[0].is_valid());
    }

    #[test]
    fn build_traversal_order_sorts_by_priority() {
        let graph = make_test_graph();
        let section_nodes = vec![2, 3];

        let sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 4,
                flags: Section::FLAG_RENDERED,
                ..Section::invalid()
            },
        ];

        let mut scalars = HashMap::new();
        scalars.insert(
            crate::track::document::input_key(2, NodeMeta::Priority.as_u8()),
            5.0,
        );
        scalars.insert(
            crate::track::document::input_key(3, NodeMeta::Priority.as_u8()),
            10.0,
        );

        let doc = DocumentView {
            graph: &graph,
            scalars: &scalars,
            vectors: &HashMap::new(),
            flags: &HashMap::new(),
            keyframes: &[],
            keyframe_ranges: &HashMap::new(),
        };

        let order = build_traversal_order(&section_nodes, &sections, &doc);
        assert_eq!(order, vec![1, 0]);
    }

    #[test]
    fn build_traversal_order_excludes_negative_priority() {
        let graph = make_test_graph();
        let section_nodes = vec![2, 3];

        let sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 4,
                flags: Section::FLAG_RENDERED,
                ..Section::invalid()
            },
        ];

        let mut scalars = HashMap::new();
        scalars.insert(
            crate::track::document::input_key(2, NodeMeta::Priority.as_u8()),
            5.0,
        );
        scalars.insert(
            crate::track::document::input_key(3, NodeMeta::Priority.as_u8()),
            -1.0,
        );

        let doc = DocumentView {
            graph: &graph,
            scalars: &scalars,
            vectors: &HashMap::new(),
            flags: &HashMap::new(),
            keyframes: &[],
            keyframe_ranges: &HashMap::new(),
        };

        let order = build_traversal_order(&section_nodes, &sections, &doc);
        assert_eq!(order, vec![0]);
    }
}
