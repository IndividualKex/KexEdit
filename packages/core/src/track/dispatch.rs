use crate::nodes::{DurationType, IterationConfig, NodeMeta, NodeType, PropertyId};
use crate::sim::{Float3, Point};

use super::document::DocumentView;
use super::result::EvaluationResult;

// Per-node-type input port indices (ordinals within the node's input port list).

pub mod anchor_ports {
    pub const POSITION: u8 = 0;
    pub const ROLL: u8 = 1;
    pub const PITCH: u8 = 2;
    pub const YAW: u8 = 3;
    pub const VELOCITY: u8 = 4;
    pub const HEART: u8 = 5;
    pub const FRICTION: u8 = 6;
    pub const RESISTANCE: u8 = 7;
}

pub mod force_ports {
    pub const ANCHOR: u8 = 0;
}

pub mod geometric_ports {
    pub const ANCHOR: u8 = 0;
}

pub mod curved_ports {
    pub const ANCHOR: u8 = 0;
    pub const RADIUS: u8 = 1;
    pub const ARC: u8 = 2;
    pub const AXIS: u8 = 3;
    pub const LEAD_IN: u8 = 4;
    pub const LEAD_OUT: u8 = 5;
}

pub mod bridge_ports {
    pub const ANCHOR: u8 = 0;
    pub const TARGET: u8 = 1;
    pub const OUT_WEIGHT: u8 = 2;
    pub const IN_WEIGHT: u8 = 3;
}

pub mod copy_path_ports {
    pub const ANCHOR: u8 = 0;
    pub const PATH: u8 = 1;
    pub const START: u8 = 2;
    pub const END: u8 = 3;
}

pub mod reverse_ports {
    pub const ANCHOR: u8 = 0;
}

pub mod reverse_path_ports {
    pub const PATH: u8 = 0;
}

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

/// Helper to get input anchor from connected predecessor node by port index.
fn try_get_anchor(
    doc: &DocumentView,
    result: &EvaluationResult,
    node_id: u32,
    input_index: usize,
) -> Option<Point> {
    let port_id = doc.graph.try_get_input(node_id, input_index)?;
    get_anchor_from_port(doc, result, port_id)
}

fn get_anchor_from_port(
    doc: &DocumentView,
    result: &EvaluationResult,
    port_id: u32,
) -> Option<Point> {
    for i in 0..doc.graph.edge_ids.len() {
        if doc.graph.edge_targets[i] != port_id {
            continue;
        }
        let source_port = doc.graph.edge_sources[i];
        let port_idx = doc.graph.get_port_index(source_port)?;
        let source_node = doc.graph.port_owners[port_idx];
        return result.anchors.get(&source_node).copied();
    }
    None
}

fn try_get_path<'a>(
    doc: &DocumentView,
    result: &'a EvaluationResult,
    node_id: u32,
    input_index: usize,
) -> Option<&'a Vec<Point>> {
    let port_id = doc.graph.try_get_input(node_id, input_index)?;

    for i in 0..doc.graph.edge_ids.len() {
        if doc.graph.edge_targets[i] != port_id {
            continue;
        }
        let source_port = doc.graph.edge_sources[i];
        let port_idx = doc.graph.get_port_index(source_port)?;
        let source_node = doc.graph.port_owners[port_idx];
        return result.paths.get(&source_node);
    }
    None
}

fn evaluate_anchor(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let position = doc.get_vector(node_id, anchor_ports::POSITION, Float3::ZERO);
    let roll = doc.get_scalar(node_id, anchor_ports::ROLL, 0.0);
    let pitch = doc.get_scalar(node_id, anchor_ports::PITCH, 0.0);
    let yaw = doc.get_scalar(node_id, anchor_ports::YAW, 0.0);
    let velocity = doc.get_scalar(node_id, anchor_ports::VELOCITY, DEFAULT_VELOCITY);
    let heart = doc.get_scalar(node_id, anchor_ports::HEART, DEFAULT_HEART_OFFSET);
    let friction = doc.get_scalar(node_id, anchor_ports::FRICTION, DEFAULT_FRICTION);
    let resistance = doc.get_scalar(node_id, anchor_ports::RESISTANCE, DEFAULT_RESISTANCE);

    let anchor = crate::nodes::anchor::build(
        position, pitch, yaw, roll, velocity, heart, friction, resistance,
    );

    result.anchors.insert(node_id, anchor);
}

