//! FFI integration tests.
//!
//! Verify the C ABI surface in `ffi/mod.rs` produces output identical to the
//! direct Rust API for every fixture, and exercise the documented error paths
//! (-1 null pointer, -3 buffer overflow, -4 cycle, invalid magic on load).
//! Until we have these, FFI marshalling bugs only surface through the Blender
//! plugin's pytest, which can't isolate them from plugin bugs.

#![cfg(feature = "ffi")]

use std::collections::HashMap;
use std::path::PathBuf;
use std::ptr;

use kexengine::ffi::{
    kex_build, kex_load, kex_load_copy_data, kex_load_free, kex_load_get_counts, kex_save,
    kex_save_size, KexDocument, KexDocumentCounts, KexOutput,
};
use kexengine::graph::{Graph, PortDataType, PortSpec};
use kexengine::nodes::NodeType;
use kexengine::persistence::{self, Document};
use kexengine::sim::{Float3, Keyframe, Point};
use kexengine::track::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    compute_spatial_continuations, evaluate_graph, interpolate_physics, resample, Section,
    SplinePoint,
};

const FIXTURES: &[&str] = &["circuit", "switch", "all_types", "shuttle"];

/// Spline arc-length spacing. Matches `trajectory_snapshot.rs` so output tables
/// have comparable size; build correctness is independent of resolution.
const RESOLUTION: f32 = 1.0;

// ---------------------------------------------------------------------------
// Fixture helpers
// ---------------------------------------------------------------------------

fn fixture_bytes(name: &str) -> Vec<u8> {
    let path = PathBuf::from("test-data").join(format!("{}.kex", name));
    std::fs::read(&path).unwrap_or_else(|e| panic!("read {}: {}", path.display(), e))
}

fn fixture_document(name: &str) -> Document {
    persistence::deserialize(&fixture_bytes(name)).unwrap_or_else(|e| panic!("{name}: {e:?}"))
}

// ---------------------------------------------------------------------------
// Document → KexDocument marshalling
// ---------------------------------------------------------------------------

/// Owns the converted-shape buffers (port_is_input as u8, hash-map keys/values
/// split into parallel vectors) so the `KexDocument` produced by
/// `as_kex_document` can borrow into stable memory.
struct DocFfi {
    port_is_input_u8: Vec<u8>,
    scalar_keys: Vec<u64>,
    scalar_values: Vec<f32>,
    vector_keys: Vec<u64>,
    vector_values: Vec<Float3>,
    flag_keys: Vec<u64>,
    flag_values: Vec<i32>,
    keyframe_range_keys: Vec<u64>,
    keyframe_range_starts: Vec<i32>,
    keyframe_range_lengths: Vec<i32>,
}

impl DocFfi {
    fn from_document(doc: &Document) -> Self {
        let port_is_input_u8 = doc
            .graph
            .port_is_input
            .iter()
            .map(|&b| b as u8)
            .collect();

        let (scalar_keys, scalar_values) = unzip_map(&doc.scalars);
        let (vector_keys, vector_values) = unzip_map(&doc.vectors);
        let (flag_keys, flag_values) = unzip_map(&doc.flags);

        let mut keyframe_range_keys = Vec::with_capacity(doc.keyframe_ranges.len());
        let mut keyframe_range_starts = Vec::with_capacity(doc.keyframe_ranges.len());
        let mut keyframe_range_lengths = Vec::with_capacity(doc.keyframe_ranges.len());
        for (&k, &(start, length)) in &doc.keyframe_ranges {
            keyframe_range_keys.push(k);
            keyframe_range_starts.push(start as i32);
            keyframe_range_lengths.push(length as i32);
        }

        Self {
            port_is_input_u8,
            scalar_keys,
            scalar_values,
            vector_keys,
            vector_values,
            flag_keys,
            flag_values,
            keyframe_range_keys,
            keyframe_range_starts,
            keyframe_range_lengths,
        }
    }

