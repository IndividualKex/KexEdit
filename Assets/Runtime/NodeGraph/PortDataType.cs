using KexEdit.Nodes;
using Unity.Burst;

namespace KexEdit.NodeGraph {
    public enum PortDataType : byte {
        Scalar = 0,
        Vector = 1,
        Anchor = 2,
        Path = 3,
    }

    [BurstCompile]
    public static class PortIdExtensions {
        [BurstCompile]
        public static PortDataType DataType(this PortId portId) => portId switch {
            PortId.Anchor => PortDataType.Anchor,
            PortId.Path => PortDataType.Path,
            PortId.Position => PortDataType.Vector,
            PortId.Rotation => PortDataType.Vector,
            PortId.Vector => PortDataType.Vector,
            PortId.Scalar => PortDataType.Scalar,
            _ => PortDataType.Scalar,
        };

        [BurstCompile]
        public static float DefaultValue(this PortId portId) => portId switch {
            PortId.Duration => 5f,
            PortId.Radius => 20f,
            PortId.Arc => 90f,
            PortId.Axis => 0f,
            PortId.LeadIn => 0f,
            PortId.LeadOut => 0f,
            PortId.InWeight => 0.5f,
            PortId.OutWeight => 0.5f,
            PortId.Start => 0f,
            PortId.End => 1f,
            _ => 0f,
        };
    }
}
