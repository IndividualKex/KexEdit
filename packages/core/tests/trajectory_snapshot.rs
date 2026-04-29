//! Trajectory snapshot regression tests.
//!
//! Regenerate after intentional output changes:
//!
//! ```bash
//! UPDATE_SNAPSHOTS=1 cargo test --test trajectory_snapshot
//! ```
//!
//! Field order for leaf records (Point, Section, SplinePoint) is fixed by
//! `POINT_FIELDS`, `SECTION_FIELDS`, `SPLINE_FIELDS`. `format_version` in the
//! file fails parsing loudly if those layouts change.

use std::collections::BTreeMap;
use std::fmt::Write as _;
use std::path::PathBuf;

use kexengine::persistence::deserialize;
use kexengine::sim::Point;
use kexengine::track::{
    build_sections, build_traversal_order, collect_sections, compute_continuations,
    evaluate_graph, resample, Section, SplinePoint,
};
use serde::Deserialize;

const FIXTURES: &[&str] = &["circuit", "switch", "all_types", "shuttle"];

/// Arc-length spacing for spline resampling. Chosen as 1 m: typical track
/// units, fine enough to expose interpolation drift, coarse enough to keep
/// snapshot files manageable.
const RESAMPLE_RESOLUTION: f32 = 1.0;

/// Schema marker. Bump when leaf array layouts change so old snapshots fail
/// loudly instead of silently mis-mapping fields.
const FORMAT_VERSION: &str = "2";

/// Number of f32 fields in a Point leaf record (see `point_fields()`).
const POINT_LEN: usize = 23;
/// Number of f32 fields in a SplinePoint leaf record (see `spline_fields()`).
const SPLINE_LEN: usize = 13;

// Tolerances. Same-architecture, same-build runs produce bit-identical
// integration outputs, so a refactor can in principle be checked against zero
// drift. The tolerances below allow a small buffer for benign float-op
// reordering (e.g. compiler choosing different fma) without masking real
// changes.
//
// f32 has 23 mantissa bits ⇒ 1 ULP relative ≈ 1.2e-7. Snapshot values are
// rounded to 6 decimal places before storage, so the parse-side error is
// bounded by 5e-7 absolute regardless of magnitude. Combined budget per leaf
// field is therefore ≈ 1 quantization step + a few ULPs of arithmetic
// reordering.
//
// `ABS_TOL` covers the small-value regime (positions near origin, near-zero
// forces) where the quantization step dominates. `REL_TOL` covers the large-
// value regime (heart_arc on long tracks, ~10⁴ m) where ULP-relative
// drift dominates and absolute matters less.

/// Absolute tolerance: 10× the snapshot quantization step (5e-7), to absorb
/// a quantization-grid crossing plus arithmetic noise on small values.
const ABS_TOL: f32 = 5e-6;

/// Relative tolerance: ~10 ULPs of f32 (≈ 1.2e-6). Allows benign reorderings
/// of associative ops; flags any algorithmic change.
const REL_TOL: f32 = 1e-6;

/// Field order for Point leaf records. Parallel to `point_to_array`.
const POINT_FIELDS: &[&str] = &[
    "heart_position.x",
    "heart_position.y",
    "heart_position.z",
    "direction.x",
    "direction.y",
    "direction.z",
    "normal.x",
    "normal.y",
    "normal.z",
    "lateral.x",
    "lateral.y",
    "lateral.z",
    "velocity",
    "normal_force",
    "lateral_force",
    "heart_arc",
    "spine_arc",
    "heart_advance",
    "friction_origin",
    "roll_speed",
    "heart_offset",
    "friction",
    "resistance",
];

