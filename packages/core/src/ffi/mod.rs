//! C ABI for kexengine.
//!
//! Surface is handle-based: callers pass `.kex` bytes in via [`kex_load`], get
//! an opaque `KexDoc` handle, build into a `KexOutput` handle, and read each
//! output array through a dedicated `kex_output_read_*` accessor.
//!
//! Documents are read-only on the FFI surface. Programmatic construction is
//! the caller's responsibility — produce `.kex` bytes (the format is small
//! and stable; see `persistence`) and load them.
//!
//! # Error codes
//! - `0`: success
//! - `-1`: null pointer
//! - `-3`: buffer too small (size requirement written to `*written`)
//! - `-4`: cycle detected
//!
//! Parse failures return null from [`kex_load`] rather than an error code.

use crate::persistence;
use crate::sim::{Float3, Keyframe, Point};
use crate::track::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    compute_spatial_continuations, evaluate_graph, interpolate_physics, resample, Section,
    SplinePoint,
};

const OK: i32 = 0;
const ERR_NULL: i32 = -1;
const ERR_OVERFLOW: i32 = -3;
const ERR_CYCLE: i32 = -4;

pub type KexDoc = *mut std::ffi::c_void;
pub type KexOutput = *mut std::ffi::c_void;

/// Document counts returned by [`kex_doc_get_counts`].
#[repr(C)]
pub struct KexDocCounts {
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

/// Output counts returned by [`kex_output_get_counts`].
#[repr(C)]
pub struct KexOutputCounts {
    pub points_count: i32,
    pub sections_count: i32,
    pub traversal_count: i32,
    pub spline_count: i32,
}

struct BuiltOutput {
    points: Vec<Point>,
    sections: Vec<Section>,
    section_node_ids: Vec<u32>,
    traversal: Vec<i32>,
    spline_points: Vec<SplinePoint>,
    spline_velocities: Vec<f32>,
    spline_normal_forces: Vec<f32>,
    spline_lateral_forces: Vec<f32>,
    spline_roll_speeds: Vec<f32>,
}

// ---------------------------------------------------------------------------
// Document load / save / free
// ---------------------------------------------------------------------------

/// Load a document from `.kex` bytes. Returns null on failure.
///
/// # Safety
/// `data` must point to ≥ `data_len` initialized bytes.
#[no_mangle]
pub unsafe extern "C" fn kex_load(data: *const u8, data_len: usize) -> KexDoc {
    if data.is_null() || data_len == 0 {
        return std::ptr::null_mut();
    }
    let bytes = std::slice::from_raw_parts(data, data_len);
    match persistence::deserialize(bytes) {
        Ok(doc) => Box::into_raw(Box::new(doc)) as KexDoc,
        Err(_) => std::ptr::null_mut(),
    }
}

/// Required buffer size for [`kex_save`]. Returns `-1` on null handle.
///
/// # Safety
/// `doc` must be a valid handle returned by [`kex_load`], or null.
#[no_mangle]
pub unsafe extern "C" fn kex_save_size(doc: KexDoc) -> i64 {
    if doc.is_null() {
        return -1;
    }
    let d = &*(doc as *const persistence::Document);
    persistence::serialize(d).len() as i64
}

/// Serialize a document into the caller's buffer.
///
/// # Safety
/// `doc` must be a valid handle. `buffer` must point to ≥ `capacity` bytes.
/// `written` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn kex_save(
    doc: KexDoc,
    buffer: *mut u8,
    capacity: usize,
    written: *mut usize,
) -> i32 {
    if doc.is_null() || buffer.is_null() || written.is_null() {
        return ERR_NULL;
    }
    let d = &*(doc as *const persistence::Document);
    let bytes = persistence::serialize(d);
    *written = bytes.len();
    if bytes.len() > capacity {
        return ERR_OVERFLOW;
    }
    std::ptr::copy_nonoverlapping(bytes.as_ptr(), buffer, bytes.len());
    OK
}

