//! High-level track evaluation, sections, and splines.
//!
//! Pipeline: node graph → evaluated paths → sections → arc-length splines.

mod dispatch;
mod document;
mod evaluate;
mod result;
mod section;
mod spatial;
mod spline;

pub use document::{input_key, keyframe_key, DocumentView};
pub use evaluate::evaluate_graph;
pub use result::EvaluationResult;
pub use section::{
    build_sections, build_traversal_order, collect_sections, compute_continuations, Section,
    SectionLink,
};
pub use spatial::compute_spatial_continuations;
pub use spline::{interpolate_physics, resample, to_spline_point, SplinePoint};