    fn as_kex_document(&self, doc: &Document) -> KexDocument {
        KexDocument {
            node_ids: ptr_or_null(&doc.graph.node_ids),
            node_count: doc.graph.node_ids.len(),
            node_types: ptr_or_null(&doc.graph.node_types),
            node_input_counts: ptr_or_null(&doc.graph.node_input_count),
            node_output_counts: ptr_or_null(&doc.graph.node_output_count),

            port_ids: ptr_or_null(&doc.graph.port_ids),
            port_count: doc.graph.port_ids.len(),
            port_types: ptr_or_null(&doc.graph.port_types),
            port_owners: ptr_or_null(&doc.graph.port_owners),
            port_is_input: ptr_or_null(&self.port_is_input_u8),

            edge_ids: ptr_or_null(&doc.graph.edge_ids),
            edge_count: doc.graph.edge_ids.len(),
            edge_sources: ptr_or_null(&doc.graph.edge_sources),
            edge_targets: ptr_or_null(&doc.graph.edge_targets),

            scalar_keys: ptr_or_null(&self.scalar_keys),
            scalar_values: ptr_or_null(&self.scalar_values),
            scalar_count: self.scalar_keys.len(),

            vector_keys: ptr_or_null(&self.vector_keys),
            vector_values: ptr_or_null(&self.vector_values),
            vector_count: self.vector_keys.len(),

            flag_keys: ptr_or_null(&self.flag_keys),
            flag_values: ptr_or_null(&self.flag_values),
            flag_count: self.flag_keys.len(),

            keyframes: ptr_or_null(&doc.keyframes),
            keyframe_count: doc.keyframes.len(),
            keyframe_range_keys: ptr_or_null(&self.keyframe_range_keys),
            keyframe_range_starts: ptr_or_null(&self.keyframe_range_starts),
            keyframe_range_lengths: ptr_or_null(&self.keyframe_range_lengths),
            keyframe_range_count: self.keyframe_range_keys.len(),
        }
    }
}

fn ptr_or_null<T>(v: &[T]) -> *const T {
    if v.is_empty() {
        ptr::null()
    } else {
        v.as_ptr()
    }
}

fn unzip_map<K: Copy, V: Copy>(map: &HashMap<K, V>) -> (Vec<K>, Vec<V>) {
    let mut keys = Vec::with_capacity(map.len());
    let mut values = Vec::with_capacity(map.len());
    for (&k, &v) in map {
        keys.push(k);
        values.push(v);
    }
    (keys, values)
}

// ---------------------------------------------------------------------------
// Build comparison
// ---------------------------------------------------------------------------

struct Built {
    points: Vec<Point>,
    sections: Vec<Section>,
    section_node_ids: Vec<u32>,
    traversal: Vec<i32>,
    spline_points: Vec<SplinePoint>,
    velocities: Vec<f32>,
    normal_forces: Vec<f32>,
    lateral_forces: Vec<f32>,
    roll_speeds: Vec<f32>,
}

/// Mirrors the body of `kex_build`. Mismatches between this and `build_via_ffi`
/// reveal marshalling errors in the FFI layer.
fn build_direct(doc: &Document, resolution: f32) -> Built {
    let view = doc.as_view();
    let result = evaluate_graph(&view).expect("evaluate_graph");
    let sorted = doc.graph.topological_sort().expect("topological_sort");
    let (section_node_ids, node_to_section) = collect_sections(&sorted, &doc.graph, &result.paths);
    let (points, mut sections) = build_sections(&section_node_ids, &result.paths, &view);
    compute_continuations(&section_node_ids, &node_to_section, &mut sections, &view);
    let traversal = build_traversal_order(&section_node_ids, &sections, &view);
    compute_spatial_continuations(&points, &mut sections, &traversal);

    let mut spline_points = Vec::new();
    let mut velocities = Vec::new();
    let mut normal_forces = Vec::new();
    let mut lateral_forces = Vec::new();
    let mut roll_speeds = Vec::new();

    let mut spline_offset = 0usize;
    for section in sections.iter_mut() {
        if !section.is_valid() {
            continue;
        }
        let start = section.start_index as usize;
        let end = section.end_index as usize;
        let path_slice = &points[start..=end];
        let spline = resample(path_slice, resolution);
        section.spline_start_index = spline_offset as i32;
        section.spline_end_index = (spline_offset + spline.len() - 1) as i32;
        for sp in spline.iter() {
            spline_points.push(*sp);
            let (vel, nf, lf, rs) = interpolate_physics(path_slice, sp.arc);
            velocities.push(vel);
            normal_forces.push(nf);
            lateral_forces.push(lf);
            roll_speeds.push(rs);
        }
        spline_offset += spline.len();
    }

    Built {
        points,
        sections,
        section_node_ids,
        traversal,
        spline_points,
        velocities,
        normal_forces,
        lateral_forces,
        roll_speeds,
    }
}

