//! Binary format constants matching Unity's CoasterSerializer.

pub const MAGIC: [u8; 4] = [b'K', b'E', b'X', b'D'];

pub const FILE_VERSION: u32 = 1;
pub const CORE_VERSION: u32 = 1;
pub const DATA_VERSION: u32 = 2;
pub const GRAPH_VERSION: u32 = 1;

pub const CHUNK_CORE: [u8; 4] = [b'C', b'O', b'R', b'E'];
pub const CHUNK_GRPH: [u8; 4] = [b'G', b'R', b'P', b'H'];
pub const CHUNK_DATA: [u8; 4] = [b'D', b'A', b'T', b'A'];

pub const CHUNK_HEADER_SIZE: usize = 12; // 4 type + 4 version + 4 length