/// Free a document handle. Null is accepted.
///
/// # Safety
/// `doc` must be a handle returned by [`kex_load`], or null.
#[no_mangle]
pub unsafe extern "C" fn kex_doc_free(doc: KexDoc) {
    if !doc.is_null() {
        drop(Box::from_raw(doc as *mut persistence::Document));
    }
}

// ---------------------------------------------------------------------------
// Document inspection
// ---------------------------------------------------------------------------

/// Populate [`KexDocCounts`] for `doc` so the caller can size read buffers.
///
/// # Safety
/// `doc` must be a valid handle; `counts` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn kex_doc_get_counts(doc: KexDoc, counts: *mut KexDocCounts) -> i32 {
    if doc.is_null() || counts.is_null() {
        return ERR_NULL;
    }
    let d = &*(doc as *const persistence::Document);
    *counts = KexDocCounts {
        node_count: d.graph.node_ids.len() as i32,
        port_count: d.graph.port_ids.len() as i32,
        edge_count: d.graph.edge_ids.len() as i32,
        scalar_count: d.scalars.len() as i32,
        vector_count: d.vectors.len() as i32,
        flag_count: d.flags.len() as i32,
        keyframe_count: d.keyframes.len() as i32,
        keyframe_range_count: d.keyframe_ranges.len() as i32,
        next_node_id: d.next_node_id,
        next_port_id: d.next_port_id,
        next_edge_id: d.next_edge_id,
    };
    OK
}

/// Copy graph topology into pre-allocated buffers.
///
/// # Safety
/// `doc` must be a valid handle; every buffer pointer must have capacity ≥
/// the matching count from [`kex_doc_get_counts`].
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn kex_doc_read_graph(
    doc: KexDoc,
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
) -> i32 {
    if doc.is_null() {
        return ERR_NULL;
    }
    let d = &*(doc as *const persistence::Document);
    let g = &d.graph;

    for (i, &id) in g.node_ids.iter().enumerate() {
        *node_ids.add(i) = id;
        *node_types.add(i) = g.node_types[i];
        *node_input_counts.add(i) = g.node_input_count[i];
        *node_output_counts.add(i) = g.node_output_count[i];
    }
    for (i, &id) in g.port_ids.iter().enumerate() {
        *port_ids.add(i) = id;
        *port_types.add(i) = g.port_types[i];
        *port_owners.add(i) = g.port_owners[i];
        *port_is_input.add(i) = g.port_is_input[i] as u8;
    }
    for (i, &id) in g.edge_ids.iter().enumerate() {
        *edge_ids.add(i) = id;
        *edge_sources.add(i) = g.edge_sources[i];
        *edge_targets.add(i) = g.edge_targets[i];
    }
    OK
}

/// Copy scalar/vector/flag property maps into pre-allocated buffers.
///
/// # Safety
/// See [`kex_doc_read_graph`].
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn kex_doc_read_properties(
    doc: KexDoc,
    scalar_keys: *mut u64,
    scalar_values: *mut f32,
    vector_keys: *mut u64,
    vector_values: *mut Float3,
    flag_keys: *mut u64,
    flag_values: *mut i32,
) -> i32 {
    if doc.is_null() {
        return ERR_NULL;
    }
    let d = &*(doc as *const persistence::Document);
    for (i, (&k, &v)) in d.scalars.iter().enumerate() {
        *scalar_keys.add(i) = k;
        *scalar_values.add(i) = v;
    }
    for (i, (&k, &v)) in d.vectors.iter().enumerate() {
        *vector_keys.add(i) = k;
        *vector_values.add(i) = v;
    }
    for (i, (&k, &v)) in d.flags.iter().enumerate() {
        *flag_keys.add(i) = k;
        *flag_values.add(i) = v;
    }
    OK
}

