//! Binary persistence for the `.kex` format.
//!
//! # On-disk layout
//!
//! ```text
//! [4 B] magic   = "KEX\0"
//! [4 B] version = u32 little-endian (currently 1)
//! repeat:
//!     [4 B] tag      // ASCII chunk identifier
//!     [4 B] length   // u32 little-endian, bytes that follow
//!     [length B] payload
//! ```
//!
//! Chunks are read sequentially. Unknown tags are skipped by length so the
//! format can grow without breaking older readers. Version 1 defines two
//! chunks: `GRPH` (graph topology) and `DATA` (input values + keyframes).
//!
//! All multi-byte integers and floats are little-endian.

mod document;

pub use document::Document;

use crate::graph::Graph;
use crate::sim::{Float3, InterpolationType, Keyframe};
use std::collections::HashMap;

pub const MAGIC: [u8; 4] = *b"KEX\0";
pub const VERSION: u32 = 1;

const TAG_GRPH: [u8; 4] = *b"GRPH";
const TAG_DATA: [u8; 4] = *b"DATA";

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum PersistenceError {
    InvalidMagic,
    UnsupportedVersion(u32),
    TruncatedData,
    CorruptedData,
}

impl std::fmt::Display for PersistenceError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::InvalidMagic => write!(f, "invalid magic (expected KEX)"),
            Self::UnsupportedVersion(v) => write!(f, "unsupported version: {v}"),
            Self::TruncatedData => write!(f, "truncated data"),
            Self::CorruptedData => write!(f, "corrupted data"),
        }
    }
}

impl std::error::Error for PersistenceError {}

/// Serialize a document to the `.kex` binary format.
pub fn serialize(doc: &Document) -> Vec<u8> {
    let mut buf = Vec::with_capacity(1024);
    buf.extend_from_slice(&MAGIC);
    buf.extend_from_slice(&VERSION.to_le_bytes());
    write_chunk(&mut buf, TAG_GRPH, |out| write_graph(out, doc));
    write_chunk(&mut buf, TAG_DATA, |out| write_data(out, doc));
    buf
}

/// Deserialize a document from the `.kex` binary format.
pub fn deserialize(data: &[u8]) -> Result<Document, PersistenceError> {
    let mut r = Reader::new(data);

    let magic = r.read_bytes(4)?;
    if magic != MAGIC {
        return Err(PersistenceError::InvalidMagic);
    }

    let version = r.read_u32()?;
    if version != VERSION {
        return Err(PersistenceError::UnsupportedVersion(version));
    }

    let mut doc = Document::new();

    while r.has_data() {
        let tag = {
            let mut t = [0u8; 4];
            t.copy_from_slice(r.read_bytes(4)?);
            t
        };
        let length = r.read_u32()? as usize;
        let payload = r.read_bytes(length)?;

        match tag {
            TAG_GRPH => read_graph(payload, &mut doc)?,
            TAG_DATA => read_data(payload, &mut doc)?,
            _ => {} // unknown chunk, skip
        }
    }

    Ok(doc)
}

// ---------------------------------------------------------------------------
// Chunk encoders
// ---------------------------------------------------------------------------

fn write_chunk<F: FnOnce(&mut Vec<u8>)>(buf: &mut Vec<u8>, tag: [u8; 4], body: F) {
    buf.extend_from_slice(&tag);
    let len_pos = buf.len();
    buf.extend_from_slice(&[0u8; 4]); // length placeholder
    let body_start = buf.len();
    body(buf);
    let body_len = (buf.len() - body_start) as u32;
    buf[len_pos..len_pos + 4].copy_from_slice(&body_len.to_le_bytes());
}

