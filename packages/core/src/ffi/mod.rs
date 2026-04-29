//! C ABI for kexengine.
//!
//! Functions:
//! - `kex_build` — build track from document data
//! - `kex_save` / `kex_save_size` — serialize document to bytes
//! - `kex_load` / `kex_load_get_counts` / `kex_load_copy_data` / `kex_load_free` —
//!   deserialize bytes into an opaque handle, query sizes, copy data out, free
//!
//! # Error codes
//! - `0`: Success
//! - `-1`: Null pointer
//! - `-3`: Buffer overflow (resize and retry)
//! - `-4`: Cycle detected
//! - `-5`: Invalid format

use crate::graph::Graph;
use crate::persistence;
use crate::sim::{Float3, Keyframe, Point};
use crate::track::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    compute_spatial_continuations, evaluate_graph, interpolate_physics, resample, DocumentView,
    Section, SplinePoint,
};
use std::collections::HashMap;

/// Document data passed across the FFI boundary as parallel arrays.
#[repr(C)]
pub struct KexDocument {
    // Graph - nodes
    pub node_ids: *const u32,
    pub node_count: usize,
    pub node_types: *const u8,
    pub node_input_counts: *const i32,
    pub node_output_counts: *const i32,

    // Graph - ports
    pub port_ids: *const u32,
    pub port_count: usize,
    pub port_types: *const u32,
    pub port_owners: *const u32,
    pub port_is_input: *const u8,

    // Graph - edges
    pub edge_ids: *const u32,
    pub edge_count: usize,
    pub edge_sources: *const u32,
    pub edge_targets: *const u32,

    // Properties - scalars
    pub scalar_keys: *const u64,
    pub scalar_values: *const f32,
    pub scalar_count: usize,

    // Properties - vectors
    pub vector_keys: *const u64,
    pub vector_values: *const Float3,
    pub vector_count: usize,

    // Properties - flags
    pub flag_keys: *const u64,
    pub flag_values: *const i32,
    pub flag_count: usize,

    // Keyframes
    pub keyframes: *const Keyframe,
    pub keyframe_count: usize,
    pub keyframe_range_keys: *const u64,
    pub keyframe_range_starts: *const i32,
    pub keyframe_range_lengths: *const i32,
    pub keyframe_range_count: usize,
}

/// Output buffers for track data. The caller pre-allocates each buffer up to
/// `*_capacity`; `kex_build` writes counts into the `*_count` pointers.
#[repr(C)]
pub struct KexOutput {
    pub points: *mut Point,
    pub points_capacity: usize,

    pub sections: *mut Section,
    pub sections_capacity: usize,
    pub section_node_ids: *mut u32,

    pub traversal_order: *mut i32,
    pub traversal_capacity: usize,

    pub spline_points: *mut SplinePoint,
    pub spline_capacity: usize,
    pub spline_velocities: *mut f32,
    pub spline_normal_forces: *mut f32,
    pub spline_lateral_forces: *mut f32,
    pub spline_roll_speeds: *mut f32,

    pub points_count: *mut usize,
    pub sections_count: *mut usize,
    pub traversal_count: *mut usize,
    pub spline_count: *mut usize,
}

