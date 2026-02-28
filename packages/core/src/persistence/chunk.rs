//! Binary chunk reader/writer matching Unity's ChunkReader/ChunkWriter.

use super::format::CHUNK_HEADER_SIZE;
use super::PersistenceError;
use crate::sim::Float3;
use std::collections::HashMap;

#[derive(Debug, Clone)]
pub struct ChunkHeader {
    pub chunk_type: [u8; 4],
    pub version: u32,
    pub length: u32,
}

pub struct ChunkReader<'a> {
    data: &'a [u8],
    position: usize,
}

impl<'a> ChunkReader<'a> {
    pub fn new(data: &'a [u8]) -> Self {
        Self { data, position: 0 }
    }

    pub fn position(&self) -> usize {
        self.position
    }

    pub fn has_data(&self) -> bool {
        self.position < self.data.len()
    }

    pub fn remaining(&self) -> usize {
        self.data.len().saturating_sub(self.position)
    }

    pub fn try_read_header(&mut self) -> Result<ChunkHeader, PersistenceError> {
        if self.position + CHUNK_HEADER_SIZE > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }

        let mut chunk_type = [0u8; 4];
        chunk_type.copy_from_slice(&self.data[self.position..self.position + 4]);
        self.position += 4;

        let version = self.read_u32()?;
        let length = self.read_u32()?;

        Ok(ChunkHeader {
            chunk_type,
            version,
            length,
        })
    }

    pub fn skip_chunk(&mut self, header: &ChunkHeader) {
        self.position += header.length as usize;
    }

    pub fn read_byte(&mut self) -> Result<u8, PersistenceError> {
        if self.position >= self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let value = self.data[self.position];
        self.position += 1;
        Ok(value)
    }

    pub fn read_bool(&mut self) -> Result<bool, PersistenceError> {
        Ok(self.read_byte()? != 0)
    }

    pub fn read_u32(&mut self) -> Result<u32, PersistenceError> {
        if self.position + 4 > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let bytes: [u8; 4] = self.data[self.position..self.position + 4]
            .try_into()
            .unwrap();
        self.position += 4;
        Ok(u32::from_le_bytes(bytes))
    }

    pub fn read_i32(&mut self) -> Result<i32, PersistenceError> {
        if self.position + 4 > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let bytes: [u8; 4] = self.data[self.position..self.position + 4]
            .try_into()
            .unwrap();
        self.position += 4;
        Ok(i32::from_le_bytes(bytes))
    }

    pub fn read_u64(&mut self) -> Result<u64, PersistenceError> {
        if self.position + 8 > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let bytes: [u8; 8] = self.data[self.position..self.position + 8]
            .try_into()
            .unwrap();
        self.position += 8;
        Ok(u64::from_le_bytes(bytes))
    }

    pub fn read_f32(&mut self) -> Result<f32, PersistenceError> {
        if self.position + 4 > self.data.len() {
            return Err(PersistenceError::TruncatedData);
        }
        let bytes: [u8; 4] = self.data[self.position..self.position + 4]
            .try_into()
            .unwrap();
        self.position += 4;
        Ok(f32::from_le_bytes(bytes))
    }

    pub fn read_float3(&mut self) -> Result<Float3, PersistenceError> {
        Ok(Float3::new(
            self.read_f32()?,
            self.read_f32()?,
            self.read_f32()?,
        ))
    }

    pub fn read_hashmap_u64_f32(&mut self) -> Result<HashMap<u64, f32>, PersistenceError> {
        let count = self.read_i32()? as usize;
        let mut map = HashMap::with_capacity(count);
        for _ in 0..count {
            let key = self.read_u64()?;
            let value = self.read_f32()?;
            map.insert(key, value);
        }
        Ok(map)
    }

    pub fn read_hashmap_u64_float3(&mut self) -> Result<HashMap<u64, Float3>, PersistenceError> {
        let count = self.read_i32()? as usize;
        let mut map = HashMap::with_capacity(count);
        for _ in 0..count {
            let key = self.read_u64()?;
            let value = self.read_float3()?;
            map.insert(key, value);
        }
        Ok(map)
    }

    pub fn read_hashmap_u64_i32(&mut self) -> Result<HashMap<u64, i32>, PersistenceError> {
        let count = self.read_i32()? as usize;
        let mut map = HashMap::with_capacity(count);
        for _ in 0..count {
            let key = self.read_u64()?;
            let value = self.read_i32()?;
            map.insert(key, value);
        }
        Ok(map)
    }
}

pub struct ChunkWriter {
    buffer: Vec<u8>,
    chunk_stack: Vec<usize>, // Start positions of nested chunks
}

impl ChunkWriter {
    pub fn new() -> Self {
        Self {
            buffer: Vec::with_capacity(1024),
            chunk_stack: Vec::with_capacity(8),
        }
    }

    pub fn into_bytes(self) -> Vec<u8> {
        self.buffer
    }

    pub fn len(&self) -> usize {
        self.buffer.len()
    }

    pub fn begin_chunk(&mut self, chunk_type: [u8; 4], version: u32) {
        let start_pos = self.buffer.len();
        self.chunk_stack.push(start_pos);

        self.buffer.extend_from_slice(&chunk_type);
        self.write_u32(version);
        self.write_u32(0); // Placeholder for length
    }