fn evaluate_force(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, force_ports::ANCHOR as usize)
    else {
        return;
    };

    let duration = doc.get_meta_scalar(node_id, NodeMeta::Duration, 1.0);
    let duration_type = if doc.get_meta_flag(node_id, NodeMeta::DurationType) == 1 {
        DurationType::Distance
    } else {
        DurationType::Time
    };
    let driven = doc.get_meta_flag(node_id, NodeMeta::Driven) == 1;

    let roll_speed = doc.get_keyframes(node_id, PropertyId::RollSpeed as u8);
    let normal_force = doc.get_keyframes(node_id, PropertyId::NormalForce as u8);
    let lateral_force = doc.get_keyframes(node_id, PropertyId::LateralForce as u8);
    let driven_velocity = doc.get_keyframes(node_id, PropertyId::DrivenVelocity as u8);
    let heart_offset = doc.get_keyframes(node_id, PropertyId::HeartOffset as u8);
    let friction = doc.get_keyframes(node_id, PropertyId::Friction as u8);
    let resistance = doc.get_keyframes(node_id, PropertyId::Resistance as u8);

    let config = IterationConfig::new(duration, duration_type);
    let path = crate::nodes::force::build(
        &input_anchor,
        &config,
        driven,
        roll_speed,
        normal_force,
        lateral_force,
        driven_velocity,
        heart_offset,
        friction,
        resistance,
        input_anchor.heart_offset,
        input_anchor.friction,
        input_anchor.resistance,
    );

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

fn evaluate_geometric(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, geometric_ports::ANCHOR as usize)
    else {
        return;
    };

    let duration = doc.get_meta_scalar(node_id, NodeMeta::Duration, 1.0);
    let duration_type = if doc.get_meta_flag(node_id, NodeMeta::DurationType) == 1 {
        DurationType::Distance
    } else {
        DurationType::Time
    };
    let driven = doc.get_meta_flag(node_id, NodeMeta::Driven) == 1;
    let steering = doc.get_meta_flag(node_id, NodeMeta::Steering) == 1;

    let roll_speed = doc.get_keyframes(node_id, PropertyId::RollSpeed as u8);
    let pitch_speed = doc.get_keyframes(node_id, PropertyId::PitchSpeed as u8);
    let yaw_speed = doc.get_keyframes(node_id, PropertyId::YawSpeed as u8);
    let driven_velocity = doc.get_keyframes(node_id, PropertyId::DrivenVelocity as u8);
    let heart_offset = doc.get_keyframes(node_id, PropertyId::HeartOffset as u8);
    let friction = doc.get_keyframes(node_id, PropertyId::Friction as u8);
    let resistance = doc.get_keyframes(node_id, PropertyId::Resistance as u8);

    let config = IterationConfig::new(duration, duration_type);
    let path = crate::nodes::geometric::build(
        &input_anchor,
        &config,
        driven,
        steering,
        roll_speed,
        pitch_speed,
        yaw_speed,
        driven_velocity,
        heart_offset,
        friction,
        resistance,
        input_anchor.heart_offset,
        input_anchor.friction,
        input_anchor.resistance,
    );

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