fn build_via_ffi(doc: &Document, resolution: f32) -> Built {
    let ffi = DocFfi::from_document(doc);
    let kd = ffi.as_kex_document(doc);

    // Capacities sized for the existing fixtures with several × headroom.
    // Buffer-overflow handling is exercised separately.
    let mut points: Vec<Point> = vec![Point::DEFAULT; 100_000];
    let mut sections: Vec<Section> = vec![Section::invalid(); 4_096];
    let mut section_node_ids: Vec<u32> = vec![0; 4_096];
    let mut traversal: Vec<i32> = vec![-1; 4_096];
    let zero_v3 = Float3::ZERO;
    let mut spline_points: Vec<SplinePoint> =
        vec![SplinePoint::new(0.0, zero_v3, zero_v3, zero_v3, zero_v3); 200_000];
    let mut velocities: Vec<f32> = vec![0.0; 200_000];
    let mut normal_forces: Vec<f32> = vec![0.0; 200_000];
    let mut lateral_forces: Vec<f32> = vec![0.0; 200_000];
    let mut roll_speeds: Vec<f32> = vec![0.0; 200_000];

    let mut points_count: usize = 0;
    let mut sections_count: usize = 0;
    let mut traversal_count: usize = 0;
    let mut spline_count: usize = 0;

    let mut output = KexOutput {
        points: points.as_mut_ptr(),
        points_capacity: points.len(),
        sections: sections.as_mut_ptr(),
        sections_capacity: sections.len(),
        section_node_ids: section_node_ids.as_mut_ptr(),
        traversal_order: traversal.as_mut_ptr(),
        traversal_capacity: traversal.len(),
        spline_points: spline_points.as_mut_ptr(),
        spline_capacity: spline_points.len(),
        spline_velocities: velocities.as_mut_ptr(),
        spline_normal_forces: normal_forces.as_mut_ptr(),
        spline_lateral_forces: lateral_forces.as_mut_ptr(),
        spline_roll_speeds: roll_speeds.as_mut_ptr(),
        points_count: &mut points_count,
        sections_count: &mut sections_count,
        traversal_count: &mut traversal_count,
        spline_count: &mut spline_count,
    };

    let rc = unsafe { kex_build(&kd, resolution, &mut output) };
    assert_eq!(rc, 0, "kex_build returned {}", rc);

    points.truncate(points_count);
    sections.truncate(sections_count);
    section_node_ids.truncate(sections_count);
    traversal.truncate(traversal_count);
    spline_points.truncate(spline_count);
    velocities.truncate(spline_count);
    normal_forces.truncate(spline_count);
    lateral_forces.truncate(spline_count);
    roll_speeds.truncate(spline_count);

    Built {
        points,
        sections,
        section_node_ids,
        traversal,
        spline_points,
        velocities,
        normal_forces,
        lateral_forces,
        roll_speeds,
    }
}

/// Section lacks PartialEq. Compare each field exactly. f32 fields are
/// produced by identical code paths in both routes, so bitwise equality holds.
fn section_eq(a: &Section, b: &Section) -> bool {
    a.start_index == b.start_index
        && a.end_index == b.end_index
        && a.arc_start.to_bits() == b.arc_start.to_bits()
        && a.arc_end.to_bits() == b.arc_end.to_bits()
        && a.flags == b.flags
        && a.next.index == b.next.index
        && a.next.flags == b.next.flags
        && a.prev.index == b.prev.index
        && a.prev.flags == b.prev.flags
        && a.spline_start_index == b.spline_start_index
        && a.spline_end_index == b.spline_end_index
}