fn write_graph(buf: &mut Vec<u8>, doc: &Document) {
    let g = &doc.graph;

    write_u32(buf, g.node_ids.len() as u32);
    for i in 0..g.node_ids.len() {
        write_u32(buf, g.node_ids[i]);
        buf.push(g.node_types[i]);
        write_i32(buf, g.node_input_count[i]);
        write_i32(buf, g.node_output_count[i]);
    }

    write_u32(buf, g.port_ids.len() as u32);
    for i in 0..g.port_ids.len() {
        write_u32(buf, g.port_ids[i]);
        write_u32(buf, g.port_types[i]);
        write_u32(buf, g.port_owners[i]);
        buf.push(if g.port_is_input[i] { 1 } else { 0 });
    }

    write_u32(buf, g.edge_ids.len() as u32);
    for i in 0..g.edge_ids.len() {
        write_u32(buf, g.edge_ids[i]);
        write_u32(buf, g.edge_sources[i]);
        write_u32(buf, g.edge_targets[i]);
    }

    write_u32(buf, doc.next_node_id);
    write_u32(buf, doc.next_port_id);
    write_u32(buf, doc.next_edge_id);
}

fn write_data(buf: &mut Vec<u8>, doc: &Document) {
    write_u32(buf, doc.keyframes.len() as u32);
    for kf in &doc.keyframes {
        write_f32(buf, kf.time);
        write_f32(buf, kf.value);
        buf.push(interp_to_byte(kf.in_interpolation));
        buf.push(interp_to_byte(kf.out_interpolation));
        write_f32(buf, kf.in_tangent);
        write_f32(buf, kf.out_tangent);
        write_f32(buf, kf.in_weight);
        write_f32(buf, kf.out_weight);
    }

    write_u32(buf, doc.keyframe_ranges.len() as u32);
    for (&key, &(start, length)) in &doc.keyframe_ranges {
        write_u64(buf, key);
        write_u32(buf, start as u32);
        write_u32(buf, length as u32);
    }

    write_u32(buf, doc.scalars.len() as u32);
    for (&key, &value) in &doc.scalars {
        write_u64(buf, key);
        write_f32(buf, value);
    }

    write_u32(buf, doc.vectors.len() as u32);
    for (&key, &v) in &doc.vectors {
        write_u64(buf, key);
        write_f32(buf, v.x);
        write_f32(buf, v.y);
        write_f32(buf, v.z);
    }

    write_u32(buf, doc.flags.len() as u32);
    for (&key, &value) in &doc.flags {
        write_u64(buf, key);
        write_i32(buf, value);
    }
}

// ---------------------------------------------------------------------------
// Chunk decoders
// ---------------------------------------------------------------------------

fn read_graph(payload: &[u8], doc: &mut Document) -> Result<(), PersistenceError> {
    let mut r = Reader::new(payload);

    let node_count = r.read_u32()? as usize;
    let mut node_ids = Vec::with_capacity(node_count);
    let mut node_types = Vec::with_capacity(node_count);
    let mut node_input_count = Vec::with_capacity(node_count);
    let mut node_output_count = Vec::with_capacity(node_count);
    for _ in 0..node_count {
        node_ids.push(r.read_u32()?);
        node_types.push(r.read_u8()?);
        node_input_count.push(r.read_i32()?);
        node_output_count.push(r.read_i32()?);
    }

    let port_count = r.read_u32()? as usize;
    let mut port_ids = Vec::with_capacity(port_count);
    let mut port_types = Vec::with_capacity(port_count);
    let mut port_owners = Vec::with_capacity(port_count);
    let mut port_is_input = Vec::with_capacity(port_count);
    for _ in 0..port_count {
        port_ids.push(r.read_u32()?);
        port_types.push(r.read_u32()?);
        port_owners.push(r.read_u32()?);
        port_is_input.push(r.read_u8()? != 0);
    }

    let edge_count = r.read_u32()? as usize;
    let mut edge_ids = Vec::with_capacity(edge_count);
    let mut edge_sources = Vec::with_capacity(edge_count);
    let mut edge_targets = Vec::with_capacity(edge_count);
    for _ in 0..edge_count {
        edge_ids.push(r.read_u32()?);
        edge_sources.push(r.read_u32()?);
        edge_targets.push(r.read_u32()?);
    }

    doc.next_node_id = r.read_u32()?;
    doc.next_port_id = r.read_u32()?;
    doc.next_edge_id = r.read_u32()?;

    doc.graph = Graph::from_vecs(
        node_ids,
        node_types,
        node_input_count,
        node_output_count,
        port_ids,
        port_types,
        port_owners,
        port_is_input,
        edge_ids,
        edge_sources,
        edge_targets,
    );
    Ok(())
}