/// Copy keyframes + range index into pre-allocated buffers.
///
/// # Safety
/// See [`kex_doc_read_graph`].
#[no_mangle]
pub unsafe extern "C" fn kex_doc_read_keyframes(
    doc: KexDoc,
    keyframes: *mut Keyframe,
    range_keys: *mut u64,
    range_starts: *mut i32,
    range_lengths: *mut i32,
) -> i32 {
    if doc.is_null() {
        return ERR_NULL;
    }
    let d = &*(doc as *const persistence::Document);
    for (i, kf) in d.keyframes.iter().enumerate() {
        *keyframes.add(i) = *kf;
    }
    for (i, (&k, &(start, length))) in d.keyframe_ranges.iter().enumerate() {
        *range_keys.add(i) = k;
        *range_starts.add(i) = start as i32;
        *range_lengths.add(i) = length as i32;
    }
    OK
}

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------

/// Build a track from `doc`. Returns an output handle (or null on error).
///
/// `error_code` is written with `0` on success, `-4` on cycle, `-1` on null doc.
///
/// # Safety
/// `doc` must be a valid handle. `error_code` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn kex_build(
    doc: KexDoc,
    resolution: f32,
    error_code: *mut i32,
) -> KexOutput {
    fn set(p: *mut i32, v: i32) {
        if !p.is_null() {
            unsafe { *p = v };
        }
    }

    if doc.is_null() {
        set(error_code, ERR_NULL);
        return std::ptr::null_mut();
    }

    let d = &*(doc as *const persistence::Document);
    let view = d.as_view();

    let Some(eval) = evaluate_graph(&view) else {
        set(error_code, ERR_CYCLE);
        return std::ptr::null_mut();
    };
    let Some(sorted) = d.graph.topological_sort() else {
        set(error_code, ERR_CYCLE);
        return std::ptr::null_mut();
    };

    let (section_node_ids, node_to_section) = collect_sections(&sorted, &d.graph, &eval.paths);
    let (points, mut sections) = build_sections(&section_node_ids, &eval.paths, &view);
    compute_continuations(&section_node_ids, &node_to_section, &mut sections, &view);
    let traversal = build_traversal_order(&section_node_ids, &sections, &view);
    compute_spatial_continuations(&points, &mut sections, &traversal);

    let mut spline_points = Vec::new();
    let mut spline_velocities = Vec::new();
    let mut spline_normal_forces = Vec::new();
    let mut spline_lateral_forces = Vec::new();
    let mut spline_roll_speeds = Vec::new();

    for section in sections.iter_mut() {
        if !section.is_valid() {
            continue;
        }
        let start = section.start_index as usize;
        let end = section.end_index as usize;
        let path_slice = &points[start..=end];
        let spline = resample(path_slice, resolution);

        let off = spline_points.len() as i32;
        section.spline_start_index = off;
        section.spline_end_index = off + spline.len() as i32 - 1;

        for sp in &spline {
            spline_points.push(*sp);
            let (vel, nf, lf, rs) = interpolate_physics(path_slice, sp.arc);
            spline_velocities.push(vel);
            spline_normal_forces.push(nf);
            spline_lateral_forces.push(lf);
            spline_roll_speeds.push(rs);
        }
    }

    let built = BuiltOutput {
        points,
        sections,
        section_node_ids,
        traversal,
        spline_points,
        spline_velocities,
        spline_normal_forces,
        spline_lateral_forces,
        spline_roll_speeds,
    };

    set(error_code, OK);
    Box::into_raw(Box::new(built)) as KexOutput
}

/// Free an output handle. Null is accepted.
///
/// # Safety
/// `out` must be a handle returned by [`kex_build`], or null.
#[no_mangle]
pub unsafe extern "C" fn kex_output_free(out: KexOutput) {
    if !out.is_null() {
        drop(Box::from_raw(out as *mut BuiltOutput));
    }
}