fn assert_built_equal(actual: &Built, expected: &Built, ctx: &str) {
    assert_eq!(actual.points, expected.points, "{ctx}: points");
    assert_eq!(
        actual.sections.len(),
        expected.sections.len(),
        "{ctx}: sections len"
    );
    for (i, (a, e)) in actual.sections.iter().zip(expected.sections.iter()).enumerate() {
        assert!(section_eq(a, e), "{ctx}: sections[{i}] differ: {a:?} vs {e:?}");
    }
    assert_eq!(
        actual.section_node_ids, expected.section_node_ids,
        "{ctx}: section_node_ids"
    );
    assert_eq!(actual.traversal, expected.traversal, "{ctx}: traversal");
    assert_eq!(
        actual.spline_points, expected.spline_points,
        "{ctx}: spline_points"
    );
    assert_eq!(actual.velocities, expected.velocities, "{ctx}: velocities");
    assert_eq!(
        actual.normal_forces, expected.normal_forces,
        "{ctx}: normal_forces"
    );
    assert_eq!(
        actual.lateral_forces, expected.lateral_forces,
        "{ctx}: lateral_forces"
    );
    assert_eq!(actual.roll_speeds, expected.roll_speeds, "{ctx}: roll_speeds");
}

// ---------------------------------------------------------------------------
// Document equality (used for save/load round-trips)
// ---------------------------------------------------------------------------

fn assert_doc_equal(a: &Document, b: &Document, ctx: &str) {
    assert_eq!(a.graph.node_ids, b.graph.node_ids, "{ctx}: node_ids");
    assert_eq!(a.graph.node_types, b.graph.node_types, "{ctx}: node_types");
    assert_eq!(
        a.graph.node_input_count, b.graph.node_input_count,
        "{ctx}: node_input_count"
    );
    assert_eq!(
        a.graph.node_output_count, b.graph.node_output_count,
        "{ctx}: node_output_count"
    );
    assert_eq!(a.graph.port_ids, b.graph.port_ids, "{ctx}: port_ids");
    assert_eq!(a.graph.port_types, b.graph.port_types, "{ctx}: port_types");
    assert_eq!(a.graph.port_owners, b.graph.port_owners, "{ctx}: port_owners");
    assert_eq!(
        a.graph.port_is_input, b.graph.port_is_input,
        "{ctx}: port_is_input"
    );
    assert_eq!(a.graph.edge_ids, b.graph.edge_ids, "{ctx}: edge_ids");
    assert_eq!(
        a.graph.edge_sources, b.graph.edge_sources,
        "{ctx}: edge_sources"
    );
    assert_eq!(
        a.graph.edge_targets, b.graph.edge_targets,
        "{ctx}: edge_targets"
    );
    assert_eq!(a.next_node_id, b.next_node_id, "{ctx}: next_node_id");
    assert_eq!(a.next_port_id, b.next_port_id, "{ctx}: next_port_id");
    assert_eq!(a.next_edge_id, b.next_edge_id, "{ctx}: next_edge_id");
    assert_eq!(a.scalars, b.scalars, "{ctx}: scalars");
    assert_eq!(a.vectors, b.vectors, "{ctx}: vectors");
    assert_eq!(a.flags, b.flags, "{ctx}: flags");
    assert_eq!(a.keyframes, b.keyframes, "{ctx}: keyframes");
    assert_eq!(a.keyframe_ranges, b.keyframe_ranges, "{ctx}: keyframe_ranges");
}

// ---------------------------------------------------------------------------
// Reconstruct a Document from kex_load_copy_data outputs
// ---------------------------------------------------------------------------