/// Field order for Section leaf records. Mixed int/float; rendered through
/// f64 in the leaf array (i32 round-trips exactly through f64 ↔ JSON).
const SECTION_FIELDS: &[&str] = &[
    "start_index",
    "end_index",
    "arc_start",
    "arc_end",
    "flags",
    "next_index",
    "next_flags",
    "prev_index",
    "prev_flags",
    "spline_start_index",
    "spline_end_index",
];
const SECTION_LEN: usize = 11;
/// Indices into a section array that are integers (exact equality, not f32 tol).
const SECTION_INT_IDX: &[usize] = &[0, 1, 4, 5, 6, 7, 8, 9, 10];

const SPLINE_FIELDS: &[&str] = &[
    "arc",
    "position.x",
    "position.y",
    "position.z",
    "direction.x",
    "direction.y",
    "direction.z",
    "normal.x",
    "normal.y",
    "normal.z",
    "lateral.x",
    "lateral.y",
    "lateral.z",
];

#[derive(Deserialize, Debug)]
struct TrajectorySnapshot {
    fixture: String,
    format_version: String,
    resample_resolution: f32,
    /// node_id (BTreeMap → sorted) -> 23-float array.
    anchors: BTreeMap<u32, Vec<f32>>,
    /// node_id (BTreeMap → sorted) -> Vec of 23-float arrays.
    paths: BTreeMap<u32, Vec<Vec<f32>>>,
    /// Section node ids in the order `collect_sections` returned them.
    section_node_ids: Vec<u32>,
    /// One 12-element f64 array per section (parallel to `section_node_ids`).
    sections: Vec<Vec<f64>>,
    /// Traversal order from `build_traversal_order`.
    traversal_order: Vec<i32>,
    /// One Vec of 13-float arrays per section (parallel to `sections`).
    splines: Vec<Vec<Vec<f32>>>,
}

fn build_snapshot(name: &str) -> TrajectorySnapshot {
    let path = PathBuf::from("test-data").join(format!("{}.kex", name));
    let bytes = std::fs::read(&path).unwrap_or_else(|e| panic!("read {}: {}", path.display(), e));
    let doc = deserialize(&bytes).unwrap_or_else(|e| panic!("deserialize {}: {:?}", name, e));
    let view = doc.as_view();

    let result = evaluate_graph(&view).unwrap_or_else(|| panic!("evaluate_graph returned None for {}", name));

    let anchors: BTreeMap<u32, Vec<f32>> = result
        .anchors
        .iter()
        .map(|(&id, p)| (id, point_to_array(p)))
        .collect();

    let paths: BTreeMap<u32, Vec<Vec<f32>>> = result
        .paths
        .iter()
        .map(|(&id, pts)| (id, pts.iter().map(point_to_array).collect()))
        .collect();

    let sorted = view
        .graph
        .topological_sort()
        .unwrap_or_else(|| panic!("topological_sort returned None for {}", name));
    let (section_node_ids, node_to_section) =
        collect_sections(&sorted, view.graph, &result.paths);
    let (_section_points, mut sections) =
        build_sections(&section_node_ids, &result.paths, &view);
    compute_continuations(&section_node_ids, &node_to_section, &mut sections, &view);
    let traversal_order = build_traversal_order(&section_node_ids, &sections, &view);

    let splines: Vec<Vec<Vec<f32>>> = section_node_ids
        .iter()
        .map(|node_id| {
            let path = result.paths.get(node_id).map(|v| v.as_slice()).unwrap_or(&[]);
            resample(path, RESAMPLE_RESOLUTION)
                .iter()
                .map(spline_to_array)
                .collect()
        })
        .collect();

    let sections = sections.iter().map(section_to_array).collect();

    TrajectorySnapshot {
        fixture: name.to_string(),
        format_version: FORMAT_VERSION.to_string(),
        resample_resolution: RESAMPLE_RESOLUTION,
        anchors,
        paths,
        section_node_ids,
        sections,
        traversal_order,
        splines,
    }
}

fn snapshot_path(name: &str) -> PathBuf {
    PathBuf::from("test-data").join(format!("{}.snap.json", name))
}