fn read_data(payload: &[u8], doc: &mut Document) -> Result<(), PersistenceError> {
    let mut r = Reader::new(payload);

    let keyframe_count = r.read_u32()? as usize;
    doc.keyframes = Vec::with_capacity(keyframe_count);
    for _ in 0..keyframe_count {
        let time = r.read_f32()?;
        let value = r.read_f32()?;
        let in_interp = byte_to_interp(r.read_u8()?);
        let out_interp = byte_to_interp(r.read_u8()?);
        let in_tangent = r.read_f32()?;
        let out_tangent = r.read_f32()?;
        let in_weight = r.read_f32()?;
        let out_weight = r.read_f32()?;
        doc.keyframes.push(Keyframe::new(
            time,
            value,
            in_interp,
            out_interp,
            in_tangent,
            out_tangent,
            in_weight,
            out_weight,
        ));
    }

    let range_count = r.read_u32()? as usize;
    doc.keyframe_ranges = HashMap::with_capacity(range_count);
    for _ in 0..range_count {
        let key = r.read_u64()?;
        let start = r.read_u32()? as usize;
        let length = r.read_u32()? as usize;
        doc.keyframe_ranges.insert(key, (start, length));
    }

    let scalar_count = r.read_u32()? as usize;
    doc.scalars = HashMap::with_capacity(scalar_count);
    for _ in 0..scalar_count {
        let key = r.read_u64()?;
        let value = r.read_f32()?;
        doc.scalars.insert(key, value);
    }

    let vector_count = r.read_u32()? as usize;
    doc.vectors = HashMap::with_capacity(vector_count);
    for _ in 0..vector_count {
        let key = r.read_u64()?;
        let v = Float3::new(r.read_f32()?, r.read_f32()?, r.read_f32()?);
        doc.vectors.insert(key, v);
    }

    let flag_count = r.read_u32()? as usize;
    doc.flags = HashMap::with_capacity(flag_count);
    for _ in 0..flag_count {
        let key = r.read_u64()?;
        let value = r.read_i32()?;
        doc.flags.insert(key, value);
    }

    Ok(())
}

// ---------------------------------------------------------------------------
// Primitives
// ---------------------------------------------------------------------------

fn write_u32(buf: &mut Vec<u8>, v: u32) {
    buf.extend_from_slice(&v.to_le_bytes());
}

fn write_i32(buf: &mut Vec<u8>, v: i32) {
    buf.extend_from_slice(&v.to_le_bytes());
}

fn write_u64(buf: &mut Vec<u8>, v: u64) {
    buf.extend_from_slice(&v.to_le_bytes());
}

fn write_f32(buf: &mut Vec<u8>, v: f32) {
    buf.extend_from_slice(&v.to_le_bytes());
}

struct Reader<'a> {
    data: &'a [u8],
    pos: usize,
}

impl<'a> Reader<'a> {
    fn new(data: &'a [u8]) -> Self {
        Self { data, pos: 0 }
    }

    fn has_data(&self) -> bool {
        self.pos < self.data.len()
    }

    fn read_bytes(&mut self, n: usize) -> Result<&'a [u8], PersistenceError> {
        if self.pos + n > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let s = &self.data[self.pos..self.pos + n];
        self.pos += n;
        Ok(s)
    }

    fn read_u8(&mut self) -> Result<u8, PersistenceError> {
        Ok(self.read_bytes(1)?[0])
    }

    fn read_u32(&mut self) -> Result<u32, PersistenceError> {
        let bytes: [u8; 4] = self
            .read_bytes(4)?
            .try_into()
            .map_err(|_| PersistenceError::CorruptedData)?;
        Ok(u32::from_le_bytes(bytes))
    }

    fn read_i32(&mut self) -> Result<i32, PersistenceError> {
        let bytes: [u8; 4] = self
            .read_bytes(4)?
            .try_into()
            .map_err(|_| PersistenceError::CorruptedData)?;
        Ok(i32::from_le_bytes(bytes))
    }

    fn read_u64(&mut self) -> Result<u64, PersistenceError> {
        let bytes: [u8; 8] = self
            .read_bytes(8)?
            .try_into()
            .map_err(|_| PersistenceError::CorruptedData)?;
        Ok(u64::from_le_bytes(bytes))
    }

    fn read_f32(&mut self) -> Result<f32, PersistenceError> {
        let bytes: [u8; 4] = self
            .read_bytes(4)?
            .try_into()
            .map_err(|_| PersistenceError::CorruptedData)?;
        Ok(f32::from_le_bytes(bytes))
    }
}