fn evaluate_curved(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, curved_ports::ANCHOR as usize)
    else {
        return;
    };

    let radius = doc.get_scalar(node_id, curved_ports::RADIUS, 10.0);
    let arc = doc.get_scalar(node_id, curved_ports::ARC, 90.0);
    let axis = doc.get_scalar(node_id, curved_ports::AXIS, 0.0);
    let lead_in = doc.get_scalar(node_id, curved_ports::LEAD_IN, 0.0);
    let lead_out = doc.get_scalar(node_id, curved_ports::LEAD_OUT, 0.0);
    let driven = doc.get_meta_flag(node_id, NodeMeta::Driven) == 1;

    let roll_speed = doc.get_keyframes(node_id, PropertyId::RollSpeed as u8);
    let driven_velocity = doc.get_keyframes(node_id, PropertyId::DrivenVelocity as u8);
    let heart_offset = doc.get_keyframes(node_id, PropertyId::HeartOffset as u8);
    let friction = doc.get_keyframes(node_id, PropertyId::Friction as u8);
    let resistance = doc.get_keyframes(node_id, PropertyId::Resistance as u8);

    let path = crate::nodes::curved::CurvedNode::build(
        &input_anchor,
        radius,
        arc,
        axis,
        lead_in,
        lead_out,
        driven,
        roll_speed,
        driven_velocity,
        heart_offset,
        friction,
        resistance,
        input_anchor.heart_offset,
        input_anchor.friction,
        input_anchor.resistance,
    );

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

fn evaluate_bridge(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, bridge_ports::ANCHOR as usize)
    else {
        return;
    };

    let Some(target_anchor) = try_get_anchor(doc, result, node_id, bridge_ports::TARGET as usize)
    else {
        return;
    };

    let in_weight = doc.get_scalar(node_id, bridge_ports::IN_WEIGHT, 0.5);
    let out_weight = doc.get_scalar(node_id, bridge_ports::OUT_WEIGHT, 0.5);
    let driven = doc.get_meta_flag(node_id, NodeMeta::Driven) == 1;

    let driven_velocity = doc.get_keyframes(node_id, PropertyId::DrivenVelocity as u8);
    let heart_offset = doc.get_keyframes(node_id, PropertyId::HeartOffset as u8);
    let friction = doc.get_keyframes(node_id, PropertyId::Friction as u8);
    let resistance = doc.get_keyframes(node_id, PropertyId::Resistance as u8);

    let path = crate::nodes::bridge::BridgeNode::build(
        &input_anchor,
        &target_anchor,
        in_weight,
        out_weight,
        driven,
        driven_velocity,
        heart_offset,
        friction,
        resistance,
        input_anchor.heart_offset,
        input_anchor.friction,
        input_anchor.resistance,
    );

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

fn evaluate_copy_path(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, copy_path_ports::ANCHOR as usize)
    else {
        return;
    };

    let Some(source_path) = try_get_path(doc, result, node_id, copy_path_ports::PATH as usize)
    else {
        return;
    };

    let start = doc.get_scalar(node_id, copy_path_ports::START, -1.0);
    let end = doc.get_scalar(node_id, copy_path_ports::END, -1.0);
    let driven = doc.get_meta_flag(node_id, NodeMeta::Driven) == 1;

    let driven_velocity = doc.get_keyframes(node_id, PropertyId::DrivenVelocity as u8);
    let heart_offset = doc.get_keyframes(node_id, PropertyId::HeartOffset as u8);
    let friction = doc.get_keyframes(node_id, PropertyId::Friction as u8);
    let resistance = doc.get_keyframes(node_id, PropertyId::Resistance as u8);

    let path = crate::nodes::copy_path::CopyPathNode::build(
        &input_anchor,
        source_path,
        start,
        end,
        driven,
        driven_velocity,
        heart_offset,
        friction,
        resistance,
        input_anchor.heart_offset,
        input_anchor.friction,
        input_anchor.resistance,
    );

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}

fn evaluate_reverse(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(input_anchor) = try_get_anchor(doc, result, node_id, reverse_ports::ANCHOR as usize)
    else {
        return;
    };

    let reversed = crate::nodes::reverse::build(&input_anchor);
    result.anchors.insert(node_id, reversed);
}

fn evaluate_reverse_path(doc: &DocumentView, node_id: u32, result: &mut EvaluationResult) {
    let Some(source_path) = try_get_path(doc, result, node_id, reverse_path_ports::PATH as usize)
    else {
        return;
    };

    let path = crate::nodes::reverse_path::build(source_path);

    if let Some(last) = path.last() {
        result.anchors.insert(node_id, *last);
    }
    result.paths.insert(node_id, path);
}