fn load_document_via_ffi(bytes: &[u8]) -> Document {
    let handle = unsafe { kex_load(bytes.as_ptr(), bytes.len()) };
    assert!(!handle.is_null(), "kex_load returned null");

    let mut counts = KexDocumentCounts {
        node_count: 0,
        port_count: 0,
        edge_count: 0,
        scalar_count: 0,
        vector_count: 0,
        flag_count: 0,
        keyframe_count: 0,
        keyframe_range_count: 0,
        next_node_id: 0,
        next_port_id: 0,
        next_edge_id: 0,
    };
    let rc = unsafe { kex_load_get_counts(handle, &mut counts) };
    assert_eq!(rc, 0);

    let nc = counts.node_count.max(0) as usize;
    let pc = counts.port_count.max(0) as usize;
    let ec = counts.edge_count.max(0) as usize;
    let sc = counts.scalar_count.max(0) as usize;
    let vc = counts.vector_count.max(0) as usize;
    let fc = counts.flag_count.max(0) as usize;
    let kf = counts.keyframe_count.max(0) as usize;
    let kr = counts.keyframe_range_count.max(0) as usize;

    let mut node_ids = vec![0u32; nc.max(1)];
    let mut node_types = vec![0u8; nc.max(1)];
    let mut node_input_counts = vec![0i32; nc.max(1)];
    let mut node_output_counts = vec![0i32; nc.max(1)];
    let mut port_ids = vec![0u32; pc.max(1)];
    let mut port_types = vec![0u32; pc.max(1)];
    let mut port_owners = vec![0u32; pc.max(1)];
    let mut port_is_input_u8 = vec![0u8; pc.max(1)];
    let mut edge_ids = vec![0u32; ec.max(1)];
    let mut edge_sources = vec![0u32; ec.max(1)];
    let mut edge_targets = vec![0u32; ec.max(1)];
    let mut scalar_keys = vec![0u64; sc.max(1)];
    let mut scalar_values = vec![0.0f32; sc.max(1)];
    let mut vector_keys = vec![0u64; vc.max(1)];
    let mut vector_values = vec![Float3::ZERO; vc.max(1)];
    let mut flag_keys = vec![0u64; fc.max(1)];
    let mut flag_values = vec![0i32; fc.max(1)];
    let mut keyframes = vec![Keyframe::simple(0.0, 0.0); kf.max(1)];
    let mut keyframe_range_keys = vec![0u64; kr.max(1)];
    let mut keyframe_range_starts = vec![0i32; kr.max(1)];
    let mut keyframe_range_lengths = vec![0i32; kr.max(1)];

    let rc = unsafe {
        kex_load_copy_data(
            handle,
            node_ids.as_mut_ptr(),
            node_types.as_mut_ptr(),
            node_input_counts.as_mut_ptr(),
            node_output_counts.as_mut_ptr(),
            port_ids.as_mut_ptr(),
            port_types.as_mut_ptr(),
            port_owners.as_mut_ptr(),
            port_is_input_u8.as_mut_ptr(),
            edge_ids.as_mut_ptr(),
            edge_sources.as_mut_ptr(),
            edge_targets.as_mut_ptr(),
            scalar_keys.as_mut_ptr(),
            scalar_values.as_mut_ptr(),
            vector_keys.as_mut_ptr(),
            vector_values.as_mut_ptr(),
            flag_keys.as_mut_ptr(),
            flag_values.as_mut_ptr(),
            keyframes.as_mut_ptr(),
            keyframe_range_keys.as_mut_ptr(),
            keyframe_range_starts.as_mut_ptr(),
            keyframe_range_lengths.as_mut_ptr(),
        )
    };
    assert_eq!(rc, 0);

    unsafe { kex_load_free(handle) };

    node_ids.truncate(nc);
    node_types.truncate(nc);
    node_input_counts.truncate(nc);
    node_output_counts.truncate(nc);
    port_ids.truncate(pc);
    port_types.truncate(pc);
    port_owners.truncate(pc);
    port_is_input_u8.truncate(pc);
    edge_ids.truncate(ec);
    edge_sources.truncate(ec);
    edge_targets.truncate(ec);
    scalar_keys.truncate(sc);
    scalar_values.truncate(sc);
    vector_keys.truncate(vc);
    vector_values.truncate(vc);
    flag_keys.truncate(fc);
    flag_values.truncate(fc);
    keyframes.truncate(kf);
    keyframe_range_keys.truncate(kr);
    keyframe_range_starts.truncate(kr);
    keyframe_range_lengths.truncate(kr);

    let port_is_input: Vec<bool> = port_is_input_u8.into_iter().map(|b| b != 0).collect();

    let graph = Graph::from_vecs(
        node_ids,
        node_types,
        node_input_counts,
        node_output_counts,
        port_ids,
        port_types,
        port_owners,
        port_is_input,
        edge_ids,
        edge_sources,
        edge_targets,
    );

    let scalars = scalar_keys.into_iter().zip(scalar_values).collect();
    let vectors = vector_keys.into_iter().zip(vector_values).collect();
    let flags = flag_keys.into_iter().zip(flag_values).collect();

    let keyframe_ranges = keyframe_range_keys
        .into_iter()
        .zip(
            keyframe_range_starts
                .into_iter()
                .zip(keyframe_range_lengths),
        )
        .map(|(k, (s, l))| (k, (s as usize, l as usize)))
        .collect();

    Document {
        graph,
        scalars,
        vectors,
        flags,
        keyframes,
        keyframe_ranges,
        next_node_id: counts.next_node_id,
        next_port_id: counts.next_port_id,
        next_edge_id: counts.next_edge_id,
    }
}