/// Populate [`KexOutputCounts`] so the caller can size read buffers.
///
/// # Safety
/// `out` must be valid; `counts` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn kex_output_get_counts(
    out: KexOutput,
    counts: *mut KexOutputCounts,
) -> i32 {
    if out.is_null() || counts.is_null() {
        return ERR_NULL;
    }
    let b = &*(out as *const BuiltOutput);
    *counts = KexOutputCounts {
        points_count: b.points.len() as i32,
        sections_count: b.sections.len() as i32,
        traversal_count: b.traversal.len() as i32,
        spline_count: b.spline_points.len() as i32,
    };
    OK
}

/// Copy raw simulation points into a buffer of `capacity` elements.
///
/// # Safety
/// `out` must be valid; `points` must have capacity ≥ `capacity`. Returns
/// `-3` if capacity < points_count.
#[no_mangle]
pub unsafe extern "C" fn kex_output_read_points(
    out: KexOutput,
    points: *mut Point,
    capacity: usize,
) -> i32 {
    if out.is_null() || points.is_null() {
        return ERR_NULL;
    }
    let b = &*(out as *const BuiltOutput);
    if capacity < b.points.len() {
        return ERR_OVERFLOW;
    }
    std::ptr::copy_nonoverlapping(b.points.as_ptr(), points, b.points.len());
    OK
}

/// Copy sections + section_node_ids in parallel.
///
/// # Safety
/// See [`kex_output_read_points`].
#[no_mangle]
pub unsafe extern "C" fn kex_output_read_sections(
    out: KexOutput,
    sections: *mut Section,
    section_node_ids: *mut u32,
    capacity: usize,
) -> i32 {
    if out.is_null() || sections.is_null() || section_node_ids.is_null() {
        return ERR_NULL;
    }
    let b = &*(out as *const BuiltOutput);
    if capacity < b.sections.len() {
        return ERR_OVERFLOW;
    }
    std::ptr::copy_nonoverlapping(b.sections.as_ptr(), sections, b.sections.len());
    std::ptr::copy_nonoverlapping(
        b.section_node_ids.as_ptr(),
        section_node_ids,
        b.section_node_ids.len(),
    );
    OK
}

/// Copy traversal order into a buffer of `capacity` elements.
///
/// # Safety
/// See [`kex_output_read_points`].
#[no_mangle]
pub unsafe extern "C" fn kex_output_read_traversal(
    out: KexOutput,
    traversal: *mut i32,
    capacity: usize,
) -> i32 {
    if out.is_null() || traversal.is_null() {
        return ERR_NULL;
    }
    let b = &*(out as *const BuiltOutput);
    if capacity < b.traversal.len() {
        return ERR_OVERFLOW;
    }
    std::ptr::copy_nonoverlapping(b.traversal.as_ptr(), traversal, b.traversal.len());
    OK
}

/// Copy spline arrays in parallel: positions + per-sample physics.
///
/// # Safety
/// See [`kex_output_read_points`]. All five buffers must have capacity ≥
/// `capacity`.
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn kex_output_read_spline(
    out: KexOutput,
    spline_points: *mut SplinePoint,
    velocities: *mut f32,
    normal_forces: *mut f32,
    lateral_forces: *mut f32,
    roll_speeds: *mut f32,
    capacity: usize,
) -> i32 {
    if out.is_null()
        || spline_points.is_null()
        || velocities.is_null()
        || normal_forces.is_null()
        || lateral_forces.is_null()
        || roll_speeds.is_null()
    {
        return ERR_NULL;
    }
    let b = &*(out as *const BuiltOutput);
    let n = b.spline_points.len();
    if capacity < n {
        return ERR_OVERFLOW;
    }
    std::ptr::copy_nonoverlapping(b.spline_points.as_ptr(), spline_points, n);
    std::ptr::copy_nonoverlapping(b.spline_velocities.as_ptr(), velocities, n);
    std::ptr::copy_nonoverlapping(b.spline_normal_forces.as_ptr(), normal_forces, n);
    std::ptr::copy_nonoverlapping(b.spline_lateral_forces.as_ptr(), lateral_forces, n);
    std::ptr::copy_nonoverlapping(b.spline_roll_speeds.as_ptr(), roll_speeds, n);
    OK
}