fn interp_to_byte(i: InterpolationType) -> u8 {
    match i {
        InterpolationType::Constant => 0,
        InterpolationType::Linear => 1,
        InterpolationType::Bezier => 2,
    }
}

fn byte_to_interp(b: u8) -> InterpolationType {
    match b {
        0 => InterpolationType::Constant,
        1 => InterpolationType::Linear,
        _ => InterpolationType::Bezier,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::{Graph, PortDataType, PortSpec};
    use crate::nodes::NodeType;
    use crate::sim::Float3;
    use crate::track::input_key;

    fn make_test_document() -> Document {
        let graph = Graph::from_vecs(
            vec![1, 2],
            vec![NodeType::Anchor as u8, NodeType::Force as u8],
            vec![0, 1],
            vec![1, 2],
            vec![101, 201, 202],
            vec![
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Anchor, 0).to_encoded(),
                PortSpec::new(PortDataType::Path, 0).to_encoded(),
            ],
            vec![1, 2, 2],
            vec![false, true, false],
            vec![301],
            vec![101],
            vec![201],
        );

        let mut doc = Document::new();
        doc.graph = graph;
        doc.next_node_id = 3;
        doc.next_port_id = 203;
        doc.next_edge_id = 302;

        doc.scalars.insert(input_key(2, 0), 5.0);
        doc.vectors
            .insert(input_key(1, 0), Float3::new(0.0, 10.0, 0.0));
        doc.flags.insert(input_key(2, 240), 1);

        doc.keyframes.push(Keyframe::simple(0.0, 0.0));
        doc.keyframes.push(Keyframe::simple(1.0, 1.0));
        doc.keyframe_ranges.insert(input_key(2, 1), (0, 2));

        doc
    }

    #[test]
    fn round_trip_empty_document() {
        let doc = Document::new();
        let bytes = serialize(&doc);
        let loaded = deserialize(&bytes).unwrap();

        assert_eq!(loaded.graph.node_count(), 0);
        assert!(loaded.scalars.is_empty());
        assert!(loaded.vectors.is_empty());
        assert!(loaded.flags.is_empty());
        assert!(loaded.keyframes.is_empty());
    }

    #[test]
    fn round_trip_full_document() {
        let original = make_test_document();
        let bytes = serialize(&original);
        let loaded = deserialize(&bytes).unwrap();

        assert_eq!(loaded.graph.node_ids, original.graph.node_ids);
        assert_eq!(loaded.graph.node_types, original.graph.node_types);
        assert_eq!(loaded.graph.port_ids, original.graph.port_ids);
        assert_eq!(loaded.graph.edge_ids, original.graph.edge_ids);

        assert_eq!(loaded.next_node_id, original.next_node_id);
        assert_eq!(loaded.next_port_id, original.next_port_id);
        assert_eq!(loaded.next_edge_id, original.next_edge_id);

        assert_eq!(loaded.scalars.len(), original.scalars.len());
        assert_eq!(loaded.vectors.len(), original.vectors.len());
        assert_eq!(loaded.flags.len(), original.flags.len());
        assert_eq!(loaded.keyframes.len(), original.keyframes.len());
        assert_eq!(loaded.keyframe_ranges.len(), original.keyframe_ranges.len());
    }

    #[test]
    fn magic_bytes_correct() {
        let bytes = serialize(&Document::new());
        assert_eq!(&bytes[0..4], b"KEX\0");
    }

    #[test]
    fn invalid_magic_returns_error() {
        let mut bytes = serialize(&Document::new());
        bytes[0] = b'X';
        assert!(matches!(
            deserialize(&bytes),
            Err(PersistenceError::InvalidMagic)
        ));
    }

    #[test]
    fn unsupported_version_returns_error() {
        let mut bytes = serialize(&Document::new());
        bytes[4..8].copy_from_slice(&999u32.to_le_bytes());
        assert!(matches!(
            deserialize(&bytes),
            Err(PersistenceError::UnsupportedVersion(999))
        ));
    }

    #[test]
    fn truncated_data_returns_error() {
        let result = deserialize(b"KEX");
        assert!(matches!(result, Err(PersistenceError::TruncatedData)));
    }

    #[test]
    fn skips_unknown_chunks() {
        let mut bytes = serialize(&make_test_document());

        // Inject an unknown chunk before the existing ones by reconstructing
        // the file with header + unknown + original chunks.
        let original = bytes.split_off(8); // 8 = header size
        let unknown_payload = b"future data";
        bytes.extend_from_slice(b"FUTR");
        bytes.extend_from_slice(&(unknown_payload.len() as u32).to_le_bytes());
        bytes.extend_from_slice(unknown_payload);
        bytes.extend_from_slice(&original);

        let loaded = deserialize(&bytes).expect("unknown chunks should be skipped");
        assert_eq!(loaded.graph.node_count(), 2);
    }

    fn load_fixture(name: &str) -> Vec<u8> {
        let path = format!("test-data/{name}.kex");
        std::fs::read(&path).unwrap_or_else(|e| panic!("read {path}: {e}"))
    }

    fn assert_documents_equal(a: &Document, b: &Document, ctx: &str) {
        assert_eq!(a.graph.node_ids, b.graph.node_ids, "{ctx}: node_ids");
        assert_eq!(a.graph.node_types, b.graph.node_types, "{ctx}: node_types");
        assert_eq!(a.graph.port_ids, b.graph.port_ids, "{ctx}: port_ids");
        assert_eq!(a.graph.edge_ids, b.graph.edge_ids, "{ctx}: edge_ids");

        assert_eq!(a.next_node_id, b.next_node_id, "{ctx}: next_node_id");
        assert_eq!(a.next_port_id, b.next_port_id, "{ctx}: next_port_id");
        assert_eq!(a.next_edge_id, b.next_edge_id, "{ctx}: next_edge_id");

        assert_eq!(a.scalars.len(), b.scalars.len(), "{ctx}: scalars len");
        for (k, v) in &a.scalars {
            let r = b
                .scalars
                .get(k)
                .unwrap_or_else(|| panic!("{ctx}: missing scalar {k}"));
            assert!((v - r).abs() < 1e-6, "{ctx}: scalar {k}");
        }

        assert_eq!(a.vectors.len(), b.vectors.len(), "{ctx}: vectors len");
        assert_eq!(a.flags.len(), b.flags.len(), "{ctx}: flags len");
        assert_eq!(a.keyframes.len(), b.keyframes.len(), "{ctx}: keyframes len");
        assert_eq!(
            a.keyframe_ranges.len(),
            b.keyframe_ranges.len(),
            "{ctx}: ranges len"
        );
    }

    fn fixture_round_trip(name: &str) {
        let bytes = load_fixture(name);
        let original = deserialize(&bytes).expect(name);
        let reserialized = serialize(&original);
        let restored = deserialize(&reserialized).expect("round trip");
        assert_documents_equal(&original, &restored, name);
    }

    fn fixture_builds_track(name: &str) {
        use crate::track::evaluate_graph;
        let bytes = load_fixture(name);
        let doc = deserialize(&bytes).expect(name);
        let result = evaluate_graph(&doc.as_view()).expect("evaluation");
        assert!(!result.paths.is_empty(), "{name}: expected paths");
        assert!(!result.anchors.is_empty(), "{name}: expected anchors");
    }

    #[test]
    fn circuit_round_trip() {
        fixture_round_trip("circuit");
    }

    #[test]
    fn switch_round_trip() {
        fixture_round_trip("switch");
    }

    #[test]
    fn all_types_round_trip() {
        fixture_round_trip("all_types");
    }

    #[test]
    fn shuttle_round_trip() {
        fixture_round_trip("shuttle");
    }

    #[test]
    fn circuit_builds_track() {
        fixture_builds_track("circuit");
    }

    #[test]
    fn switch_builds_track() {
        fixture_builds_track("switch");
    }

    #[test]
    fn all_types_builds_track() {
        fixture_builds_track("all_types");
    }

    #[test]
    fn shuttle_builds_track() {
        fixture_builds_track("shuttle");
    }
}
