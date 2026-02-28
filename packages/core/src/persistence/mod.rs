//! Binary persistence for kexd format.
//!
//! Provides serialize/deserialize for Document matching Unity's chunked binary format.

mod chunk;
mod document;
mod format;
mod graph_codec;

pub use chunk::{ChunkHeader, ChunkReader, ChunkWriter};
pub use document::Document;
pub use format::*;

use crate::sim::{InterpolationType, Keyframe};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum PersistenceError {
    InvalidMagic,
    UnsupportedVersion { expected: u32, found: u32 },
    TruncatedData,
    InvalidChunkType,
    CorruptedData,
}

impl std::fmt::Display for PersistenceError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            PersistenceError::InvalidMagic => write!(f, "Invalid file magic (expected KEXD)"),
            PersistenceError::UnsupportedVersion { expected, found } => {
                write!(f, "Unsupported version: expected {expected}, found {found}")
            }
            PersistenceError::TruncatedData => write!(f, "Truncated data"),
            PersistenceError::InvalidChunkType => write!(f, "Invalid chunk type"),
            PersistenceError::CorruptedData => write!(f, "Corrupted data"),
        }
    }
}

impl std::error::Error for PersistenceError {}

/// Serialize document to KEXD binary format.
pub fn serialize(doc: &Document) -> Vec<u8> {
    let mut writer = ChunkWriter::new();

    // File header
    for b in MAGIC {
        writer.write_byte(b);
    }
    writer.write_u32(FILE_VERSION);

    // CORE chunk
    writer.begin_chunk(*b"CORE", CORE_VERSION);

    // GRPH sub-chunk
    writer.begin_chunk(*b"GRPH", GRAPH_VERSION);
    graph_codec::write(
        &mut writer,
        &doc.graph,
        (doc.next_node_id, doc.next_port_id, doc.next_edge_id),
    );
    writer.end_chunk();

    // DATA sub-chunk
    writer.begin_chunk(*b"DATA", DATA_VERSION);
    write_keyframes(&mut writer, &doc.keyframes, &doc.keyframe_ranges);
    writer.write_hashmap_u64_f32(&doc.scalars);
    writer.write_hashmap_u64_float3(&doc.vectors);
    writer.write_hashmap_u64_i32(&doc.flags);
    writer.end_chunk();

    writer.end_chunk(); // End CORE

    writer.into_bytes()
}

/// Deserialize document from KEXD binary format.
pub fn deserialize(data: &[u8]) -> Result<Document, PersistenceError> {
    let mut reader = ChunkReader::new(data);

    // Read and validate magic
    if reader.remaining() < 8 {
        return Err(PersistenceError::TruncatedData);
    }

    let mut magic = [0u8; 4];
    for byte in &mut magic {
        *byte = reader.read_byte()?;
    }
    if magic != MAGIC {
        return Err(PersistenceError::InvalidMagic);
    }

    let _file_version = reader.read_u32()?;

    let mut doc = Document::new();

    // Read chunks
    while reader.has_data() {
        let header = match reader.try_read_header() {
            Ok(h) => h,
            Err(_) => break,
        };

        if &header.chunk_type == b"CORE" {
            read_core_chunk(&mut reader, &mut doc, &header)?;
            break;
        } else {
            reader.skip_chunk(&header);
        }
    }

    Ok(doc)
}

fn read_core_chunk(
    reader: &mut ChunkReader,
    doc: &mut Document,
    core_header: &ChunkHeader,
) -> Result<(), PersistenceError> {
    let end_pos = reader.position() + core_header.length as usize;

    while reader.position() < end_pos {
        let sub_header = match reader.try_read_header() {
            Ok(h) => h,
            Err(_) => break,
        };

        if &sub_header.chunk_type == b"GRPH" {
            let (graph, next_node, next_port, next_edge) = graph_codec::read(reader)?;
            doc.graph = graph;
            doc.next_node_id = next_node;
            doc.next_port_id = next_port;
            doc.next_edge_id = next_edge;
        } else if &sub_header.chunk_type == b"DATA" {
            read_keyframes(reader, &mut doc.keyframes, &mut doc.keyframe_ranges)?;
            doc.scalars = reader.read_hashmap_u64_f32()?;
            doc.vectors = reader.read_hashmap_u64_float3()?;
            doc.flags = reader.read_hashmap_u64_i32()?;
        } else {
            reader.skip_chunk(&sub_header);
        }
    }

    Ok(())
}

