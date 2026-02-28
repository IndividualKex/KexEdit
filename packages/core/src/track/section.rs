use std::collections::HashMap;

use crate::graph::{Graph, PortDataType};
use crate::nodes::PropertyId;
use crate::sim::Point;

use super::dispatch::node_meta;
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
    pub style_index: u8,
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
            style_index: 0,
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

// C# node types that produce sections
const SECTION_PRODUCING_TYPES: [u32; 5] = [
    2, // Force
    3, // Geometric
    4, // Curved
    5, // CopyPath
    6, // Bridge
];

/// Collect section-producing nodes from the topologically sorted list.
/// Returns (section_nodes, node_to_section map).
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

        if !SECTION_PRODUCING_TYPES.contains(&node_type) {
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
/// Returns (accumulated_points, sections).
pub fn build_sections(
    section_nodes: &[u32],
    paths: &HashMap<u32, Vec<Point>>,
    doc: &DocumentView,
    default_style_index: u8,
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

        // Get flags from document
        let facing = doc.get_flag(node_id, node_meta::FACING);
        let render_hidden = doc.get_flag(node_id, node_meta::RENDER);

        let mut flags = 0u8;
        if facing < 0 {
            flags |= Section::FLAG_REVERSED;
        }
        // Render = 0 means rendered, Render = 1 means hidden
        // Default (missing flag, returns 0) is rendered
        if render_hidden == 0 {
            flags |= Section::FLAG_RENDERED;
        }

        // Get style index
        let style_index = get_style_index(doc, node_id, default_style_index);

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
            style_index,
        });
    }

    (points, sections)
}

fn get_style_index(doc: &DocumentView, node_id: u32, default: u8) -> u8 {
    let has_override = doc.get_flag(node_id, node_meta::OVERRIDE_TRACK_STYLE) != 0;
    if !has_override {
        return default;
    }

    let keyframes = doc.get_keyframes(node_id, PropertyId::TrackStyle as u8);
    if keyframes.is_empty() {
        return default;
    }

    keyframes[0].value.round().clamp(0.0, 255.0) as u8
}

/// Build traversal order by sorting sections by priority (descending).
/// Only includes sections with priority >= 0 (cosmetic sections have priority < 0).
pub fn build_traversal_order(
    section_nodes: &[u32],
    sections: &[Section],
    doc: &DocumentView,
) -> Vec<i32> {
    // Collect candidates with their priorities
    // C# reads priority from Scalars (floats), not Flags
    let mut candidates: Vec<(usize, i32)> = Vec::new();

    for (i, (&node_id, section)) in section_nodes.iter().zip(sections.iter()).enumerate() {
        // Only include valid sections (matching C# behavior)
        if !section.is_valid() {
            continue;
        }

        // Priority is stored as a scalar (float), default is 0
        let priority = doc.get_scalar(node_id, node_meta::PRIORITY, 0.0) as i32;
        if priority < 0 {
            continue;
        }

        candidates.push((i, priority));
    }

    // Insertion sort by priority (descending) - matches C# implementation
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
                // Link current section's end to next section's start
                sections[i].next = SectionLink::new(next_section_idx as i32, true, false);
                // Link next section's start back to current section's end
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
    // Get anchor output port
    let output_port = doc
        .graph
        .try_get_output_by_spec(node_id, PortDataType::Anchor, 0)?;

    // Find edges from this port
    for i in 0..doc.graph.edge_ids.len() {
        if doc.graph.edge_sources[i] != output_port {
            continue;
        }

        let target_port = doc.graph.edge_targets[i];
        let target_port_idx = doc.graph.get_port_index(target_port)?;
        let target_node = doc.graph.port_owners[target_port_idx];
        let target_type = doc.graph.get_node_type(target_node)?;

        // Skip Reverse and ReversePath nodes (they don't propagate forward connections)
        if target_type == 8 || target_type == 9 {
            continue;
        }

        // If target is a section-producing node, return its index
        if let Some(&section_idx) = node_to_section.get(&target_node) {
            return Some(section_idx);
        }

        // For Anchor nodes (7), recursively search forward
        if target_type == 7 {
            if let Some(idx) = find_next_section(doc, target_node, node_to_section) {
                return Some(idx);
            }
        }
    }

    None
}

const SPATIAL_TOLERANCE: f32 = 0.01;
const DIRECTION_THRESHOLD: f32 = 0.9;

