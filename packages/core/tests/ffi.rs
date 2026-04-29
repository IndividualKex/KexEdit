//! FFI integration tests.
//!
//! Verify the handle-based C ABI in `ffi/mod.rs` produces output identical to
//! the direct Rust API for every fixture, exercises the documented error paths
//! (-1 null, -3 overflow, -4 cycle), and round-trips through `kex_save`.

#![cfg(feature = "ffi")]

use std::path::PathBuf;
use std::ptr;

use kexengine::ffi::{
    kex_build, kex_doc_free, kex_doc_get_counts, kex_doc_read_graph, kex_doc_read_keyframes,
    kex_doc_read_properties, kex_load, kex_output_free, kex_output_get_counts,
    kex_output_read_points, kex_output_read_sections, kex_output_read_spline,
    kex_output_read_traversal, kex_save, kex_save_size, KexDoc, KexDocCounts, KexOutputCounts,
};
use kexengine::graph::Graph;
use kexengine::persistence::{self, Document};
use kexengine::sim::{Float3, Keyframe, Point};
use kexengine::track::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    compute_spatial_continuations, evaluate_graph, interpolate_physics, resample, Section,
    SplinePoint,
};

const FIXTURES: &[&str] = &["circuit", "switch", "all_types", "shuttle"];
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