fn check_fixture(name: &str) {
    let actual = build_snapshot(name);
    let path = snapshot_path(name);
    let json = render_snapshot(&actual);

    if std::env::var("UPDATE_SNAPSHOTS").is_ok() {
        std::fs::write(&path, &json)
            .unwrap_or_else(|e| panic!("write snapshot {}: {}", path.display(), e));
        eprintln!("wrote {}", path.display());
        return;
    }

    let expected_text = std::fs::read_to_string(&path).unwrap_or_else(|e| {
        panic!(
            "missing snapshot {}: {} — regenerate with UPDATE_SNAPSHOTS=1",
            path.display(),
            e
        )
    });
    let expected: TrajectorySnapshot = serde_json::from_str(&expected_text)
        .unwrap_or_else(|e| panic!("parse snapshot {}: {}", path.display(), e));

    if expected.format_version != FORMAT_VERSION {
        panic!(
            "{}: snapshot format_version {} != current {} — regenerate with UPDATE_SNAPSHOTS=1",
            name, expected.format_version, FORMAT_VERSION
        );
    }

    let diffs = diff_snapshots(&actual, &expected);
    if !diffs.is_empty() {
        let preview = diffs.iter().take(20).cloned().collect::<Vec<_>>().join("\n  ");
        panic!(
            "{} snapshot mismatch ({} diffs, showing first 20):\n  {}\n\nIf the change is intentional, regenerate with UPDATE_SNAPSHOTS=1.",
            name,
            diffs.len(),
            preview
        );
    }
}

#[test]
fn circuit_snapshot_matches() {
    check_fixture("circuit");
}

#[test]
fn switch_snapshot_matches() {
    check_fixture("switch");
}

#[test]
fn all_types_snapshot_matches() {
    check_fixture("all_types");
}

#[test]
fn shuttle_snapshot_matches() {
    check_fixture("shuttle");
}

#[test]
fn snapshots_regenerate_deterministically() {
    // Re-running build_snapshot + render_snapshot on the same fixture must
    // produce byte-identical text. If this fails, something downstream of
    // evaluate_graph (HashMap iteration order, NaN-bit divergence, allocator-
    // dependent state) is leaking nondeterminism into the snapshot.
    for &name in FIXTURES {
        let a = render_snapshot(&build_snapshot(name));
        let b = render_snapshot(&build_snapshot(name));
        assert_eq!(
            a, b,
            "{}: snapshot regen produced different bytes across runs",
            name
        );
    }
}

// ---------------------------------------------------------------------------
// conversion: typed -> flat array
// ---------------------------------------------------------------------------

fn point_to_array(p: &Point) -> Vec<f32> {
    let v: Vec<f32> = vec![
        p.heart_position.x,
        p.heart_position.y,
        p.heart_position.z,
        p.direction.x,
        p.direction.y,
        p.direction.z,
        p.normal.x,
        p.normal.y,
        p.normal.z,
        p.lateral.x,
        p.lateral.y,
        p.lateral.z,
        p.velocity,
        p.normal_force,
        p.lateral_force,
        p.heart_arc,
        p.spine_arc,
        p.heart_advance,
        p.friction_origin,
        p.roll_speed,
        p.heart_offset,
        p.friction,
        p.resistance,
    ];
    debug_assert_eq!(v.len(), POINT_LEN);
    v.into_iter().map(quantize).collect()
}

fn spline_to_array(s: &SplinePoint) -> Vec<f32> {
    let v: Vec<f32> = vec![
        s.arc,
        s.position.x,
        s.position.y,
        s.position.z,
        s.direction.x,
        s.direction.y,
        s.direction.z,
        s.normal.x,
        s.normal.y,
        s.normal.z,
        s.lateral.x,
        s.lateral.y,
        s.lateral.z,
    ];
    debug_assert_eq!(v.len(), SPLINE_LEN);
    v.into_iter().map(quantize).collect()
}