    pub fn end_chunk(&mut self) {
        let start_pos = self.chunk_stack.pop().expect("Unbalanced chunk stack");
        let content_length = self.buffer.len() - start_pos - CHUNK_HEADER_SIZE;

        // Write length at offset +8 from start (after type and version)
        let length_bytes = (content_length as u32).to_le_bytes();
        self.buffer[start_pos + 8..start_pos + 12].copy_from_slice(&length_bytes);
    }

    pub fn write_byte(&mut self, value: u8) {
        self.buffer.push(value);
    }

    pub fn write_bool(&mut self, value: bool) {
        self.buffer.push(if value { 1 } else { 0 });
    }

    pub fn write_u32(&mut self, value: u32) {
        self.buffer.extend_from_slice(&value.to_le_bytes());
    }

    pub fn write_i32(&mut self, value: i32) {
        self.buffer.extend_from_slice(&value.to_le_bytes());
    }

    pub fn write_u64(&mut self, value: u64) {
        self.buffer.extend_from_slice(&value.to_le_bytes());
    }

    pub fn write_f32(&mut self, value: f32) {
        self.buffer.extend_from_slice(&value.to_le_bytes());
    }

    pub fn write_float3(&mut self, value: Float3) {
        self.write_f32(value.x);
        self.write_f32(value.y);
        self.write_f32(value.z);
    }

    pub fn write_hashmap_u64_f32(&mut self, map: &HashMap<u64, f32>) {
        self.write_i32(map.len() as i32);
        for (&key, &value) in map {
            self.write_u64(key);
            self.write_f32(value);
        }
    }

    pub fn write_hashmap_u64_float3(&mut self, map: &HashMap<u64, Float3>) {
        self.write_i32(map.len() as i32);
        for (&key, &value) in map {
            self.write_u64(key);
            self.write_float3(value);
        }
    }

    pub fn write_hashmap_u64_i32(&mut self, map: &HashMap<u64, i32>) {
        self.write_i32(map.len() as i32);
        for (&key, &value) in map {
            self.write_u64(key);
            self.write_i32(value);
        }
    }
}

impl Default for ChunkWriter {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn write_and_read_primitives() {
        let mut writer = ChunkWriter::new();
        writer.write_byte(0x42);
        writer.write_bool(true);
        writer.write_u32(12345);
        writer.write_i32(-9876);
        writer.write_u64(0xDEADBEEF);
        writer.write_f32(3.14159);
        writer.write_float3(Float3::new(1.0, 2.0, 3.0));

        let data = writer.into_bytes();
        let mut reader = ChunkReader::new(&data);

        assert_eq!(reader.read_byte().unwrap(), 0x42);
        assert!(reader.read_bool().unwrap());
        assert_eq!(reader.read_u32().unwrap(), 12345);
        assert_eq!(reader.read_i32().unwrap(), -9876);
        assert_eq!(reader.read_u64().unwrap(), 0xDEADBEEF);
        assert!((reader.read_f32().unwrap() - 3.14159).abs() < 1e-5);
        let v = reader.read_float3().unwrap();
        assert!((v.x - 1.0).abs() < 1e-5);
        assert!((v.y - 2.0).abs() < 1e-5);
        assert!((v.z - 3.0).abs() < 1e-5);
    }

    #[test]
    fn chunk_header_round_trip() {
        let mut writer = ChunkWriter::new();
        writer.begin_chunk(*b"TEST", 42);
        writer.write_u32(12345);
        writer.write_u32(67890);
        writer.end_chunk();

        let data = writer.into_bytes();
        let mut reader = ChunkReader::new(&data);

        let header = reader.try_read_header().unwrap();
        assert_eq!(&header.chunk_type, b"TEST");
        assert_eq!(header.version, 42);
        assert_eq!(header.length, 8); // Two u32s = 8 bytes

        assert_eq!(reader.read_u32().unwrap(), 12345);
        assert_eq!(reader.read_u32().unwrap(), 67890);
    }

    #[test]
    fn nested_chunks() {
        let mut writer = ChunkWriter::new();
        writer.begin_chunk(*b"OUTR", 1);
        writer.write_u32(111);
        writer.begin_chunk(*b"INNR", 2);
        writer.write_u32(222);
        writer.end_chunk();
        writer.write_u32(333);
        writer.end_chunk();

        let data = writer.into_bytes();
        let mut reader = ChunkReader::new(&data);

        let outer = reader.try_read_header().unwrap();
        assert_eq!(&outer.chunk_type, b"OUTR");
        // Outer content: 4 (u32) + 12 (inner header) + 4 (inner content) + 4 (u32) = 24
        assert_eq!(outer.length, 24);

        assert_eq!(reader.read_u32().unwrap(), 111);

        let inner = reader.try_read_header().unwrap();
        assert_eq!(&inner.chunk_type, b"INNR");
        assert_eq!(inner.length, 4);

        assert_eq!(reader.read_u32().unwrap(), 222);
        assert_eq!(reader.read_u32().unwrap(), 333);
    }

    #[test]
    fn hashmap_round_trip() {
        let mut scalars = HashMap::new();
        scalars.insert(100u64, 1.5f32);
        scalars.insert(200u64, 2.5f32);

        let mut writer = ChunkWriter::new();
        writer.write_hashmap_u64_f32(&scalars);

        let data = writer.into_bytes();
        let mut reader = ChunkReader::new(&data);

        let read_scalars = reader.read_hashmap_u64_f32().unwrap();
        assert_eq!(read_scalars.len(), 2);
        assert!((read_scalars[&100] - 1.5).abs() < 1e-5);
        assert!((read_scalars[&200] - 2.5).abs() < 1e-5);
    }
}
