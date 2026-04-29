use crate::nodes::{
    anchor, bridge, copy_path, curved, force, geometric, reverse, reverse_path, DurationType,
    IterationConfig, NodeMeta, NodeType, PropertyId,
};
use crate::sim::{Float3, Keyframe, Point};

use super::document::DocumentView;
use super::result::EvaluationResult;

/// Default physics constants
pub const DEFAULT_VELOCITY: f32 = 10.0;
pub const DEFAULT_HEART_OFFSET: f32 = 1.1;
pub const DEFAULT_FRICTION: f32 = 0.021;
pub const DEFAULT_RESISTANCE: f32 = 2e-5;

/// Evaluates a single node and stores results in the EvaluationResult.
pub fn evaluate_node(
    doc: &DocumentView,
    node_id: u32,
    node_type: NodeType,
    result: &mut EvaluationResult,
) {
    match node_type {
        NodeType::Anchor => evaluate_anchor(doc, node_id, result),
        NodeType::Force => evaluate_force(doc, node_id, result),
        NodeType::Geometric => evaluate_geometric(doc, node_id, result),
        NodeType::Curved => evaluate_curved(doc, node_id, result),
        NodeType::Bridge => evaluate_bridge(doc, node_id, result),
        NodeType::CopyPath => evaluate_copy_path(doc, node_id, result),
        NodeType::Reverse => evaluate_reverse(doc, node_id, result),
        NodeType::ReversePath => evaluate_reverse_path(doc, node_id, result),
    }
}

// ---------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------

fn try_get_anchor(
    doc: &DocumentView,
    result: &EvaluationResult,
    node_id: u32,
    input_index: usize,
) -> Option<Point> {
    let port_id = doc.graph.try_get_input(node_id, input_index)?;
    let &ei = doc.graph.incoming_edge_indices_to_port(port_id).first()?;
    let source_port = doc.graph.edge_sources[ei];
    let port_idx = doc.graph.get_port_index(source_port)?;
    let source_node = doc.graph.port_owners[port_idx];
    result.anchors.get(&source_node).copied()
}

fn try_get_path<'a>(
    doc: &DocumentView,
    result: &'a EvaluationResult,
    node_id: u32,
    input_index: usize,
) -> Option<&'a Vec<Point>> {
    let port_id = doc.graph.try_get_input(node_id, input_index)?;
    let &ei = doc.graph.incoming_edge_indices_to_port(port_id).first()?;
    let source_port = doc.graph.edge_sources[ei];
    let port_idx = doc.graph.get_port_index(source_port)?;
    let source_node = doc.graph.port_owners[port_idx];
    result.paths.get(&source_node)
}

/// Iteration config (duration + duration type) read from common metadata slots.
fn read_iteration(doc: &DocumentView, node_id: u32) -> IterationConfig {
    let duration = doc.get_meta_scalar(node_id, NodeMeta::Duration, 1.0);
    let duration_type = if doc.get_meta_flag(node_id, NodeMeta::DurationType) == 1 {
        DurationType::Distance
    } else {
        DurationType::Time
    };
    IterationConfig::new(duration, duration_type)
}

fn driven(doc: &DocumentView, node_id: u32) -> bool {
    doc.get_meta_flag(node_id, NodeMeta::Driven) == 1
}

fn keyframes<'a>(doc: &'a DocumentView, node_id: u32, prop: PropertyId) -> &'a [Keyframe] {
    doc.get_keyframes(node_id, prop as u8)
}

/// Insert path output and the trailing anchor (if any) into the result.
fn finalize(node_id: u32, path: Vec<Point>, result: &mut EvaluationResult) {
    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

// ---------------------------------------------------------------------------
// Per-node evaluators
// ---------------------------------------------------------------------------

fn evaluate_anchor(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let position = doc.get_vector(node_id, anchor::ports::POSITION, Float3::ZERO);
    let roll = doc.get_scalar(node_id, anchor::ports::ROLL, 0.0);
    let pitch = doc.get_scalar(node_id, anchor::ports::PITCH, 0.0);
    let yaw = doc.get_scalar(node_id, anchor::ports::YAW, 0.0);
    let velocity = doc.get_scalar(node_id, anchor::ports::VELOCITY, DEFAULT_VELOCITY);
    let heart = doc.get_scalar(node_id, anchor::ports::HEART, DEFAULT_HEART_OFFSET);
    let friction = doc.get_scalar(node_id, anchor::ports::FRICTION, DEFAULT_FRICTION);
    let resistance = doc.get_scalar(node_id, anchor::ports::RESISTANCE, DEFAULT_RESISTANCE);

    let anchor = anchor::build(
        position, pitch, yaw, roll, velocity, heart, friction, resistance,
    );
    result.anchors.insert(node_id, anchor);
}

fn evaluate_force(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, force::ports::ANCHOR as usize) else {
        return;
    };

    let path = force::build(
        &input,
        &read_iteration(doc, node_id),
        driven(doc, node_id),
        keyframes(doc, node_id, PropertyId::RollSpeed),
        keyframes(doc, node_id, PropertyId::NormalForce),
        keyframes(doc, node_id, PropertyId::LateralForce),
        keyframes(doc, node_id, PropertyId::DrivenVelocity),
        keyframes(doc, node_id, PropertyId::HeartOffset),
        keyframes(doc, node_id, PropertyId::Friction),
        keyframes(doc, node_id, PropertyId::Resistance),
        input.heart_offset,
        input.friction,
        input.resistance,
    );
    finalize(node_id, path, result);
}