/// Build track from document data.
///
/// # Safety
/// - `doc` and `output` must be valid pointers to initialized structs.
/// - All array pointers in `doc` must be valid for their respective counts.
/// - Output buffer pointers must have capacity ≥ their `*_capacity` fields.
#[no_mangle]
pub unsafe extern "C" fn kex_build(
    doc: *const KexDocument,
    resolution: f32,
    output: *mut KexOutput,
) -> i32 {
    if doc.is_null() || output.is_null() {
        return -1;
    }
    let doc = &*doc;
    let output = &mut *output;

    let port_is_input: Vec<bool> = if doc.port_count > 0 && !doc.port_is_input.is_null() {
        std::slice::from_raw_parts(doc.port_is_input, doc.port_count)
            .iter()
            .map(|&b| b != 0)
            .collect()
    } else {
        Vec::new()
    };

    let graph = if doc.node_count > 0 {
        Graph::from_vecs(
            to_vec(doc.node_ids, doc.node_count),
            to_vec(doc.node_types, doc.node_count),
            to_vec(doc.node_input_counts, doc.node_count),
            to_vec(doc.node_output_counts, doc.node_count),
            to_vec(doc.port_ids, doc.port_count),
            to_vec(doc.port_types, doc.port_count),
            to_vec(doc.port_owners, doc.port_count),
            port_is_input,
            to_vec(doc.edge_ids, doc.edge_count),
            to_vec(doc.edge_sources, doc.edge_count),
            to_vec(doc.edge_targets, doc.edge_count),
        )
    } else {
        Graph::new()
    };

    let scalars = to_map(doc.scalar_keys, doc.scalar_values, doc.scalar_count);
    let vectors = to_map(doc.vector_keys, doc.vector_values, doc.vector_count);
    let flags = to_map(doc.flag_keys, doc.flag_values, doc.flag_count);

    let keyframes: Vec<Keyframe> = to_vec(doc.keyframes, doc.keyframe_count);
    let keyframe_ranges = to_keyframe_ranges(
        doc.keyframe_range_keys,
        doc.keyframe_range_starts,
        doc.keyframe_range_lengths,
        doc.keyframe_range_count,
    );

    let doc_view = DocumentView::new(
        &graph,
        &scalars,
        &vectors,
        &flags,
        &keyframes,
        &keyframe_ranges,
    );

    let eval_result = match evaluate_graph(&doc_view) {
        Some(r) => r,
        None => return -4,
    };

    let sorted = match graph.topological_sort() {
        Some(s) => s,
        None => return -4,
    };

    let (section_nodes, node_to_section) = collect_sections(&sorted, &graph, &eval_result.paths);
    let (points, mut sections) = build_sections(&section_nodes, &eval_result.paths, &doc_view);
    compute_continuations(&section_nodes, &node_to_section, &mut sections, &doc_view);
    let traversal = build_traversal_order(&section_nodes, &sections, &doc_view);
    compute_spatial_continuations(&points, &mut sections, &traversal);

    if points.len() > output.points_capacity
        || sections.len() > output.sections_capacity
        || traversal.len() > output.traversal_capacity
    {
        return -3;
    }

    for (i, point) in points.iter().enumerate() {
        *output.points.add(i) = *point;
    }

    let mut spline_offset = 0usize;
    for section in sections.iter_mut() {
        if !section.is_valid() {
            continue;
        }

        let start = section.start_index as usize;
        let end = section.end_index as usize;
        let path_slice = &points[start..=end];

        let spline = resample(path_slice, resolution);

        if spline_offset + spline.len() > output.spline_capacity {
            return -3;
        }

        section.spline_start_index = spline_offset as i32;
        section.spline_end_index = (spline_offset + spline.len() - 1) as i32;

        for (j, sp) in spline.iter().enumerate() {
            *output.spline_points.add(spline_offset + j) = *sp;

            let (vel, nf, lf, rs) = interpolate_physics(path_slice, sp.arc);
            *output.spline_velocities.add(spline_offset + j) = vel;
            *output.spline_normal_forces.add(spline_offset + j) = nf;
            *output.spline_lateral_forces.add(spline_offset + j) = lf;
            *output.spline_roll_speeds.add(spline_offset + j) = rs;
        }

        spline_offset += spline.len();
    }

    for (i, section) in sections.iter().enumerate() {
        *output.sections.add(i) = *section;
        *output.section_node_ids.add(i) = section_nodes[i];
    }

    for (i, &idx) in traversal.iter().enumerate() {
        *output.traversal_order.add(i) = idx;
    }

    *output.points_count = points.len();
    *output.sections_count = sections.len();
    *output.traversal_count = traversal.len();
    *output.spline_count = spline_offset;

    0
}

unsafe fn to_vec<T: Copy>(ptr: *const T, len: usize) -> Vec<T> {
    if len == 0 || ptr.is_null() {
        Vec::new()
    } else {
        std::slice::from_raw_parts(ptr, len).to_vec()
    }
}

unsafe fn to_map<K: Copy + Eq + std::hash::Hash, V: Copy>(
    keys: *const K,
    values: *const V,
    count: usize,
) -> HashMap<K, V> {
    if count == 0 || keys.is_null() || values.is_null() {
        return HashMap::new();
    }
    std::slice::from_raw_parts(keys, count)
        .iter()
        .copied()
        .zip(std::slice::from_raw_parts(values, count).iter().copied())
        .collect()
}

unsafe fn to_keyframe_ranges(
    keys: *const u64,
    starts: *const i32,
    lengths: *const i32,
    count: usize,
) -> HashMap<u64, (usize, usize)> {
    if count == 0 || keys.is_null() || starts.is_null() || lengths.is_null() {
        return HashMap::new();
    }
    let keys = std::slice::from_raw_parts(keys, count);
    let starts = std::slice::from_raw_parts(starts, count);
    let lengths = std::slice::from_raw_parts(lengths, count);

    (0..count)
        .map(|i| (keys[i], (starts[i] as usize, lengths[i] as usize)))
        .collect()
}

// ============================================================================
// Persistence
// ============================================================================