fn save_document_via_ffi(doc: &Document) -> Vec<u8> {
    let ffi = DocFfi::from_document(doc);
    let kd = ffi.as_kex_document(doc);

    let size = unsafe { kex_save_size(&kd) };
    assert!(size > 0, "kex_save_size returned {}", size);

    let mut buf = vec![0u8; size as usize];
    let mut written: usize = 0;
    let rc = unsafe { kex_save(&kd, buf.as_mut_ptr(), buf.len(), &mut written) };
    assert_eq!(rc, 0);
    assert_eq!(written, size as usize);
    buf
}

// ---------------------------------------------------------------------------
// Tests: kex_build matches direct API for every fixture
// ---------------------------------------------------------------------------

fn check_build(name: &str) {
    let doc = fixture_document(name);
    let direct = build_direct(&doc, RESOLUTION);
    let ffi = build_via_ffi(&doc, RESOLUTION);
    assert_built_equal(&ffi, &direct, name);
}

#[test]
fn build_circuit_matches_direct() {
    check_build("circuit");
}

#[test]
fn build_switch_matches_direct() {
    check_build("switch");
}

#[test]
fn build_all_types_matches_direct() {
    check_build("all_types");
}

#[test]
fn build_shuttle_matches_direct() {
    check_build("shuttle");
}

// ---------------------------------------------------------------------------
// Tests: kex_load + kex_load_get_counts + kex_load_copy_data round-trip
// ---------------------------------------------------------------------------

fn check_load(name: &str) {
    let bytes = fixture_bytes(name);
    let direct = persistence::deserialize(&bytes).expect(name);
    let via_ffi = load_document_via_ffi(&bytes);
    assert_doc_equal(&via_ffi, &direct, name);
}

#[test]
fn load_circuit_matches_direct() {
    check_load("circuit");
}

#[test]
fn load_switch_matches_direct() {
    check_load("switch");
}

#[test]
fn load_all_types_matches_direct() {
    check_load("all_types");
}

#[test]
fn load_shuttle_matches_direct() {
    check_load("shuttle");
}

// ---------------------------------------------------------------------------
// Tests: kex_save_size + kex_save round-trip
// ---------------------------------------------------------------------------

fn check_save(name: &str) {
    let original = fixture_document(name);
    let bytes = save_document_via_ffi(&original);
    let reloaded = persistence::deserialize(&bytes).expect("deserialize after kex_save");
    assert_doc_equal(&reloaded, &original, name);
}

#[test]
fn save_circuit_round_trips() {
    check_save("circuit");
}

#[test]
fn save_switch_round_trips() {
    check_save("switch");
}

#[test]
fn save_all_types_round_trips() {
    check_save("all_types");
}

#[test]
fn save_shuttle_round_trips() {
    check_save("shuttle");
}

// ---------------------------------------------------------------------------
// Tests: full FFI round-trip (load → copy → save → equal)
// ---------------------------------------------------------------------------

#[test]
fn full_ffi_round_trip_preserves_documents() {
    for &name in FIXTURES {
        let original_bytes = fixture_bytes(name);
        let via_ffi = load_document_via_ffi(&original_bytes);
        let resaved = save_document_via_ffi(&via_ffi);
        let reloaded =
            persistence::deserialize(&resaved).expect("deserialize after FFI round-trip");
        let direct =
            persistence::deserialize(&original_bytes).expect("deserialize original");
        assert_doc_equal(&reloaded, &direct, name);
    }
}

// ---------------------------------------------------------------------------
// Error paths
// ---------------------------------------------------------------------------

#[test]
fn kex_load_rejects_invalid_magic() {
    let mut bytes = fixture_bytes("circuit");
    bytes[0] = b'X'; // corrupt the magic
    let handle = unsafe { kex_load(bytes.as_ptr(), bytes.len()) };
    assert!(handle.is_null(), "kex_load should reject invalid magic");
}

