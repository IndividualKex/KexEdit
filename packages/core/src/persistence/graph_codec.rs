//! Graph serialization matching Unity's GraphCodec.

use super::chunk::{ChunkReader, ChunkWriter};
use super::PersistenceError;
use crate::graph::Graph;

/// Write graph to chunk writer.
/// Format matches Unity's GraphCodec exactly.
pub fn write(writer: &mut ChunkWriter, graph: &Graph, next_ids: (u32, u32, u32)) {
    let (next_node_id, next_port_id, next_edge_id) = next_ids;

    writer.write_i32(graph.node_ids.len() as i32);
    writer.write_i32(graph.port_ids.len() as i32);
    writer.write_i32(graph.edge_ids.len() as i32);

    // Nodes
    for i in 0..graph.node_ids.len() {
        writer.write_u32(graph.node_ids[i]);
        writer.write_u32(graph.node_types[i]);
        writer.write_i32(graph.node_input_count[i]);
        writer.write_i32(graph.node_output_count[i]);
    }

    // Ports
    for i in 0..graph.port_ids.len() {
        writer.write_u32(graph.port_ids[i]);
        writer.write_u32(graph.port_types[i]);
        writer.write_u32(graph.port_owners[i]);
        writer.write_bool(graph.port_is_input[i]);
    }

    // Edges
    for i in 0..graph.edge_ids.len() {
        writer.write_u32(graph.edge_ids[i]);
        writer.write_u32(graph.edge_sources[i]);
        writer.write_u32(graph.edge_targets[i]);
    }

    // Next IDs
    writer.write_u32(next_node_id);
    writer.write_u32(next_port_id);
    writer.write_u32(next_edge_id);
}

/// Read graph from chunk reader.
/// Returns (Graph, next_node_id, next_port_id, next_edge_id).
pub fn read(reader: &mut ChunkReader) -> Result<(Graph, u32, u32, u32), PersistenceError> {
    let node_count = reader.read_i32()? as usize;
    let port_count = reader.read_i32()? as usize;
    let edge_count = reader.read_i32()? as usize;

    let mut node_ids = Vec::with_capacity(node_count);
    let mut node_types = Vec::with_capacity(node_count);
    let mut node_input_count = Vec::with_capacity(node_count);
    let mut node_output_count = Vec::with_capacity(node_count);

    for _ in 0..node_count {
        node_ids.push(reader.read_u32()?);
        node_types.push(reader.read_u32()?);
        node_input_count.push(reader.read_i32()?);
        node_output_count.push(reader.read_i32()?);
    }

    let mut port_ids = Vec::with_capacity(port_count);
    let mut port_types = Vec::with_capacity(port_count);
    let mut port_owners = Vec::with_capacity(port_count);
    let mut port_is_input = Vec::with_capacity(port_count);

    for _ in 0..port_count {
        port_ids.push(reader.read_u32()?);
        port_types.push(reader.read_u32()?);
        port_owners.push(reader.read_u32()?);
        port_is_input.push(reader.read_bool()?);
    }

    let mut edge_ids = Vec::with_capacity(edge_count);
    let mut edge_sources = Vec::with_capacity(edge_count);
    let mut edge_targets = Vec::with_capacity(edge_count);

    for _ in 0..edge_count {
        edge_ids.push(reader.read_u32()?);
        edge_sources.push(reader.read_u32()?);
        edge_targets.push(reader.read_u32()?);
    }

    let next_node_id = reader.read_u32()?;
    let next_port_id = reader.read_u32()?;
    let next_edge_id = reader.read_u32()?;

    let graph = Graph::from_vecs(
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

    Ok((graph, next_node_id, next_port_id, next_edge_id))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::graph::{PortDataType, PortSpec};

    fn make_simple_graph() -> (Graph, u32, u32, u32) {
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
        (graph, 3, 203, 302) // next IDs
    }

    #[test]
    fn graph_round_trip() {
        let (original, next_node, next_port, next_edge) = make_simple_graph();

        let mut writer = ChunkWriter::new();
        write(&mut writer, &original, (next_node, next_port, next_edge));

        let data = writer.into_bytes();
        let mut reader = ChunkReader::new(&data);
        let (loaded, read_next_node, read_next_port, read_next_edge) = read(&mut reader).unwrap();

        assert_eq!(loaded.node_ids, original.node_ids);
        assert_eq!(loaded.node_types, original.node_types);
        assert_eq!(loaded.port_ids, original.port_ids);
        assert_eq!(loaded.edge_ids, original.edge_ids);
        assert_eq!(read_next_node, next_node);
        assert_eq!(read_next_port, next_port);
        assert_eq!(read_next_edge, next_edge);
    }
}