/// Get the buffer size required to serialize a document.
///
/// # Safety
/// `doc` must be a valid pointer to an initialized `KexDocument`.
#[no_mangle]
pub unsafe extern "C" fn kex_save_size(doc: *const KexDocument) -> i64 {
    if doc.is_null() {
        return -1;
    }
    let doc = &*doc;
    persistence::serialize(&kex_document_to_owned(doc)).len() as i64
}

/// Serialize a document to a byte buffer.
///
/// # Returns
/// `0` on success, `-1` on null pointer, `-3` if buffer is too small (the
/// required size is written to `bytes_written`).
///
/// # Safety
/// All pointers must be valid; `buffer` must point to ≥ `buffer_capacity` bytes.
#[no_mangle]
pub unsafe extern "C" fn kex_save(
    doc: *const KexDocument,
    buffer: *mut u8,
    buffer_capacity: usize,
    bytes_written: *mut usize,
) -> i32 {
    if doc.is_null() || buffer.is_null() || bytes_written.is_null() {
        return -1;
    }
    let doc = &*doc;
    let serialized = persistence::serialize(&kex_document_to_owned(doc));

    if serialized.len() > buffer_capacity {
        *bytes_written = serialized.len();
        return -3;
    }

    std::ptr::copy_nonoverlapping(serialized.as_ptr(), buffer, serialized.len());
    *bytes_written = serialized.len();
    0
}

/// Opaque handle to a loaded document.
pub type KexDocumentHandle = *mut std::ffi::c_void;

/// Load a document from a byte buffer.
///
/// # Returns
/// Non-null handle on success, null on error.
///
/// # Safety
/// `data` must point to a valid buffer of ≥ `data_len` bytes.
#[no_mangle]
pub unsafe extern "C" fn kex_load(data: *const u8, data_len: usize) -> KexDocumentHandle {
    if data.is_null() || data_len == 0 {
        return std::ptr::null_mut();
    }

    let bytes = std::slice::from_raw_parts(data, data_len);
    match persistence::deserialize(bytes) {
        Ok(doc) => Box::into_raw(Box::new(doc)) as KexDocumentHandle,
        Err(_) => std::ptr::null_mut(),
    }
}

/// Free a loaded document handle.
///
/// # Safety
/// `handle` must be a valid handle returned by `kex_load`, or null.
#[no_mangle]
pub unsafe extern "C" fn kex_load_free(handle: KexDocumentHandle) {
    if !handle.is_null() {
        drop(Box::from_raw(handle as *mut persistence::Document));
    }
}

/// Document counts returned by `kex_load_get_counts`.
#[repr(C)]
pub struct KexDocumentCounts {
    pub node_count: i32,
    pub port_count: i32,
    pub edge_count: i32,
    pub scalar_count: i32,
    pub vector_count: i32,
    pub flag_count: i32,
    pub keyframe_count: i32,
    pub keyframe_range_count: i32,
    pub next_node_id: u32,
    pub next_port_id: u32,
    pub next_edge_id: u32,
}

/// Get document counts so the caller can allocate output buffers.
///
/// # Safety
/// `handle` must be valid; `counts` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn kex_load_get_counts(
    handle: KexDocumentHandle,
    counts: *mut KexDocumentCounts,
) -> i32 {
    if handle.is_null() || counts.is_null() {
        return -1;
    }

    let doc = &*(handle as *const persistence::Document);
    let out = &mut *counts;

    out.node_count = doc.graph.node_ids.len() as i32;
    out.port_count = doc.graph.port_ids.len() as i32;
    out.edge_count = doc.graph.edge_ids.len() as i32;
    out.scalar_count = doc.scalars.len() as i32;
    out.vector_count = doc.vectors.len() as i32;
    out.flag_count = doc.flags.len() as i32;
    out.keyframe_count = doc.keyframes.len() as i32;
    out.keyframe_range_count = doc.keyframe_ranges.len() as i32;
    out.next_node_id = doc.next_node_id;
    out.next_port_id = doc.next_port_id;
    out.next_edge_id = doc.next_edge_id;

    0
}