/// Compute spatial continuations for sections without graph-based links.
pub fn compute_spatial_continuations(
    points: &[Point],
    sections: &mut [Section],
    traversal_order: &[i32],
) {
    let traversal_set: std::collections::HashSet<i32> = traversal_order.iter().copied().collect();

    for i in 0..sections.len() {
        if !sections[i].is_valid() {
            continue;
        }

        // Find spatial match for next if not already linked
        if !sections[i].next.is_valid() {
            if let Some(link) = find_spatial_match(points, sections, i, true, &traversal_set) {
                sections[i].next = link;
            }
        }

        // Find spatial match for prev if not already linked
        if !sections[i].prev.is_valid() {
            if let Some(link) = find_spatial_match(points, sections, i, false, &traversal_set) {
                sections[i].prev = link;
            }
        }
    }
}

fn find_spatial_match(
    points: &[Point],
    sections: &[Section],
    section_idx: usize,
    is_next: bool,
    traversal_set: &std::collections::HashSet<i32>,
) -> Option<SectionLink> {
    let section = &sections[section_idx];
    if !section.is_valid() {
        return None;
    }

    // Get our endpoint
    let our_point_idx = if is_next {
        section.end_index as usize
    } else {
        section.start_index as usize
    };

    if our_point_idx >= points.len() {
        return None;
    }

    let our_point = &points[our_point_idx];
    let our_pos = our_point.heart_position;
    let our_dir = our_point.direction;

    let mut best_match: Option<SectionLink> = None;
    let mut best_dist = f32::MAX;
    let mut best_is_cosmetic = false;

    for (i, candidate) in sections.iter().enumerate() {
        if i == section_idx || !candidate.is_valid() {
            continue;
        }

        let cand_start_idx = candidate.start_index as usize;
        let cand_end_idx = candidate.end_index as usize;

        if cand_start_idx >= points.len() || cand_end_idx >= points.len() {
            continue;
        }

        let cand_start = &points[cand_start_idx];
        let cand_end = &points[cand_end_idx];

        let is_cosmetic = !traversal_set.contains(&(i as i32));

        if is_next {
            // Pattern A: candidate start near our end, same direction
            let dist_start = distance(our_pos, cand_start.heart_position);
            let dir_dot_start = dot(our_dir, cand_start.direction);

            if dist_start < SPATIAL_TOLERANCE
                && dir_dot_start > DIRECTION_THRESHOLD
                && is_better_match(dist_start, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, true, false));
                best_dist = dist_start;
                best_is_cosmetic = is_cosmetic;
            }

            // Pattern B: candidate end near our end, opposite direction
            let dist_end = distance(our_pos, cand_end.heart_position);
            let dir_dot_end = dot(our_dir, cand_end.direction);

            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end < -DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, true));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }
        } else {
            let dist_end = distance(our_pos, cand_end.heart_position);
            let dir_dot_end = dot(our_dir, cand_end.direction);
            let dist_start = distance(our_pos, cand_start.heart_position);
            let dir_dot_start = dot(our_dir, cand_start.direction);

            // Pattern A: candidate end near our start, same direction (contiguous)
            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end > DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, false));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }

            // Pattern B: candidate end near our start, opposite direction (overhang)
            if dist_end < SPATIAL_TOLERANCE
                && dir_dot_end < -DIRECTION_THRESHOLD
                && is_better_match(dist_end, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, false, true));
                best_dist = dist_end;
                best_is_cosmetic = is_cosmetic;
            }

            // Pattern C: candidate start near our start, opposite direction
            if dist_start < SPATIAL_TOLERANCE
                && dir_dot_start < -DIRECTION_THRESHOLD
                && is_better_match(dist_start, is_cosmetic, best_dist, best_is_cosmetic)
            {
                best_match = Some(SectionLink::new(i as i32, true, true));
                best_dist = dist_start;
                best_is_cosmetic = is_cosmetic;
            }
        }
    }

    best_match
}

fn is_better_match(dist: f32, is_cosmetic: bool, best_dist: f32, best_is_cosmetic: bool) -> bool {
    // Prefer cosmetic sections for overhang support
    if is_cosmetic && !best_is_cosmetic {
        return true;
    }
    if !is_cosmetic && best_is_cosmetic {
        return false;
    }
    // Among same cosmetic status, prefer closer
    dist < best_dist
}

fn distance(a: crate::sim::Float3, b: crate::sim::Float3) -> f32 {
    let dx = a.x - b.x;
    let dy = a.y - b.y;
    let dz = a.z - b.z;
    (dx * dx + dy * dy + dz * dz).sqrt()
}

