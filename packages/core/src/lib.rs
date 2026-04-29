//! KexEngine — Force Vector Design physics simulation for roller coaster track generation.
//!
//! # Architecture
//!
//! Layered modules with strict inward-only dependencies:
//!
//! - **sim**: physics/math primitives (Float3, Frame, Point, Keyframe)
//! - **graph**: DAG structure and traversal
//! - **nodes**: node type implementations
//! - **track**: track evaluation, sections, splines
//! - **persistence**: `.kex` binary format
//! - **ffi**: C ABI bindings (feature-gated)
//!
//! # Usage
//!
//! ```ignore
//! use kexengine::{sim::Point, graph::Graph};
//! ```
//!
//! For non-Rust consumers (Blender via ctypes, web/WASM), link against the
//! cdylib and call the `kex_*` functions in the `ffi` module.

pub mod graph;
pub mod nodes;
pub mod persistence;
pub mod sim;
pub mod track;

#[cfg(feature = "ffi")]
pub mod ffi;

pub use graph::Graph;
pub use sim::{Float3, Frame, Keyframe, Point, Quaternion};