fn section_to_array(s: &Section) -> Vec<f64> {
    // Mixed int/float: f64 round-trips i32 exactly. Quantize the float fields
    // for textual stability; integers are exact.
    let v: Vec<f64> = vec![
        s.start_index as f64,
        s.end_index as f64,
        quantize(s.arc_start) as f64,
        quantize(s.arc_end) as f64,
        s.flags as f64,
        s.next.index as f64,
        s.next.flags as f64,
        s.prev.index as f64,
        s.prev.flags as f64,
        s.spline_start_index as f64,
        s.spline_end_index as f64,
    ];
    debug_assert_eq!(v.len(), SECTION_LEN);
    v
}

fn quantize(x: f32) -> f32 {
    // 6-decimal quantization. Stabilizes the JSON text against ULP-scale
    // recomputation noise so the file diff stays tight during refactors.
    // Only the *stored* value is rounded; comparison is performed on the
    // quantized stored values too, so tolerance budget already accounts for it.
    let q = 1_000_000.0;
    (x * q).round() / q
}

// ---------------------------------------------------------------------------
// custom JSON emitter: top-level pretty, leaves compact-on-one-line
// ---------------------------------------------------------------------------
//
// `serde_json::to_string_pretty` would put every f32 on its own line. With
// 5000 points × 23 fields per fixture this produces 100k+ lines and a 4 MB
// file. We want the structure (paths/sections/splines hierarchy) to indent
// for diffability, but every individual leaf record (a Point / Spline /
// Section array) collapsed to one line. This is straightforward to do by
// hand because the schema is fixed.

fn render_snapshot(snap: &TrajectorySnapshot) -> String {
    let mut s = String::new();
    s.push_str("{\n");
    let _ = writeln!(
        s,
        "  \"fixture\": {},",
        serde_json::to_string(&snap.fixture).unwrap()
    );
    let _ = writeln!(
        s,
        "  \"format_version\": {},",
        serde_json::to_string(&snap.format_version).unwrap()
    );
    let _ = writeln!(
        s,
        "  \"resample_resolution\": {},",
        serde_json::to_string(&snap.resample_resolution).unwrap()
    );

    // anchors: { "id": [23 floats], ... }
    s.push_str("  \"anchors\": {\n");
    let mut first = true;
    for (id, arr) in &snap.anchors {
        if !first {
            s.push_str(",\n");
        }
        first = false;
        let _ = write!(s, "    \"{}\": {}", id, render_f32_array(arr));
    }
    if !snap.anchors.is_empty() {
        s.push('\n');
    }
    s.push_str("  },\n");

    // paths: { "id": [ [23 floats], [23 floats], ... ], ... }
    s.push_str("  \"paths\": {\n");
    let mut first = true;
    for (id, pts) in &snap.paths {
        if !first {
            s.push_str(",\n");
        }
        first = false;
        let _ = writeln!(s, "    \"{}\": [", id);
        for (i, arr) in pts.iter().enumerate() {
            let _ = write!(s, "      {}", render_f32_array(arr));
            if i + 1 < pts.len() {
                s.push(',');
            }
            s.push('\n');
        }
        s.push_str("    ]");
    }
    if !snap.paths.is_empty() {
        s.push('\n');
    }
    s.push_str("  },\n");

    let _ = writeln!(
        s,
        "  \"section_node_ids\": {},",
        serde_json::to_string(&snap.section_node_ids).unwrap()
    );

    s.push_str("  \"sections\": [\n");
    for (i, arr) in snap.sections.iter().enumerate() {
        let _ = write!(s, "    {}", render_f64_array(arr));
        if i + 1 < snap.sections.len() {
            s.push(',');
        }
        s.push('\n');
    }
    s.push_str("  ],\n");

    let _ = writeln!(
        s,
        "  \"traversal_order\": {},",
        serde_json::to_string(&snap.traversal_order).unwrap()
    );

    s.push_str("  \"splines\": [\n");
    for (i, sp) in snap.splines.iter().enumerate() {
        s.push_str("    [\n");
        for (j, arr) in sp.iter().enumerate() {
            let _ = write!(s, "      {}", render_f32_array(arr));
            if j + 1 < sp.len() {
                s.push(',');
            }
            s.push('\n');
        }
        s.push_str("    ]");
        if i + 1 < snap.splines.len() {
            s.push(',');
        }
        s.push('\n');
    }
    s.push_str("  ]\n");

    s.push_str("}\n");
    s
}