fn evaluate_geometric(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, geometric::ports::ANCHOR as usize)
    else {
        return;
    };
    let steering = doc.get_meta_flag(node_id, NodeMeta::Steering) == 1;

    let path = geometric::build(
        &input,
        &read_iteration(doc, node_id),
        driven(doc, node_id),
        steering,
        keyframes(doc, node_id, PropertyId::RollSpeed),
        keyframes(doc, node_id, PropertyId::PitchSpeed),
        keyframes(doc, node_id, PropertyId::YawSpeed),
        keyframes(doc, node_id, PropertyId::DrivenVelocity),
        keyframes(doc, node_id, PropertyId::HeartOffset),
        keyframes(doc, node_id, PropertyId::Friction),
        keyframes(doc, node_id, PropertyId::Resistance),
        input.heart_offset,
        input.friction,
        input.resistance,
    );
    finalize(node_id, path, result);
}

fn evaluate_curved(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, curved::ports::ANCHOR as usize) else {
        return;
    };

    let path = curved::CurvedNode::build(
        &input,
        doc.get_scalar(node_id, curved::ports::RADIUS, 10.0),
        doc.get_scalar(node_id, curved::ports::ARC, 90.0),
        doc.get_scalar(node_id, curved::ports::AXIS, 0.0),
        doc.get_scalar(node_id, curved::ports::LEAD_IN, 0.0),
        doc.get_scalar(node_id, curved::ports::LEAD_OUT, 0.0),
        driven(doc, node_id),
        keyframes(doc, node_id, PropertyId::RollSpeed),
        keyframes(doc, node_id, PropertyId::DrivenVelocity),
        keyframes(doc, node_id, PropertyId::HeartOffset),
        keyframes(doc, node_id, PropertyId::Friction),
        keyframes(doc, node_id, PropertyId::Resistance),
        input.heart_offset,
        input.friction,
        input.resistance,
    );
    finalize(node_id, path, result);
}

fn evaluate_bridge(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, bridge::ports::ANCHOR as usize) else {
        return;
    };
    let Some(target) = try_get_anchor(doc, result, node_id, bridge::ports::TARGET as usize) else {
        return;
    };

    let path = bridge::BridgeNode::build(
        &input,
        &target,
        doc.get_scalar(node_id, bridge::ports::IN_WEIGHT, 0.5),
        doc.get_scalar(node_id, bridge::ports::OUT_WEIGHT, 0.5),
        driven(doc, node_id),
        keyframes(doc, node_id, PropertyId::DrivenVelocity),
        keyframes(doc, node_id, PropertyId::HeartOffset),
        keyframes(doc, node_id, PropertyId::Friction),
        keyframes(doc, node_id, PropertyId::Resistance),
        input.heart_offset,
        input.friction,
        input.resistance,
    );
    finalize(node_id, path, result);
}

fn evaluate_copy_path(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, copy_path::ports::ANCHOR as usize)
    else {
        return;
    };
    let Some(source) = try_get_path(doc, result, node_id, copy_path::ports::PATH as usize) else {
        return;
    };

    let path = copy_path::CopyPathNode::build(
        &input,
        source,
        doc.get_scalar(node_id, copy_path::ports::START, -1.0),
        doc.get_scalar(node_id, copy_path::ports::END, -1.0),
        driven(doc, node_id),
        keyframes(doc, node_id, PropertyId::DrivenVelocity),
        keyframes(doc, node_id, PropertyId::HeartOffset),
        keyframes(doc, node_id, PropertyId::Friction),
        keyframes(doc, node_id, PropertyId::Resistance),
        input.heart_offset,
        input.friction,
        input.resistance,
    );
    finalize(node_id, path, result);
}

fn evaluate_reverse(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input) = try_get_anchor(doc, result, node_id, reverse::ports::ANCHOR as usize) else {
        return;
    };
    result.anchors.insert(node_id, reverse::build(&input));
}

fn evaluate_reverse_path(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(source) = try_get_path(doc, result, node_id, reverse_path::ports::PATH as usize)
    else {
        return;
    };
    finalize(node_id, reverse_path::build(source), result);
}
