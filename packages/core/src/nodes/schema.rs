//! Node-type identity, property identity, per-node metadata slots, and shared
//! iteration config. Per-node port indices live in each `nodes/<type>.rs` file.

#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PropertyId {
    RollSpeed = 0,
    NormalForce = 1,
    LateralForce = 2,
    PitchSpeed = 3,
    YawSpeed = 4,
    DrivenVelocity = 5,
    HeartOffset = 6,
    Friction = 7,
    Resistance = 8,
}

impl PropertyId {
    pub const fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(PropertyId::RollSpeed),
            1 => Some(PropertyId::NormalForce),
            2 => Some(PropertyId::LateralForce),
            3 => Some(PropertyId::PitchSpeed),
            4 => Some(PropertyId::YawSpeed),
            5 => Some(PropertyId::DrivenVelocity),
            6 => Some(PropertyId::HeartOffset),
            7 => Some(PropertyId::Friction),
            8 => Some(PropertyId::Resistance),
            _ => None,
        }
    }
}

#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum NodeType {
    Force = 0,
    Geometric = 1,
    Curved = 2,
    CopyPath = 3,
    Bridge = 4,
    Anchor = 5,
    Reverse = 6,
    ReversePath = 7,
}

impl NodeType {
    pub const fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(NodeType::Force),
            1 => Some(NodeType::Geometric),
            2 => Some(NodeType::Curved),
            3 => Some(NodeType::CopyPath),
            4 => Some(NodeType::Bridge),
            5 => Some(NodeType::Anchor),
            6 => Some(NodeType::Reverse),
            7 => Some(NodeType::ReversePath),
            _ => None,
        }
    }
}

/// Per-node metadata slot keys.
///
/// Stored in the same `(node_id << 8) | slot` map as port inputs, with values
/// in the high range so they don't collide with port-index slots.
#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum NodeMeta {
    Duration = 240,
    DurationType = 241,
    Driven = 242,
    Steering = 243,
    Priority = 244,
    Facing = 245,
    Render = 246,
}

impl NodeMeta {
    pub const fn as_u8(self) -> u8 {
        self as u8
    }
}

#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum DurationType {
    Time = 0,
    Distance = 1,
}

#[derive(Debug, Copy, Clone)]
pub struct IterationConfig {
    pub duration: f32,
    pub duration_type: DurationType,
}

impl IterationConfig {
    pub const fn new(duration: f32, duration_type: DurationType) -> Self {
        Self {
            duration,
            duration_type,
        }
    }
}