fn dot(a: crate::sim::Float3, b: crate::sim::Float3) -> f32 {
    a.x * b.x + a.y * b.y + a.z * b.z
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
        // Graph: Anchor(1) -> Force(2) -> Geometric(3)
        Graph::from_vecs(
            vec![1, 2, 3],
            vec![7, 2, 3], // Anchor, Force, Geometric
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
        let sorted = vec![1, 2, 3]; // Anchor, Force, Geometric

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

        // Should include Force(2) and Geometric(3), not Anchor(1)
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
        // Only include path for node 2, not node 3
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

        let (points, sections) = build_sections(&section_nodes, &paths, &doc, 0);

        assert_eq!(points.len(), 5); // 2 + 3 points
        assert_eq!(sections.len(), 2);

        // First section: indices 0-1
        assert_eq!(sections[0].start_index, 0);
        assert_eq!(sections[0].end_index, 1);
        assert_eq!(sections[0].length(), 2);

        // Second section: indices 2-4
        assert_eq!(sections[1].start_index, 2);
        assert_eq!(sections[1].end_index, 4);
        assert_eq!(sections[1].length(), 3);
    }

    #[test]
    fn build_sections_invalid_for_short_paths() {
        let graph = make_test_graph();

        let mut paths = HashMap::new();
        // Path with only 1 point
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

        let (points, sections) = build_sections(&section_nodes, &paths, &doc, 0);

        assert_eq!(points.len(), 0);
        assert_eq!(sections.len(), 1);
        assert!(!sections[0].is_valid());
    }

    #[test]
    fn build_traversal_order_sorts_by_priority() {
        let graph = make_test_graph();
        let section_nodes = vec![2, 3];

        // Create sections (rendered status not checked, only priority)
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

        // Node 2 has priority 5, node 3 has priority 10
        // Priority is stored in scalars (floats), not flags
        let mut scalars = HashMap::new();
        scalars.insert(
            crate::track::document::input_key(2, node_meta::PRIORITY),
            5.0,
        );
        scalars.insert(
            crate::track::document::input_key(3, node_meta::PRIORITY),
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

        // Higher priority (10) should come first
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

        // Node 2 has priority 5, node 3 has priority -1 (cosmetic)
        // Priority is stored in scalars (floats), not flags
        let mut scalars = HashMap::new();
        scalars.insert(
            crate::track::document::input_key(2, node_meta::PRIORITY),
            5.0,
        );
        scalars.insert(
            crate::track::document::input_key(3, node_meta::PRIORITY),
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

        // Only section 0 has positive priority
        assert_eq!(order, vec![0]);
    }

    #[test]
    fn compute_spatial_continuations_finds_same_direction() {
        let points = vec![
            // Section 0: starts at origin, goes forward
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            // Section 1: starts where section 0 ends, same direction
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        let traversal_order = vec![0, 1];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        // Section 0's next should link to section 1's start
        assert!(sections[0].next.is_valid());
        assert_eq!(sections[0].next.index, 1);
        assert!(sections[0].next.at_start());
        assert!(!sections[0].next.flip());

        // Section 1's prev should link to section 0's end
        assert!(sections[1].prev.is_valid());
        assert_eq!(sections[1].prev.index, 0);
        assert!(!sections[1].prev.at_start());
        assert!(!sections[1].prev.flip());
    }

    #[test]
    fn compute_spatial_continuations_finds_opposite_direction() {
        let points = vec![
            // Section 0: starts at origin, goes forward
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            // Section 1: ends where section 0 ends, opposite direction
            make_test_point(0.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, -1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, -1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        let traversal_order = vec![0, 1];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        // Section 0's next should link to section 1's end with flip
        assert!(sections[0].next.is_valid());
        assert_eq!(sections[0].next.index, 1);
        assert!(!sections[0].next.at_start());
        assert!(sections[0].next.flip());
    }

    #[test]
    fn compute_spatial_continuations_prefers_cosmetic() {
        let points = vec![
            // Section 0
            make_test_point(0.0, Float3::ZERO, Float3::new(0.0, 0.0, 1.0)),
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            // Section 1 (cosmetic - not in traversal order)
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
            // Section 2 (non-cosmetic - in traversal order)
            make_test_point(1.0, Float3::new(0.0, 0.0, 1.0), Float3::new(0.0, 0.0, 1.0)),
            make_test_point(2.0, Float3::new(0.0, 0.0, 2.0), Float3::new(0.0, 0.0, 1.0)),
        ];

        let mut sections = vec![
            Section {
                start_index: 0,
                end_index: 1,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 2,
                end_index: 3,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
            Section {
                start_index: 4,
                end_index: 5,
                flags: Section::FLAG_RENDERED,
                next: SectionLink::NONE,
                prev: SectionLink::NONE,
                ..Section::invalid()
            },
        ];

        // Only sections 0 and 2 are in traversal order (non-cosmetic)
        let traversal_order = vec![0, 2];
        compute_spatial_continuations(&points, &mut sections, &traversal_order);

        // Section 0's next should link to section 1 (cosmetic) for overhang support
        // even though section 2 is equally close
        assert_eq!(sections[0].next.index, 1);
    }
}