#[test]
fn kex_load_rejects_empty_buffer() {
    let handle = unsafe { kex_load(ptr::null(), 0) };
    assert!(handle.is_null());
}

#[test]
fn kex_load_get_counts_returns_minus_one_for_null_handle() {
    let mut counts = KexDocumentCounts {
        node_count: 0,
        port_count: 0,
        edge_count: 0,
        scalar_count: 0,
        vector_count: 0,
        flag_count: 0,
        keyframe_count: 0,
        keyframe_range_count: 0,
        next_node_id: 0,
        next_port_id: 0,
        next_edge_id: 0,
    };
    let rc = unsafe { kex_load_get_counts(ptr::null_mut(), &mut counts) };
    assert_eq!(rc, -1);
}

#[test]
fn kex_save_reports_overflow_with_required_size() {
    let doc = fixture_document("circuit");
    let ffi = DocFfi::from_document(&doc);
    let kd = ffi.as_kex_document(&doc);

    let required = unsafe { kex_save_size(&kd) };
    assert!(required > 1, "expected non-trivial document");

    let mut tiny = [0u8; 1];
    let mut written: usize = 0;
    let rc = unsafe { kex_save(&kd, tiny.as_mut_ptr(), tiny.len(), &mut written) };
    assert_eq!(rc, -3, "expected -3 buffer overflow, got {}", rc);
    assert_eq!(
        written, required as usize,
        "kex_save should report the required size on overflow"
    );
}

#[test]
fn kex_save_returns_minus_one_for_null_pointers() {
    let doc = fixture_document("circuit");
    let ffi = DocFfi::from_document(&doc);
    let kd = ffi.as_kex_document(&doc);
    let mut buf = [0u8; 16];
    let mut written: usize = 0;

    let rc = unsafe { kex_save(ptr::null(), buf.as_mut_ptr(), buf.len(), &mut written) };
    assert_eq!(rc, -1);

    let rc = unsafe { kex_save(&kd, ptr::null_mut(), buf.len(), &mut written) };
    assert_eq!(rc, -1);

    let rc = unsafe { kex_save(&kd, buf.as_mut_ptr(), buf.len(), ptr::null_mut()) };
    assert_eq!(rc, -1);
}

#[test]
fn kex_build_returns_minus_one_for_null_pointers() {
    let mut output = empty_output();
    let rc = unsafe { kex_build(ptr::null(), RESOLUTION, &mut output) };
    assert_eq!(rc, -1);

    let doc = fixture_document("circuit");
    let ffi = DocFfi::from_document(&doc);
    let kd = ffi.as_kex_document(&doc);
    let rc = unsafe { kex_build(&kd, RESOLUTION, ptr::null_mut()) };
    assert_eq!(rc, -1);
}

#[test]
fn kex_build_returns_minus_three_when_points_buffer_too_small() {
    let doc = fixture_document("circuit");
    let ffi = DocFfi::from_document(&doc);
    let kd = ffi.as_kex_document(&doc);

    // Single-element output buffers — circuit has dozens of points, so this
    // forces the capacity check to fire.
    let mut points = [Point::DEFAULT; 1];
    let mut sections = [Section::invalid(); 1];
    let mut section_node_ids = [0u32; 1];
    let mut traversal = [-1i32; 1];
    let zero_v3 = Float3::ZERO;
    let mut spline_points =
        [SplinePoint::new(0.0, zero_v3, zero_v3, zero_v3, zero_v3); 1];
    let mut velocities = [0f32; 1];
    let mut normal_forces = [0f32; 1];
    let mut lateral_forces = [0f32; 1];
    let mut roll_speeds = [0f32; 1];

    let mut points_count = 0usize;
    let mut sections_count = 0usize;
    let mut traversal_count = 0usize;
    let mut spline_count = 0usize;

    let mut output = KexOutput {
        points: points.as_mut_ptr(),
        points_capacity: points.len(),
        sections: sections.as_mut_ptr(),
        sections_capacity: sections.len(),
        section_node_ids: section_node_ids.as_mut_ptr(),
        traversal_order: traversal.as_mut_ptr(),
        traversal_capacity: traversal.len(),
        spline_points: spline_points.as_mut_ptr(),
        spline_capacity: spline_points.len(),
        spline_velocities: velocities.as_mut_ptr(),
        spline_normal_forces: normal_forces.as_mut_ptr(),
        spline_lateral_forces: lateral_forces.as_mut_ptr(),
        spline_roll_speeds: roll_speeds.as_mut_ptr(),
        points_count: &mut points_count,
        sections_count: &mut sections_count,
        traversal_count: &mut traversal_count,
        spline_count: &mut spline_count,
    };

    let rc = unsafe { kex_build(&kd, RESOLUTION, &mut output) };
    assert_eq!(rc, -3, "expected -3 buffer overflow, got {}", rc);
}

