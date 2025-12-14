using Unity.Burst;

namespace KexEdit.Nodes {
    [BurstCompile]
    public static class NodeSchema {
        [BurstCompile]
        public static int InputCount(NodeType type) => type switch {
            NodeType.Force => 2,
            NodeType.Geometric => 2,
            NodeType.Curved => 6,
            NodeType.CopyPath => 4,
            NodeType.Bridge => 3,
            NodeType.Anchor => 2,
            NodeType.Reverse => 1,
            NodeType.ReversePath => 1,
            _ => 0,
        };

        [BurstCompile]
        public static PortId Input(NodeType type, int index) => (type, index) switch {
            (NodeType.Force, 0) => PortId.Anchor,
            (NodeType.Force, 1) => PortId.Duration,
            (NodeType.Geometric, 0) => PortId.Anchor,
            (NodeType.Geometric, 1) => PortId.Duration,
            (NodeType.Curved, 0) => PortId.Anchor,
            (NodeType.Curved, 1) => PortId.Radius,
            (NodeType.Curved, 2) => PortId.Arc,
            (NodeType.Curved, 3) => PortId.Axis,
            (NodeType.Curved, 4) => PortId.LeadIn,
            (NodeType.Curved, 5) => PortId.LeadOut,
            (NodeType.CopyPath, 0) => PortId.Anchor,
            (NodeType.CopyPath, 1) => PortId.Path,
            (NodeType.CopyPath, 2) => PortId.Start,
            (NodeType.CopyPath, 3) => PortId.End,
            (NodeType.Bridge, 0) => PortId.Anchor,
            (NodeType.Bridge, 1) => PortId.InWeight,
            (NodeType.Bridge, 2) => PortId.OutWeight,
            (NodeType.Anchor, 0) => PortId.Position,
            (NodeType.Anchor, 1) => PortId.Rotation,
            (NodeType.Reverse, 0) => PortId.Anchor,
            (NodeType.ReversePath, 0) => PortId.Path,
            _ => (PortId)255,
        };

        [BurstCompile]
        public static int OutputCount(NodeType type) => type switch {
            NodeType.Force => 2,
            NodeType.Geometric => 2,
            NodeType.Curved => 2,
            NodeType.CopyPath => 2,
            NodeType.Bridge => 2,
            NodeType.Anchor => 1,
            NodeType.Reverse => 1,
            NodeType.ReversePath => 1,
            _ => 0,
        };

        [BurstCompile]
        public static PortId Output(NodeType type, int index) => (type, index) switch {
            (NodeType.Force, 0) => PortId.Anchor,
            (NodeType.Force, 1) => PortId.Path,
            (NodeType.Geometric, 0) => PortId.Anchor,
            (NodeType.Geometric, 1) => PortId.Path,
            (NodeType.Curved, 0) => PortId.Anchor,
            (NodeType.Curved, 1) => PortId.Path,
            (NodeType.CopyPath, 0) => PortId.Anchor,
            (NodeType.CopyPath, 1) => PortId.Path,
            (NodeType.Bridge, 0) => PortId.Anchor,
            (NodeType.Bridge, 1) => PortId.Path,
            (NodeType.Anchor, 0) => PortId.Anchor,
            (NodeType.Reverse, 0) => PortId.Anchor,
            (NodeType.ReversePath, 0) => PortId.Path,
            _ => (PortId)255,
        };

        [BurstCompile]
        public static int PropertyCount(NodeType type) => type switch {
            NodeType.Force => 7,
            NodeType.Geometric => 7,
            NodeType.Curved => 5,
            NodeType.CopyPath => 4,
            NodeType.Bridge => 5,
            _ => 0,
        };

        [BurstCompile]
        public static PropertyId Property(NodeType type, int index) => (type, index) switch {
            (NodeType.Force, 0) => PropertyId.RollSpeed,
            (NodeType.Force, 1) => PropertyId.NormalForce,
            (NodeType.Force, 2) => PropertyId.LateralForce,
            (NodeType.Force, 3) => PropertyId.DrivenVelocity,
            (NodeType.Force, 4) => PropertyId.HeartOffset,
            (NodeType.Force, 5) => PropertyId.Friction,
            (NodeType.Force, 6) => PropertyId.Resistance,
            (NodeType.Geometric, 0) => PropertyId.RollSpeed,
            (NodeType.Geometric, 1) => PropertyId.PitchSpeed,
            (NodeType.Geometric, 2) => PropertyId.YawSpeed,
            (NodeType.Geometric, 3) => PropertyId.DrivenVelocity,
            (NodeType.Geometric, 4) => PropertyId.HeartOffset,
            (NodeType.Geometric, 5) => PropertyId.Friction,
            (NodeType.Geometric, 6) => PropertyId.Resistance,
            (NodeType.Curved, 0) => PropertyId.RollSpeed,
            (NodeType.Curved, 1) => PropertyId.DrivenVelocity,
            (NodeType.Curved, 2) => PropertyId.HeartOffset,
            (NodeType.Curved, 3) => PropertyId.Friction,
            (NodeType.Curved, 4) => PropertyId.Resistance,
            (NodeType.CopyPath, 0) => PropertyId.DrivenVelocity,
            (NodeType.CopyPath, 1) => PropertyId.HeartOffset,
            (NodeType.CopyPath, 2) => PropertyId.Friction,
            (NodeType.CopyPath, 3) => PropertyId.Resistance,
            (NodeType.Bridge, 0) => PropertyId.DrivenVelocity,
            (NodeType.Bridge, 1) => PropertyId.HeartOffset,
            (NodeType.Bridge, 2) => PropertyId.Friction,
            (NodeType.Bridge, 3) => PropertyId.Resistance,
            (NodeType.Bridge, 4) => PropertyId.TrackStyle,
            _ => (PropertyId)255,
        };
    }
}