fn render_f32_array(arr: &[f32]) -> String {
    let mut s = String::with_capacity(arr.len() * 10);
    s.push('[');
    for (i, v) in arr.iter().enumerate() {
        if i > 0 {
            s.push(',');
        }
        // serde_json uses Ryu shortest round-trip — same code path as parsing.
        let _ = write!(s, "{}", serde_json::to_string(v).unwrap());
    }
    s.push(']');
    s
}

fn render_f64_array(arr: &[f64]) -> String {
    let mut s = String::with_capacity(arr.len() * 10);
    s.push('[');
    for (i, v) in arr.iter().enumerate() {
        if i > 0 {
            s.push(',');
        }
        let _ = write!(s, "{}", serde_json::to_string(v).unwrap());
    }
    s.push(']');
    s
}

// ---------------------------------------------------------------------------
// comparison
// ---------------------------------------------------------------------------

fn diff_snapshots(actual: &TrajectorySnapshot, expected: &TrajectorySnapshot) -> Vec<String> {
    let mut diffs = Vec::new();

    if actual.fixture != expected.fixture {
        diffs.push(format!(
            "fixture mismatch: actual={} expected={}",
            actual.fixture, expected.fixture
        ));
    }
    if (actual.resample_resolution - expected.resample_resolution).abs() > f32::EPSILON {
        diffs.push(format!(
            "resample_resolution mismatch: actual={} expected={}",
            actual.resample_resolution, expected.resample_resolution
        ));
    }

    diff_keys(
        "anchors",
        actual.anchors.keys().copied(),
        expected.anchors.keys().copied(),
        &mut diffs,
    );
    for (id, arr) in &actual.anchors {
        if let Some(exp) = expected.anchors.get(id) {
            cmp_point(arr, exp, &format!("anchors[{}]", id), &mut diffs);
        }
    }

    diff_keys(
        "paths",
        actual.paths.keys().copied(),
        expected.paths.keys().copied(),
        &mut diffs,
    );
    for (id, pts) in &actual.paths {
        let Some(exp_pts) = expected.paths.get(id) else { continue };
        if pts.len() != exp_pts.len() {
            diffs.push(format!(
                "paths[{}] length: actual={} expected={}",
                id,
                pts.len(),
                exp_pts.len()
            ));
            continue;
        }
        for (i, (a, e)) in pts.iter().zip(exp_pts.iter()).enumerate() {
            cmp_point(a, e, &format!("paths[{}][{}]", id, i), &mut diffs);
        }
    }

    if actual.section_node_ids != expected.section_node_ids {
        diffs.push(format!(
            "section_node_ids mismatch: actual={:?} expected={:?}",
            actual.section_node_ids, expected.section_node_ids
        ));
    }
    if actual.traversal_order != expected.traversal_order {
        diffs.push(format!(
            "traversal_order mismatch: actual={:?} expected={:?}",
            actual.traversal_order, expected.traversal_order
        ));
    }

    if actual.sections.len() != expected.sections.len() {
        diffs.push(format!(
            "sections length: actual={} expected={}",
            actual.sections.len(),
            expected.sections.len()
        ));
    } else {
        for (i, (a, e)) in actual.sections.iter().zip(expected.sections.iter()).enumerate() {
            cmp_section(a, e, i, &mut diffs);
        }
    }

    if actual.splines.len() != expected.splines.len() {
        diffs.push(format!(
            "splines length: actual={} expected={}",
            actual.splines.len(),
            expected.splines.len()
        ));
    } else {
        for (i, (a, e)) in actual.splines.iter().zip(expected.splines.iter()).enumerate() {
            if a.len() != e.len() {
                diffs.push(format!(
                    "splines[{}] length: actual={} expected={}",
                    i,
                    a.len(),
                    e.len()
                ));
                continue;
            }
            for (j, (sa, se)) in a.iter().zip(e.iter()).enumerate() {
                cmp_spline(sa, se, &format!("splines[{}][{}]", i, j), &mut diffs);
            }
        }
    }

    diffs
}