#[test]
fn kex_build_returns_minus_four_for_cycle() {
    // 2-node cycle: A.out -> B.in, B.out -> A.in. Same shape as the
    // make_cycle_graph used in the graph traversal unit tests, marshalled
    // through the FFI surface so the cycle detection path is exercised
    // end-to-end.
    let graph = Graph::from_vecs(
        vec![1, 2],
        vec![NodeType::Force as u8, NodeType::Force as u8],
        vec![1, 1],
        vec![1, 1],
        vec![101, 102, 201, 202],
        vec![
            PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
            PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
        ],
        vec![1, 1, 2, 2],
        vec![true, false, true, false],
        vec![301, 302],
        vec![102, 202],
        vec![201, 101],
    );

    let mut doc = Document::new();
    doc.graph = graph;
    doc.next_node_id = 3;
    doc.next_port_id = 203;
    doc.next_edge_id = 303;

    let ffi = DocFfi::from_document(&doc);
    let kd = ffi.as_kex_document(&doc);

    let mut output = empty_output();
    let rc = unsafe { kex_build(&kd, RESOLUTION, &mut output) };
    assert_eq!(rc, -4, "expected -4 cycle, got {}", rc);
}

#[test]
fn kex_load_free_handles_null() {
    // Documented contract: null is acceptable. Should not abort.
    unsafe { kex_load_free(ptr::null_mut()) };
}

// ---------------------------------------------------------------------------
// Output buffer scratch state
// ---------------------------------------------------------------------------

/// Output struct with valid (but trivial) buffer pointers. Used for error-path
/// tests where we don't expect any writes.
fn empty_output() -> KexOutput {
    // `Box::leak` keeps these alive for the duration of the test process so
    // the raw pointers in `KexOutput` stay valid even after this function
    // returns. Acceptable in test code; would not be acceptable in production.
    let points = Box::leak(Box::new([Point::DEFAULT; 1]));
    let sections = Box::leak(Box::new([Section::invalid(); 1]));
    let section_node_ids = Box::leak(Box::new([0u32; 1]));
    let traversal = Box::leak(Box::new([-1i32; 1]));
    let zero_v3 = Float3::ZERO;
    let spline_points = Box::leak(Box::new([SplinePoint::new(
        0.0, zero_v3, zero_v3, zero_v3, zero_v3,
    ); 1]));
    let velocities = Box::leak(Box::new([0f32; 1]));
    let normal_forces = Box::leak(Box::new([0f32; 1]));
    let lateral_forces = Box::leak(Box::new([0f32; 1]));
    let roll_speeds = Box::leak(Box::new([0f32; 1]));
    let points_count = Box::leak(Box::new(0usize));
    let sections_count = Box::leak(Box::new(0usize));
    let traversal_count = Box::leak(Box::new(0usize));
    let spline_count = Box::leak(Box::new(0usize));

    KexOutput {
        points: points.as_mut_ptr(),
        points_capacity: points.len(),
        sections: sections.as_mut_ptr(),
        sections_capacity: sections.len(),
        section_node_ids: section_node_ids.as_mut_ptr(),
        traversal_order: traversal.as_mut_ptr(),
        traversal_capacity: traversal.len(),
        spline_points: spline_points.as_mut_ptr(),
        spline_capacity: spline_points.len(),
        spline_velocities: velocities.as_mut_ptr(),
        spline_normal_forces: normal_forces.as_mut_ptr(),
        spline_lateral_forces: lateral_forces.as_mut_ptr(),
        spline_roll_speeds: roll_speeds.as_mut_ptr(),
        points_count,
        sections_count,
        traversal_count,
        spline_count,
    }
}