/// Copy loaded document data into pre-allocated buffers.
///
/// # Safety
/// `handle` must be valid; all buffer pointers must have sufficient capacity
/// (use `kex_load_get_counts` first).
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn kex_load_copy_data(
    handle: KexDocumentHandle,
    node_ids: *mut u32,
    node_types: *mut u8,
    node_input_counts: *mut i32,
    node_output_counts: *mut i32,
    port_ids: *mut u32,
    port_types: *mut u32,
    port_owners: *mut u32,
    port_is_input: *mut u8,
    edge_ids: *mut u32,
    edge_sources: *mut u32,
    edge_targets: *mut u32,
    scalar_keys: *mut u64,
    scalar_values: *mut f32,
    vector_keys: *mut u64,
    vector_values: *mut Float3,
    flag_keys: *mut u64,
    flag_values: *mut i32,
    keyframes: *mut Keyframe,
    keyframe_range_keys: *mut u64,
    keyframe_range_starts: *mut i32,
    keyframe_range_lengths: *mut i32,
) -> i32 {
    if handle.is_null() {
        return -1;
    }

    let doc = &*(handle as *const persistence::Document);

    for (i, &id) in doc.graph.node_ids.iter().enumerate() {
        *node_ids.add(i) = id;
        *node_types.add(i) = doc.graph.node_types[i];
        *node_input_counts.add(i) = doc.graph.node_input_count[i];
        *node_output_counts.add(i) = doc.graph.node_output_count[i];
    }

    for (i, &id) in doc.graph.port_ids.iter().enumerate() {
        *port_ids.add(i) = id;
        *port_types.add(i) = doc.graph.port_types[i];
        *port_owners.add(i) = doc.graph.port_owners[i];
        *port_is_input.add(i) = if doc.graph.port_is_input[i] { 1 } else { 0 };
    }

    for (i, &id) in doc.graph.edge_ids.iter().enumerate() {
        *edge_ids.add(i) = id;
        *edge_sources.add(i) = doc.graph.edge_sources[i];
        *edge_targets.add(i) = doc.graph.edge_targets[i];
    }

    for (i, (&key, &value)) in doc.scalars.iter().enumerate() {
        *scalar_keys.add(i) = key;
        *scalar_values.add(i) = value;
    }

    for (i, (&key, &value)) in doc.vectors.iter().enumerate() {
        *vector_keys.add(i) = key;
        *vector_values.add(i) = value;
    }

    for (i, (&key, &value)) in doc.flags.iter().enumerate() {
        *flag_keys.add(i) = key;
        *flag_values.add(i) = value;
    }

    for (i, kf) in doc.keyframes.iter().enumerate() {
        *keyframes.add(i) = *kf;
    }

    for (i, (&key, &(start, length))) in doc.keyframe_ranges.iter().enumerate() {
        *keyframe_range_keys.add(i) = key;
        *keyframe_range_starts.add(i) = start as i32;
        *keyframe_range_lengths.add(i) = length as i32;
    }

    0
}

/// Convert FFI `KexDocument` into an owned `persistence::Document`.
unsafe fn kex_document_to_owned(doc: &KexDocument) -> persistence::Document {
    let port_is_input: Vec<bool> = if doc.port_count > 0 && !doc.port_is_input.is_null() {
        std::slice::from_raw_parts(doc.port_is_input, doc.port_count)
            .iter()
            .map(|&b| b != 0)
            .collect()
    } else {
        Vec::new()
    };

    let graph = if doc.node_count > 0 {
        Graph::from_vecs(
            to_vec(doc.node_ids, doc.node_count),
            to_vec(doc.node_types, doc.node_count),
            to_vec(doc.node_input_counts, doc.node_count),
            to_vec(doc.node_output_counts, doc.node_count),
            to_vec(doc.port_ids, doc.port_count),
            to_vec(doc.port_types, doc.port_count),
            to_vec(doc.port_owners, doc.port_count),
            port_is_input,
            to_vec(doc.edge_ids, doc.edge_count),
            to_vec(doc.edge_sources, doc.edge_count),
            to_vec(doc.edge_targets, doc.edge_count),
        )
    } else {
        Graph::new()
    };

    let scalars = to_map(doc.scalar_keys, doc.scalar_values, doc.scalar_count);
    let vectors = to_map(doc.vector_keys, doc.vector_values, doc.vector_count);
    let flags = to_map(doc.flag_keys, doc.flag_values, doc.flag_count);

    let keyframes: Vec<Keyframe> = to_vec(doc.keyframes, doc.keyframe_count);
    let keyframe_ranges = to_keyframe_ranges(
        doc.keyframe_range_keys,
        doc.keyframe_range_starts,
        doc.keyframe_range_lengths,
        doc.keyframe_range_count,
    );

    let next_node_id = graph.node_ids.iter().copied().max().unwrap_or(0) + 1;
    let next_port_id = graph.port_ids.iter().copied().max().unwrap_or(0) + 1;
    let next_edge_id = graph.edge_ids.iter().copied().max().unwrap_or(0) + 1;

    persistence::Document {
        graph,
        scalars,
        vectors,
        flags,
        keyframes,
        keyframe_ranges,
        next_node_id,
        next_port_id,
        next_edge_id,
    }
}
