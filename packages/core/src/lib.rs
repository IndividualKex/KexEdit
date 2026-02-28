//! KexEngine - Force Vector Design physics simulation for roller coaster track generation.
//!
//! # Architecture
//!
//! Layered modules with strict inward-only dependencies:
//!
//! - **sim**: Physics/math primitives (Float3, Frame, Point, Keyframe)
//! - **graph**: DAG structure and traversal
//! - **nodes**: Node type implementations
//! - **track**: Track evaluation, sections, splines
//! - **ffi**: C FFI bindings
//!
//! # Usage
//!
//! ```ignore
//! use kexengine::{sim::Point, graph::Graph};
//! ```
//!
//! For C/C#/Unity, link the cdylib and use `kex_*` FFI functions.

pub mod graph;
pub mod nodes;
pub mod persistence;
pub mod sim;
pub mod track;

#[cfg(feature = "ffi")]
pub mod ffi;

// Re-export commonly used types at crate root
pub use graph::Graph;
pub use sim::{Float3, Frame, Keyframe, Point, Quaternion};
