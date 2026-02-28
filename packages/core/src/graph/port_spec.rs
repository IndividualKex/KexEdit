#[repr(u8)]
#[derive(Debug, Copy, Clone, PartialEq, Eq, Hash)]
pub enum PortDataType {
    Scalar = 0,
    Vector = 1,
    Anchor = 2,
    Path = 3,
}

impl PortDataType {
    pub fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(Self::Scalar),
            1 => Some(Self::Vector),
            2 => Some(Self::Anchor),
            3 => Some(Self::Path),
            _ => None,
        }
    }
}

#[derive(Debug, Copy, Clone, PartialEq, Eq, Hash)]
pub struct PortSpec {
    pub data_type: PortDataType,
    pub local_index: u8,
}

impl PortSpec {
    pub const fn new(data_type: PortDataType, local_index: u8) -> Self {
        Self {
            data_type,
            local_index,
        }
    }

    /// Encode to u32: (DataType << 8) | LocalIndex
    pub const fn to_encoded(&self) -> u32 {
        ((self.data_type as u32) << 8) | (self.local_index as u32)
    }

    /// Decode from u32
    pub fn from_encoded(encoded: u32) -> Self {
        let data_type_byte = (encoded >> 8) as u8;
        let data_type = match data_type_byte {
            0 => PortDataType::Scalar,
            1 => PortDataType::Vector,
            2 => PortDataType::Anchor,
            3 => PortDataType::Path,
            _ => PortDataType::Scalar,
        };
        Self {
            data_type,
            local_index: (encoded & 0xFF) as u8,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn port_spec_encoding_roundtrip() {
        let specs = [
            PortSpec::new(PortDataType::Scalar, 0),
            PortSpec::new(PortDataType::Scalar, 5),
            PortSpec::new(PortDataType::Vector, 0),
            PortSpec::new(PortDataType::Anchor, 0),
            PortSpec::new(PortDataType::Path, 0),
            PortSpec::new(PortDataType::Path, 255),
        ];

        for spec in specs {
            let encoded = spec.to_encoded();
            let decoded = PortSpec::from_encoded(encoded);
            assert_eq!(spec, decoded, "roundtrip failed for {:?}", spec);
        }
    }

    #[test]
    fn port_spec_anchor_encodes_correctly() {
        let spec = PortSpec::new(PortDataType::Anchor, 0);
        // Anchor = 2, LocalIndex = 0 -> (2 << 8) | 0 = 512
        assert_eq!(spec.to_encoded(), 512);
    }

    #[test]
    fn port_spec_path_encodes_correctly() {
        let spec = PortSpec::new(PortDataType::Path, 0);
        // Path = 3, LocalIndex = 0 -> (3 << 8) | 0 = 768
        assert_eq!(spec.to_encoded(), 768);
    }

    #[test]
    fn port_spec_scalar_with_index_encodes_correctly() {
        let spec = PortSpec::new(PortDataType::Scalar, 2);
        // Scalar = 0, LocalIndex = 2 -> (0 << 8) | 2 = 2
        assert_eq!(spec.to_encoded(), 2);
    }
}