fn cmp_point(a: &[f32], b: &[f32], ctx: &str, out: &mut Vec<String>) {
    if a.len() != POINT_LEN || b.len() != POINT_LEN {
        out.push(format!(
            "{}: bad Point length actual={} expected={} (schema = {})",
            ctx,
            a.len(),
            b.len(),
            POINT_LEN
        ));
        return;
    }
    for (i, name) in POINT_FIELDS.iter().enumerate() {
        cmp_f32(a[i], b[i], &format!("{}.{}", ctx, name), out);
    }
}

fn cmp_spline(a: &[f32], b: &[f32], ctx: &str, out: &mut Vec<String>) {
    if a.len() != SPLINE_LEN || b.len() != SPLINE_LEN {
        out.push(format!(
            "{}: bad SplinePoint length actual={} expected={} (schema = {})",
            ctx,
            a.len(),
            b.len(),
            SPLINE_LEN
        ));
        return;
    }
    for (i, name) in SPLINE_FIELDS.iter().enumerate() {
        cmp_f32(a[i], b[i], &format!("{}.{}", ctx, name), out);
    }
}

fn cmp_section(a: &[f64], b: &[f64], idx: usize, out: &mut Vec<String>) {
    if a.len() != SECTION_LEN || b.len() != SECTION_LEN {
        out.push(format!(
            "sections[{}]: bad length actual={} expected={} (schema = {})",
            idx,
            a.len(),
            b.len(),
            SECTION_LEN
        ));
        return;
    }
    for (i, name) in SECTION_FIELDS.iter().enumerate() {
        let ctx = format!("sections[{}].{}", idx, name);
        if SECTION_INT_IDX.contains(&i) {
            if a[i] != b[i] {
                out.push(format!("{}: actual={} expected={}", ctx, a[i], b[i]));
            }
        } else {
            cmp_f32(a[i] as f32, b[i] as f32, &ctx, out);
        }
    }
}

fn approx_eq(a: f32, b: f32) -> bool {
    if a == b {
        return true;
    }
    if a.is_nan() || b.is_nan() {
        return false;
    }
    let diff = (a - b).abs();
    diff <= ABS_TOL || diff <= REL_TOL * a.abs().max(b.abs())
}

fn cmp_f32(a: f32, b: f32, ctx: &str, out: &mut Vec<String>) {
    if !approx_eq(a, b) {
        out.push(format!(
            "{}: actual={} expected={} (Δ={})",
            ctx,
            a,
            b,
            (a - b).abs()
        ));
    }
}

fn diff_keys<I, J>(label: &str, a: I, b: J, out: &mut Vec<String>)
where
    I: IntoIterator<Item = u32>,
    J: IntoIterator<Item = u32>,
{
    let a: std::collections::BTreeSet<u32> = a.into_iter().collect();
    let b: std::collections::BTreeSet<u32> = b.into_iter().collect();
    let only_a: Vec<u32> = a.difference(&b).copied().collect();
    let only_b: Vec<u32> = b.difference(&a).copied().collect();
    if !only_a.is_empty() {
        out.push(format!("{}: extra keys actual: {:?}", label, only_a));
    }
    if !only_b.is_empty() {
        out.push(format!(
            "{}: missing keys (in expected only): {:?}",
            label, only_b
        ));
    }
}

