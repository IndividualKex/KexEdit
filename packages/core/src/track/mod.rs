//! High-level track evaluation, sections, and splines.
//!
//! This module provides the pipeline for evaluating a node graph into
//! track sections and arc-length parameterized splines.

mod dispatch;
mod document;
mod evaluate;
mod result;
mod section;
mod spline;

pub use dispatch::{
    anchor_ports, bridge_ports, copy_path_ports, curved_ports, force_ports, geometric_ports,
    node_meta, reverse_path_ports, reverse_ports,
};
pub use document::{input_key, keyframe_key, DocumentView};
pub use evaluate::evaluate_graph;
pub use result::EvaluationResult;
pub use section::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    compute_spatial_continuations, Section, SectionLink,
};
pub use spline::{interpolate_physics, resample, to_spline_point, SplinePoint};