unsafe fn load_handle(name: &str) -> KexDoc {
    let bytes = fixture_bytes(name);
    let h = kex_load(bytes.as_ptr(), bytes.len());
    assert!(!h.is_null(), "{name}: kex_load returned null");
    h
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

/// Mirrors the body of `kex_build` so divergences flag marshalling errors.
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
        for sp in spline.iter() {
            spline_points.push(*sp);
            let (vel, nf, lf, rs) = interpolate_physics(path_slice, sp.arc);
            velocities.push(vel);
            normal_forces.push(nf);
            lateral_forces.push(lf);
            roll_speeds.push(rs);
        }
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

unsafe fn build_via_ffi(handle: KexDoc, resolution: f32) -> Built {
    let mut err = 0i32;
    let out = kex_build(handle, resolution, &mut err);
    assert_eq!(err, 0, "kex_build error {}", err);
    assert!(!out.is_null());

    let mut counts = KexOutputCounts {
        points_count: 0,
        sections_count: 0,
        traversal_count: 0,
        spline_count: 0,
    };
    assert_eq!(kex_output_get_counts(out, &mut counts), 0);

    let pc = counts.points_count.max(0) as usize;
    let sc = counts.sections_count.max(0) as usize;
    let tc = counts.traversal_count.max(0) as usize;
    let xc = counts.spline_count.max(0) as usize;

    let mut points = vec![Point::DEFAULT; pc.max(1)];
    assert_eq!(
        kex_output_read_points(out, points.as_mut_ptr(), points.len()),
        0
    );
    points.truncate(pc);

    let mut sections = vec![Section::invalid(); sc.max(1)];
    let mut section_node_ids = vec![0u32; sc.max(1)];
    assert_eq!(
        kex_output_read_sections(
            out,
            sections.as_mut_ptr(),
            section_node_ids.as_mut_ptr(),
            sc.max(1)
        ),
        0
    );
    sections.truncate(sc);
    section_node_ids.truncate(sc);

    let mut traversal = vec![-1i32; tc.max(1)];
    assert_eq!(
        kex_output_read_traversal(out, traversal.as_mut_ptr(), traversal.len()),
        0
    );
    traversal.truncate(tc);

    let zero = Float3::ZERO;
    let mut spline_points = vec![SplinePoint::new(0.0, zero, zero, zero, zero); xc.max(1)];
    let mut velocities = vec![0f32; xc.max(1)];
    let mut normal_forces = vec![0f32; xc.max(1)];
    let mut lateral_forces = vec![0f32; xc.max(1)];
    let mut roll_speeds = vec![0f32; xc.max(1)];
    assert_eq!(
        kex_output_read_spline(
            out,
            spline_points.as_mut_ptr(),
            velocities.as_mut_ptr(),
            normal_forces.as_mut_ptr(),
            lateral_forces.as_mut_ptr(),
            roll_speeds.as_mut_ptr(),
            xc.max(1)
        ),
        0
    );
    spline_points.truncate(xc);
    velocities.truncate(xc);
    normal_forces.truncate(xc);
    lateral_forces.truncate(xc);
    roll_speeds.truncate(xc);

    kex_output_free(out);

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
// Document equality
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

unsafe fn read_doc_via_ffi(handle: KexDoc) -> Document {
    let mut counts = KexDocCounts {
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
    assert_eq!(kex_doc_get_counts(handle, &mut counts), 0);

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
    assert_eq!(
        kex_doc_read_graph(
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
        ),
        0
    );

    let mut scalar_keys = vec![0u64; sc.max(1)];
    let mut scalar_values = vec![0.0f32; sc.max(1)];
    let mut vector_keys = vec![0u64; vc.max(1)];
    let mut vector_values = vec![Float3::ZERO; vc.max(1)];
    let mut flag_keys = vec![0u64; fc.max(1)];
    let mut flag_values = vec![0i32; fc.max(1)];
    assert_eq!(
        kex_doc_read_properties(
            handle,
            scalar_keys.as_mut_ptr(),
            scalar_values.as_mut_ptr(),
            vector_keys.as_mut_ptr(),
            vector_values.as_mut_ptr(),
            flag_keys.as_mut_ptr(),
            flag_values.as_mut_ptr(),
        ),
        0
    );

    let mut keyframes = vec![Keyframe::simple(0.0, 0.0); kf.max(1)];
    let mut range_keys = vec![0u64; kr.max(1)];
    let mut range_starts = vec![0i32; kr.max(1)];
    let mut range_lengths = vec![0i32; kr.max(1)];
    assert_eq!(
        kex_doc_read_keyframes(
            handle,
            keyframes.as_mut_ptr(),
            range_keys.as_mut_ptr(),
            range_starts.as_mut_ptr(),
            range_lengths.as_mut_ptr(),
        ),
        0
    );

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
    range_keys.truncate(kr);
    range_starts.truncate(kr);
    range_lengths.truncate(kr);

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

    Document {
        graph,
        scalars: scalar_keys.into_iter().zip(scalar_values).collect(),
        vectors: vector_keys.into_iter().zip(vector_values).collect(),
        flags: flag_keys.into_iter().zip(flag_values).collect(),
        keyframes,
        keyframe_ranges: range_keys
            .into_iter()
            .zip(range_starts.into_iter().zip(range_lengths))
            .map(|(k, (s, l))| (k, (s as usize, l as usize)))
            .collect(),
        next_node_id: counts.next_node_id,
        next_port_id: counts.next_port_id,
        next_edge_id: counts.next_edge_id,
    }
}

unsafe fn save_via_ffi(handle: KexDoc) -> Vec<u8> {
    let size = kex_save_size(handle);
    assert!(size > 0, "kex_save_size returned {}", size);
    let mut buf = vec![0u8; size as usize];
    let mut written = 0usize;
    assert_eq!(kex_save(handle, buf.as_mut_ptr(), buf.len(), &mut written), 0);
    assert_eq!(written, size as usize);
    buf
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

fn check_build(name: &str) {
    unsafe {
        let h = load_handle(name);
        let direct = build_direct(&fixture_document(name), RESOLUTION);
        let ffi = build_via_ffi(h, RESOLUTION);
        assert_built_equal(&ffi, &direct, name);
        kex_doc_free(h);
    }
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

fn check_load(name: &str) {
    unsafe {
        let h = load_handle(name);
        let direct = fixture_document(name);
        let via_ffi = read_doc_via_ffi(h);
        assert_doc_equal(&via_ffi, &direct, name);
        kex_doc_free(h);
    }
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

fn check_save(name: &str) {
    unsafe {
        let h = load_handle(name);
        let bytes = save_via_ffi(h);
        let reloaded = persistence::deserialize(&bytes).expect("deserialize after kex_save");
        assert_doc_equal(&reloaded, &fixture_document(name), name);
        kex_doc_free(h);
    }
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

#[test]
fn full_ffi_round_trip_preserves_documents() {
    unsafe {
        for &name in FIXTURES {
            let h1 = load_handle(name);
            let resaved = save_via_ffi(h1);
            kex_doc_free(h1);

            let h2 = kex_load(resaved.as_ptr(), resaved.len());
            assert!(!h2.is_null());
            let via_ffi = read_doc_via_ffi(h2);
            kex_doc_free(h2);

            assert_doc_equal(&via_ffi, &fixture_document(name), name);
        }
    }
}

// ---------------------------------------------------------------------------
// Error paths
// ---------------------------------------------------------------------------

#[test]
fn kex_load_rejects_invalid_magic() {
    let mut bytes = fixture_bytes("circuit");
    bytes[0] = b'X';
    let h = unsafe { kex_load(bytes.as_ptr(), bytes.len()) };
    assert!(h.is_null());
}

#[test]
fn kex_load_rejects_empty_buffer() {
    let h = unsafe { kex_load(ptr::null(), 0) };
    assert!(h.is_null());
}

#[test]
fn kex_doc_get_counts_rejects_null_handle() {
    let mut counts = KexDocCounts {
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
    assert_eq!(unsafe { kex_doc_get_counts(ptr::null_mut(), &mut counts) }, -1);
}

#[test]
fn kex_save_reports_overflow_with_required_size() {
    unsafe {
        let h = load_handle("circuit");
        let required = kex_save_size(h);
        assert!(required > 1);

        let mut tiny = [0u8; 1];
        let mut written = 0usize;
        let rc = kex_save(h, tiny.as_mut_ptr(), tiny.len(), &mut written);
        assert_eq!(rc, -3);
        assert_eq!(written, required as usize);
        kex_doc_free(h);
    }
}

#[test]
fn kex_save_rejects_null_pointers() {
    unsafe {
        let h = load_handle("circuit");
        let mut buf = [0u8; 16];
        let mut written = 0usize;
        assert_eq!(kex_save(ptr::null_mut(), buf.as_mut_ptr(), buf.len(), &mut written), -1);
        assert_eq!(kex_save(h, ptr::null_mut(), buf.len(), &mut written), -1);
        assert_eq!(kex_save(h, buf.as_mut_ptr(), buf.len(), ptr::null_mut()), -1);
        kex_doc_free(h);
    }
}

#[test]
fn kex_build_rejects_null_handle() {
    let mut err = 0i32;
    let out = unsafe { kex_build(ptr::null_mut(), RESOLUTION, &mut err) };
    assert!(out.is_null());
    assert_eq!(err, -1);
}

#[test]
fn kex_build_returns_null_on_cycle() {
    use kexengine::graph::{PortDataType, PortSpec};
    use kexengine::nodes::NodeType;

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

    let bytes = persistence::serialize(&doc);
    unsafe {
        let h = kex_load(bytes.as_ptr(), bytes.len());
        assert!(!h.is_null());
        let mut err = 0i32;
        let out = kex_build(h, RESOLUTION, &mut err);
        assert!(out.is_null());
        assert_eq!(err, -4);
        kex_doc_free(h);
    }
}

#[test]
fn kex_output_read_returns_minus_three_on_overflow() {
    unsafe {
        let h = load_handle("circuit");
        let mut err = 0i32;
        let out = kex_build(h, RESOLUTION, &mut err);
        assert_eq!(err, 0);
        assert!(!out.is_null());

        let mut tiny_points = [Point::DEFAULT; 1];
        let rc = kex_output_read_points(out, tiny_points.as_mut_ptr(), tiny_points.len());
        assert_eq!(rc, -3);

        kex_output_free(out);
        kex_doc_free(h);
    }
}

#[test]
fn kex_doc_free_handles_null() {
    unsafe { kex_doc_free(ptr::null_mut()) };
}

#[test]
fn kex_output_free_handles_null() {
    unsafe { kex_output_free(ptr::null_mut()) };
}
