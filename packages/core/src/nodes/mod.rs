//! Node type implementations for FVD track sections.
//!
//! Each node type takes an anchor point and parameters, producing a path (Vec<Point>).

mod schema;

pub mod anchor;
pub mod bridge;
pub mod copy_path;
pub mod curved;
pub mod force;
pub mod forces;
pub mod geometric;
pub mod reverse;
pub mod reverse_path;

pub use schema::{
    DurationType, IterationConfig, NodeSchema, NodeType, PortId, PropertyId, PropertyIndex,
};