fn write_keyframes(
    writer: &mut ChunkWriter,
    keyframes: &[Keyframe],
    ranges: &std::collections::HashMap<u64, (usize, usize)>,
) {
    writer.write_i32(keyframes.len() as i32);
    for kf in keyframes {
        writer.write_f32(kf.time);
        writer.write_f32(kf.value);
        writer.write_byte(interp_to_byte(kf.in_interpolation));
        writer.write_byte(interp_to_byte(kf.out_interpolation));
        writer.write_f32(kf.in_tangent);
        writer.write_f32(kf.out_tangent);
        writer.write_f32(kf.in_weight);
        writer.write_f32(kf.out_weight);
    }

    writer.write_i32(ranges.len() as i32);
    for (&key, &(start, length)) in ranges {
        writer.write_u64(key);
        writer.write_i32(start as i32);
        writer.write_i32(length as i32);
    }
}

fn read_keyframes(
    reader: &mut ChunkReader,
    keyframes: &mut Vec<Keyframe>,
    ranges: &mut std::collections::HashMap<u64, (usize, usize)>,
) -> Result<(), PersistenceError> {
    let keyframe_count = reader.read_i32()? as usize;
    keyframes.reserve(keyframe_count);

    for _ in 0..keyframe_count {
        let time = reader.read_f32()?;
        let value = reader.read_f32()?;
        let in_interp = byte_to_interp(reader.read_byte()?);
        let out_interp = byte_to_interp(reader.read_byte()?);
        let in_tangent = reader.read_f32()?;
        let out_tangent = reader.read_f32()?;
        let in_weight = reader.read_f32()?;
        let out_weight = reader.read_f32()?;

        keyframes.push(Keyframe::new(
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

    let range_count = reader.read_i32()? as usize;
    for _ in 0..range_count {
        let key = reader.read_u64()?;
        let start = reader.read_i32()? as usize;
        let length = reader.read_i32()? as usize;
        ranges.insert(key, (start, length));
    }

    Ok(())
}

fn interp_to_byte(interp: InterpolationType) -> u8 {
    match interp {
        InterpolationType::Constant => 0,
        InterpolationType::Linear => 1,
        InterpolationType::Bezier => 2,
    }
}

fn byte_to_interp(byte: u8) -> InterpolationType {
    match byte {
        0 => InterpolationType::Constant,
        1 => InterpolationType::Linear,
        _ => InterpolationType::Bezier,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::{Graph, PortDataType, PortSpec};
    use crate::sim::Float3;
    use crate::track::input_key;

    fn make_test_document() -> Document {
        let graph = Graph::from_vecs(
            vec![1, 2],
            vec![7, 2], // Anchor, Force
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

        // Add some data
        doc.scalars.insert(input_key(2, 0), 5.0); // Duration
        doc.vectors
            .insert(input_key(1, 0), Float3::new(0.0, 10.0, 0.0));
        doc.flags.insert(input_key(2, 240), 1); // Duration type

        // Add keyframes
        doc.keyframes.push(Keyframe::simple(0.0, 0.0));
        doc.keyframes.push(Keyframe::simple(1.0, 1.0));
        doc.keyframe_ranges.insert(input_key(2, 1), (0, 2)); // RollSpeed

        doc
    }

    #[test]
    fn serialize_deserialize_empty_document() {
        let doc = Document::new();
        let data = serialize(&doc);
        let loaded = deserialize(&data).unwrap();

        assert_eq!(loaded.graph.node_count(), 0);
        assert!(loaded.scalars.is_empty());
        assert!(loaded.vectors.is_empty());
        assert!(loaded.flags.is_empty());
        assert!(loaded.keyframes.is_empty());
    }

    #[test]
    fn serialize_deserialize_full_document() {
        let original = make_test_document();
        let data = serialize(&original);
        let loaded = deserialize(&data).unwrap();

        // Graph
        assert_eq!(loaded.graph.node_ids, original.graph.node_ids);
        assert_eq!(loaded.graph.node_types, original.graph.node_types);
        assert_eq!(loaded.graph.port_ids, original.graph.port_ids);
        assert_eq!(loaded.graph.edge_ids, original.graph.edge_ids);

        // Next IDs
        assert_eq!(loaded.next_node_id, original.next_node_id);
        assert_eq!(loaded.next_port_id, original.next_port_id);
        assert_eq!(loaded.next_edge_id, original.next_edge_id);

        // Data maps
        assert_eq!(loaded.scalars.len(), original.scalars.len());
        assert_eq!(loaded.vectors.len(), original.vectors.len());
        assert_eq!(loaded.flags.len(), original.flags.len());

        // Keyframes
        assert_eq!(loaded.keyframes.len(), original.keyframes.len());
        assert_eq!(loaded.keyframe_ranges.len(), original.keyframe_ranges.len());
    }

    #[test]
    fn magic_bytes_correct() {
        let doc = Document::new();
        let data = serialize(&doc);

        assert!(data.len() >= 4);
        assert_eq!(&data[0..4], b"KEXD");
    }

    #[test]
    fn invalid_magic_returns_error() {
        let mut data = serialize(&Document::new());
        data[0] = b'X'; // Corrupt magic

        let result = deserialize(&data);
        assert!(matches!(result, Err(PersistenceError::InvalidMagic)));
    }

    #[test]
    fn truncated_data_returns_error() {
        let result = deserialize(&[b'K', b'E', b'X']);
        assert!(matches!(result, Err(PersistenceError::TruncatedData)));
    }

    // Round-trip tests for _kexd files
    fn load_test_file(name: &str) -> Vec<u8> {
        let path = format!("test-data/{}.kex", name);
        std::fs::read(&path).unwrap_or_else(|e| panic!("Failed to load {}: {}", path, e))
    }

    fn assert_documents_equal(original: &Document, restored: &Document, context: &str) {
        // Graph structure
        assert_eq!(
            original.graph.node_ids, restored.graph.node_ids,
            "{}: node_ids mismatch",
            context
        );
        assert_eq!(
            original.graph.node_types, restored.graph.node_types,
            "{}: node_types mismatch",
            context
        );
        assert_eq!(
            original.graph.port_ids, restored.graph.port_ids,
            "{}: port_ids mismatch",
            context
        );
        assert_eq!(
            original.graph.edge_ids, restored.graph.edge_ids,
            "{}: edge_ids mismatch",
            context
        );

        // Next IDs
        assert_eq!(
            original.next_node_id, restored.next_node_id,
            "{}: next_node_id mismatch",
            context
        );
        assert_eq!(
            original.next_port_id, restored.next_port_id,
            "{}: next_port_id mismatch",
            context
        );
        assert_eq!(
            original.next_edge_id, restored.next_edge_id,
            "{}: next_edge_id mismatch",
            context
        );

        // Data maps
        assert_eq!(
            original.scalars.len(),
            restored.scalars.len(),
            "{}: scalars count mismatch",
            context
        );
        for (key, value) in &original.scalars {
            let restored_value = restored
                .scalars
                .get(key)
                .unwrap_or_else(|| panic!("{}: scalar key {} not found", context, key));
            assert!(
                (value - restored_value).abs() < 1e-6,
                "{}: scalar {} mismatch: {} vs {}",
                context,
                key,
                value,
                restored_value
            );
        }

        assert_eq!(
            original.vectors.len(),
            restored.vectors.len(),
            "{}: vectors count mismatch",
            context
        );
        for (key, value) in &original.vectors {
            let restored_value = restored
                .vectors
                .get(key)
                .unwrap_or_else(|| panic!("{}: vector key {} not found", context, key));
            assert!(
                (value.x - restored_value.x).abs() < 1e-6,
                "{}: vector {}.x mismatch",
                context,
                key
            );
            assert!(
                (value.y - restored_value.y).abs() < 1e-6,
                "{}: vector {}.y mismatch",
                context,
                key
            );
            assert!(
                (value.z - restored_value.z).abs() < 1e-6,
                "{}: vector {}.z mismatch",
                context,
                key
            );
        }

        assert_eq!(
            original.flags.len(),
            restored.flags.len(),
            "{}: flags count mismatch",
            context
        );
        for (key, value) in &original.flags {
            let restored_value = restored
                .flags
                .get(key)
                .unwrap_or_else(|| panic!("{}: flag key {} not found", context, key));
            assert_eq!(
                value, restored_value,
                "{}: flag {} mismatch",
                context, key
            );
        }

        // Keyframes
        assert_eq!(
            original.keyframes.len(),
            restored.keyframes.len(),
            "{}: keyframes count mismatch",
            context
        );
        assert_eq!(
            original.keyframe_ranges.len(),
            restored.keyframe_ranges.len(),
            "{}: keyframe_ranges count mismatch",
            context
        );
    }

    #[test]
    fn circuit_kexd_roundtrip() {
        let data = load_test_file("circuit_kexd");
        let original = deserialize(&data).expect("Failed to deserialize circuit_kexd");

        let serialized = serialize(&original);
        let restored = deserialize(&serialized).expect("Failed to deserialize round-tripped data");

        assert_documents_equal(&original, &restored, "circuit_kexd");
    }

    #[test]
    fn switch_kexd_roundtrip() {
        let data = load_test_file("switch_kexd");
        let original = deserialize(&data).expect("Failed to deserialize switch_kexd");

        let serialized = serialize(&original);
        let restored = deserialize(&serialized).expect("Failed to deserialize round-tripped data");

        assert_documents_equal(&original, &restored, "switch_kexd");
    }

    #[test]
    fn all_types_kexd_roundtrip() {
        let data = load_test_file("all_types_kexd");
        let original = deserialize(&data).expect("Failed to deserialize all_types_kexd");

        let serialized = serialize(&original);
        let restored = deserialize(&serialized).expect("Failed to deserialize round-tripped data");

        assert_documents_equal(&original, &restored, "all_types_kexd");
    }

    #[test]
    fn shuttle_kexd_roundtrip() {
        let data = load_test_file("shuttle_kexd");
        let original = deserialize(&data).expect("Failed to deserialize shuttle_kexd");

        let serialized = serialize(&original);
        let restored = deserialize(&serialized).expect("Failed to deserialize round-tripped data");

        assert_documents_equal(&original, &restored, "shuttle_kexd");
    }

    #[test]
    fn circuit_kexd_builds_track() {
        use crate::track::evaluate_graph;

        let data = load_test_file("circuit_kexd");
        let doc = deserialize(&data).expect("Failed to deserialize circuit_kexd");

        let result = evaluate_graph(&doc.as_view()).expect("circuit_kexd evaluation failed");
        assert!(
            !result.paths.is_empty(),
            "circuit_kexd should produce track paths"
        );
        assert!(
            !result.anchors.is_empty(),
            "circuit_kexd should have anchors"
        );
    }

    #[test]
    fn switch_kexd_builds_track() {
        use crate::track::evaluate_graph;

        let data = load_test_file("switch_kexd");
        let doc = deserialize(&data).expect("Failed to deserialize switch_kexd");

        let result = evaluate_graph(&doc.as_view()).expect("switch_kexd evaluation failed");
        assert!(
            !result.paths.is_empty(),
            "switch_kexd should produce track paths"
        );
        assert!(
            !result.anchors.is_empty(),
            "switch_kexd should have anchors"
        );
    }

    #[test]
    fn all_types_kexd_builds_track() {
        use crate::track::evaluate_graph;

        let data = load_test_file("all_types_kexd");
        let doc = deserialize(&data).expect("Failed to deserialize all_types_kexd");

        let result = evaluate_graph(&doc.as_view()).expect("all_types_kexd evaluation failed");
        assert!(
            !result.paths.is_empty(),
            "all_types_kexd should produce track paths"
        );
        assert!(
            !result.anchors.is_empty(),
            "all_types_kexd should have anchors"
        );
    }

    #[test]
    fn shuttle_kexd_builds_track() {
        use crate::track::evaluate_graph;

        let data = load_test_file("shuttle_kexd");
        let doc = deserialize(&data).expect("Failed to deserialize shuttle_kexd");

        let result = evaluate_graph(&doc.as_view()).expect("shuttle_kexd evaluation failed");
        assert!(
            !result.paths.is_empty(),
            "shuttle_kexd should produce track paths"
        );
        assert!(
            !result.anchors.is_empty(),
            "shuttle_kexd should have anchors"
        );
    }
}
