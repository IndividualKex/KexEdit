//! Pure physics/math primitives for FVD simulation.
//!
//! This module contains zero-dependency core types and physics calculations.

mod curvature;
mod forces;
mod frame;
mod frame_change;
mod keyframe;
mod math;
mod physics_params;
mod point;

// Physics constants module - re-exported for backwards compatibility
pub mod physics;

pub use curvature::Curvature;
pub use forces::Forces;
pub use frame::Frame;
pub use frame_change::FrameChange;
pub use keyframe::{evaluate, evaluate_segment, InterpolationType, Keyframe};
pub use math::{Float3, Matrix3, Quaternion};
pub use physics::{
    update_velocity, wrap_angle, DT, EPSILON, G, HZ, MAX_FORCE, MAX_VELOCITY, MIN_VELOCITY,
};
pub use physics_params::PhysicsParams;
pub use point::Point;
