#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PortId {
    Anchor = 0,
    Path = 1,
    Duration = 2,
    Radius = 3,
    Arc = 4,
    Axis = 5,
    LeadIn = 6,
    LeadOut = 7,
    InWeight = 8,
    OutWeight = 9,
    Start = 10,
    End = 11,
    Position = 12,
    Rotation = 13,
}

impl PortId {
    pub const fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(PortId::Anchor),
            1 => Some(PortId::Path),
            2 => Some(PortId::Duration),
            3 => Some(PortId::Radius),
            4 => Some(PortId::Arc),
            5 => Some(PortId::Axis),
            6 => Some(PortId::LeadIn),
            7 => Some(PortId::LeadOut),
            8 => Some(PortId::InWeight),
            9 => Some(PortId::OutWeight),
            10 => Some(PortId::Start),
            11 => Some(PortId::End),
            12 => Some(PortId::Position),
            13 => Some(PortId::Rotation),
            _ => None,
        }
    }
}

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
    TrackStyle = 9,
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
            9 => Some(PropertyId::TrackStyle),
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
    const COUNT: usize = 8;

    const fn as_index(self) -> usize {
        self as usize
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

const INVALID_PORT: u8 = 255;
const INVALID_PROPERTY: u8 = 255;

const INPUT_COUNTS: [usize; NodeType::COUNT] = [2, 2, 6, 4, 3, 2, 1, 1];

const INPUT_PORTS: [[u8; 6]; NodeType::COUNT] = [
    [
        PortId::Anchor as u8,
        PortId::Duration as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Anchor as u8,
        PortId::Duration as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Anchor as u8,
        PortId::Radius as u8,
        PortId::Arc as u8,
        PortId::Axis as u8,
        PortId::LeadIn as u8,
        PortId::LeadOut as u8,
    ],
    [
        PortId::Anchor as u8,
        PortId::Path as u8,
        PortId::Start as u8,
        PortId::End as u8,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Anchor as u8,
        PortId::InWeight as u8,
        PortId::OutWeight as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Position as u8,
        PortId::Rotation as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Anchor as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
    [
        PortId::Path as u8,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
        INVALID_PORT,
    ],
];

const OUTPUT_COUNTS: [usize; NodeType::COUNT] = [2, 2, 2, 2, 2, 1, 1, 1];

const OUTPUT_PORTS: [[u8; 2]; NodeType::COUNT] = [
    [PortId::Anchor as u8, PortId::Path as u8],
    [PortId::Anchor as u8, PortId::Path as u8],
    [PortId::Anchor as u8, PortId::Path as u8],
    [PortId::Anchor as u8, PortId::Path as u8],
    [PortId::Anchor as u8, PortId::Path as u8],
    [PortId::Anchor as u8, INVALID_PORT],
    [PortId::Anchor as u8, INVALID_PORT],
    [PortId::Path as u8, INVALID_PORT],
];

const PROPERTY_COUNTS: [usize; NodeType::COUNT] = [7, 7, 5, 4, 5, 0, 0, 0];

const PROPERTIES: [[u8; 7]; NodeType::COUNT] = [
    [
        PropertyId::RollSpeed as u8,
        PropertyId::NormalForce as u8,
        PropertyId::LateralForce as u8,
        PropertyId::DrivenVelocity as u8,
        PropertyId::HeartOffset as u8,
        PropertyId::Friction as u8,
        PropertyId::Resistance as u8,
    ],
    [
        PropertyId::RollSpeed as u8,
        PropertyId::PitchSpeed as u8,
        PropertyId::YawSpeed as u8,
        PropertyId::DrivenVelocity as u8,
        PropertyId::HeartOffset as u8,
        PropertyId::Friction as u8,
        PropertyId::Resistance as u8,
    ],
    [
        PropertyId::RollSpeed as u8,
        PropertyId::DrivenVelocity as u8,
        PropertyId::HeartOffset as u8,
        PropertyId::Friction as u8,
        PropertyId::Resistance as u8,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
    [
        PropertyId::DrivenVelocity as u8,
        PropertyId::HeartOffset as u8,
        PropertyId::Friction as u8,
        PropertyId::Resistance as u8,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
    [
        PropertyId::DrivenVelocity as u8,
        PropertyId::HeartOffset as u8,
        PropertyId::Friction as u8,
        PropertyId::Resistance as u8,
        PropertyId::TrackStyle as u8,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
    [
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
    [
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
    [
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
        INVALID_PROPERTY,
    ],
];

pub struct NodeSchema;

impl NodeSchema {
    pub const fn input_count(node_type: NodeType) -> usize {
        INPUT_COUNTS[node_type.as_index()]
    }

    pub const fn input(node_type: NodeType, index: usize) -> Option<PortId> {
        if index >= 6 {
            return None;
        }
        let port_u8 = INPUT_PORTS[node_type.as_index()][index];
        if port_u8 == INVALID_PORT {
            None
        } else {
            PortId::from_u8(port_u8)
        }
    }

    pub const fn output_count(node_type: NodeType) -> usize {
        OUTPUT_COUNTS[node_type.as_index()]
    }

    pub const fn output(node_type: NodeType, index: usize) -> Option<PortId> {
        if index >= 2 {
            return None;
        }
        let port_u8 = OUTPUT_PORTS[node_type.as_index()][index];
        if port_u8 == INVALID_PORT {
            None
        } else {
            PortId::from_u8(port_u8)
        }
    }

    pub const fn property_count(node_type: NodeType) -> usize {
        PROPERTY_COUNTS[node_type.as_index()]
    }

    pub const fn property(node_type: NodeType, index: usize) -> Option<PropertyId> {
        if index >= 7 {
            return None;
        }
        let property_u8 = PROPERTIES[node_type.as_index()][index];
        if property_u8 == INVALID_PROPERTY {
            None
        } else {
            PropertyId::from_u8(property_u8)
        }
    }
}

pub struct PropertyIndex;

impl PropertyIndex {
    pub const fn to_index(property: PropertyId, node: NodeType) -> i32 {
        let count = NodeSchema::property_count(node);
        let mut i = 0;
        while i < count {
            if let Some(prop) = NodeSchema::property(node, i) {
                if prop as u8 == property as u8 {
                    return i as i32;
                }
            }
            i += 1;
        }
        -1
    }

    pub const fn from_index(index: i32, node: NodeType) -> Option<PropertyId> {
        if index < 0 || index as usize >= NodeSchema::property_count(node) {
            None
        } else {
            NodeSchema::property(node, index as usize)
        }
    }
}
