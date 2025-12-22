using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Sim.Schema {
    public enum PropertyKind { Unavailable = 0, Innate = 1, Override = 2 }

    [BurstCompile]
    public static class NodeSchema {
        [BurstCompile]
        public static int InputCount(NodeType type) => type switch {
            NodeType.Force => 2,
            NodeType.Geometric => 2,
            NodeType.Curved => 6,
            NodeType.CopyPath => 4,
            NodeType.Bridge => 4,
            NodeType.Anchor => 8,
            NodeType.Reverse => 1,
            NodeType.ReversePath => 1,
            NodeType.Scalar => 0,
            NodeType.Vector => 0,
            _ => 0,
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
            NodeType.Scalar => 1,
            NodeType.Vector => 1,
            _ => 0,
        };

        [BurstCompile]
        public static int PropertyCount(NodeType type) => type switch {
            NodeType.Scalar => 0,
            NodeType.Vector => 0,
            NodeType.Force => 8,
            NodeType.Geometric => 8,
            NodeType.Curved => 6,
            NodeType.CopyPath => 5,
            NodeType.Bridge => 5,
            _ => 0,
        };

        [BurstCompile]
        public static PropertyKind GetPropertyKind(NodeType nodeType, PropertyId propertyId) => (nodeType, propertyId) switch {
            (NodeType.Force, PropertyId.RollSpeed) => PropertyKind.Innate,
            (NodeType.Force, PropertyId.NormalForce) => PropertyKind.Innate,
            (NodeType.Force, PropertyId.LateralForce) => PropertyKind.Innate,
            (NodeType.Force, PropertyId.DrivenVelocity) => PropertyKind.Override,
            (NodeType.Force, PropertyId.HeartOffset) => PropertyKind.Override,
            (NodeType.Force, PropertyId.Friction) => PropertyKind.Override,
            (NodeType.Force, PropertyId.Resistance) => PropertyKind.Override,
            (NodeType.Force, PropertyId.TrackStyle) => PropertyKind.Override,

            (NodeType.Geometric, PropertyId.RollSpeed) => PropertyKind.Innate,
            (NodeType.Geometric, PropertyId.PitchSpeed) => PropertyKind.Innate,
            (NodeType.Geometric, PropertyId.YawSpeed) => PropertyKind.Innate,
            (NodeType.Geometric, PropertyId.DrivenVelocity) => PropertyKind.Override,
            (NodeType.Geometric, PropertyId.HeartOffset) => PropertyKind.Override,
            (NodeType.Geometric, PropertyId.Friction) => PropertyKind.Override,
            (NodeType.Geometric, PropertyId.Resistance) => PropertyKind.Override,
            (NodeType.Geometric, PropertyId.TrackStyle) => PropertyKind.Override,

            (NodeType.Curved, PropertyId.RollSpeed) => PropertyKind.Innate,
            (NodeType.Curved, PropertyId.DrivenVelocity) => PropertyKind.Override,
            (NodeType.Curved, PropertyId.HeartOffset) => PropertyKind.Override,
            (NodeType.Curved, PropertyId.Friction) => PropertyKind.Override,
            (NodeType.Curved, PropertyId.Resistance) => PropertyKind.Override,
            (NodeType.Curved, PropertyId.TrackStyle) => PropertyKind.Override,

            (NodeType.CopyPath, PropertyId.DrivenVelocity) => PropertyKind.Override,
            (NodeType.CopyPath, PropertyId.HeartOffset) => PropertyKind.Override,
            (NodeType.CopyPath, PropertyId.Friction) => PropertyKind.Override,
            (NodeType.CopyPath, PropertyId.Resistance) => PropertyKind.Override,
            (NodeType.CopyPath, PropertyId.TrackStyle) => PropertyKind.Override,

            (NodeType.Bridge, PropertyId.DrivenVelocity) => PropertyKind.Override,
            (NodeType.Bridge, PropertyId.HeartOffset) => PropertyKind.Override,
            (NodeType.Bridge, PropertyId.Friction) => PropertyKind.Override,
            (NodeType.Bridge, PropertyId.Resistance) => PropertyKind.Override,
            (NodeType.Bridge, PropertyId.TrackStyle) => PropertyKind.Override,

            _ => PropertyKind.Unavailable,
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
            (NodeType.Force, 7) => PropertyId.TrackStyle,
            (NodeType.Geometric, 0) => PropertyId.RollSpeed,
            (NodeType.Geometric, 1) => PropertyId.PitchSpeed,
            (NodeType.Geometric, 2) => PropertyId.YawSpeed,
            (NodeType.Geometric, 3) => PropertyId.DrivenVelocity,
            (NodeType.Geometric, 4) => PropertyId.HeartOffset,
            (NodeType.Geometric, 5) => PropertyId.Friction,
            (NodeType.Geometric, 6) => PropertyId.Resistance,
            (NodeType.Geometric, 7) => PropertyId.TrackStyle,
            (NodeType.Curved, 0) => PropertyId.RollSpeed,
            (NodeType.Curved, 1) => PropertyId.DrivenVelocity,
            (NodeType.Curved, 2) => PropertyId.HeartOffset,
            (NodeType.Curved, 3) => PropertyId.Friction,
            (NodeType.Curved, 4) => PropertyId.Resistance,
            (NodeType.Curved, 5) => PropertyId.TrackStyle,
            (NodeType.CopyPath, 0) => PropertyId.DrivenVelocity,
            (NodeType.CopyPath, 1) => PropertyId.HeartOffset,
            (NodeType.CopyPath, 2) => PropertyId.Friction,
            (NodeType.CopyPath, 3) => PropertyId.Resistance,
            (NodeType.CopyPath, 4) => PropertyId.TrackStyle,
            (NodeType.Bridge, 0) => PropertyId.DrivenVelocity,
            (NodeType.Bridge, 1) => PropertyId.HeartOffset,
            (NodeType.Bridge, 2) => PropertyId.Friction,
            (NodeType.Bridge, 3) => PropertyId.Resistance,
            (NodeType.Bridge, 4) => PropertyId.TrackStyle,
            _ => (PropertyId)255,
        };

        [BurstCompile]
        public static void InputSpec(NodeType type, int index, out PortSpec result) {
            result = (type, index) switch {
                (NodeType.Force, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Force, 1) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Geometric, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Geometric, 1) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Curved, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Curved, 1) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Curved, 2) => new PortSpec(PortDataType.Scalar, 1),
                (NodeType.Curved, 3) => new PortSpec(PortDataType.Scalar, 2),
                (NodeType.Curved, 4) => new PortSpec(PortDataType.Scalar, 3),
                (NodeType.Curved, 5) => new PortSpec(PortDataType.Scalar, 4),
                (NodeType.CopyPath, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.CopyPath, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.CopyPath, 2) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.CopyPath, 3) => new PortSpec(PortDataType.Scalar, 1),
                (NodeType.Bridge, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Bridge, 1) => new PortSpec(PortDataType.Anchor, 1),
                (NodeType.Bridge, 2) => new PortSpec(PortDataType.Scalar, 1),
                (NodeType.Bridge, 3) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Anchor, 0) => new PortSpec(PortDataType.Vector, 0),
                (NodeType.Anchor, 1) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Anchor, 2) => new PortSpec(PortDataType.Scalar, 1),
                (NodeType.Anchor, 3) => new PortSpec(PortDataType.Scalar, 2),
                (NodeType.Anchor, 4) => new PortSpec(PortDataType.Scalar, 3),
                (NodeType.Anchor, 5) => new PortSpec(PortDataType.Scalar, 4),
                (NodeType.Anchor, 6) => new PortSpec(PortDataType.Scalar, 5),
                (NodeType.Anchor, 7) => new PortSpec(PortDataType.Scalar, 6),
                (NodeType.Reverse, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.ReversePath, 0) => new PortSpec(PortDataType.Path, 0),
                _ => PortSpec.Invalid,
            };
        }

        [BurstCompile]
        public static void OutputSpec(NodeType type, int index, out PortSpec result) {
            result = (type, index) switch {
                (NodeType.Scalar, 0) => new PortSpec(PortDataType.Scalar, 0),
                (NodeType.Vector, 0) => new PortSpec(PortDataType.Vector, 0),
                (NodeType.Force, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Force, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.Geometric, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Geometric, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.Curved, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Curved, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.CopyPath, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.CopyPath, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.Bridge, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Bridge, 1) => new PortSpec(PortDataType.Path, 0),
                (NodeType.Anchor, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.Reverse, 0) => new PortSpec(PortDataType.Anchor, 0),
                (NodeType.ReversePath, 0) => new PortSpec(PortDataType.Path, 0),
                _ => PortSpec.Invalid,
            };
        }

        [BurstCompile]
        public static void InputName(NodeType type, int index, out FixedString32Bytes result) {
            result = (type, index) switch {
                (NodeType.Force, 0) => "Anchor",
                (NodeType.Force, 1) => "Duration",
                (NodeType.Geometric, 0) => "Anchor",
                (NodeType.Geometric, 1) => "Duration",
                (NodeType.Curved, 0) => "Anchor",
                (NodeType.Curved, 1) => "Radius",
                (NodeType.Curved, 2) => "Arc",
                (NodeType.Curved, 3) => "Axis",
                (NodeType.Curved, 4) => "Lead In",
                (NodeType.Curved, 5) => "Lead Out",
                (NodeType.CopyPath, 0) => "Anchor",
                (NodeType.CopyPath, 1) => "Path",
                (NodeType.CopyPath, 2) => "Start",
                (NodeType.CopyPath, 3) => "End",
                (NodeType.Bridge, 0) => "Anchor",
                (NodeType.Bridge, 1) => "Target",
                (NodeType.Bridge, 2) => "Out Weight",
                (NodeType.Bridge, 3) => "In Weight",
                (NodeType.Anchor, 0) => "Position",
                (NodeType.Anchor, 1) => "Roll",
                (NodeType.Anchor, 2) => "Pitch",
                (NodeType.Anchor, 3) => "Yaw",
                (NodeType.Anchor, 4) => "Velocity",
                (NodeType.Anchor, 5) => "Heart",
                (NodeType.Anchor, 6) => "Friction",
                (NodeType.Anchor, 7) => "Resistance",
                (NodeType.Reverse, 0) => "Anchor",
                (NodeType.ReversePath, 0) => "Path",
                _ => "",
            };
        }

        [BurstCompile]
        public static void OutputName(NodeType type, int index, out FixedString32Bytes result) {
            result = (type, index) switch {
                (NodeType.Scalar, 0) => "Scalar",
                (NodeType.Vector, 0) => "Vector",
                (NodeType.Force, 0) => "Anchor",
                (NodeType.Force, 1) => "Path",
                (NodeType.Geometric, 0) => "Anchor",
                (NodeType.Geometric, 1) => "Path",
                (NodeType.Curved, 0) => "Anchor",
                (NodeType.Curved, 1) => "Path",
                (NodeType.CopyPath, 0) => "Anchor",
                (NodeType.CopyPath, 1) => "Path",
                (NodeType.Bridge, 0) => "Anchor",
                (NodeType.Bridge, 1) => "Path",
                (NodeType.Anchor, 0) => "Anchor",
                (NodeType.Reverse, 0) => "Anchor",
                (NodeType.ReversePath, 0) => "Path",
                _ => "",
            };
        }

        [BurstCompile]
        public static float DefaultInputValue(NodeType type, int index) => (type, index) switch {
            (NodeType.Force, 1) => 5f,
            (NodeType.Geometric, 1) => 5f,
            (NodeType.Curved, 1) => 20f,
            (NodeType.Curved, 2) => 90f,
            (NodeType.Curved, 3) => 0f,
            (NodeType.Curved, 4) => 0f,
            (NodeType.Curved, 5) => 0f,
            (NodeType.CopyPath, 2) => 0f,
            (NodeType.CopyPath, 3) => 1f,
            (NodeType.Bridge, 2) => 0.5f,
            (NodeType.Bridge, 3) => 0.5f,
            (NodeType.Anchor, 1) => 0f,       // Roll
            (NodeType.Anchor, 2) => 0f,       // Pitch
            (NodeType.Anchor, 3) => 0f,       // Yaw
            (NodeType.Anchor, 4) => 10f,      // Velocity
            (NodeType.Anchor, 5) => 1.1f,     // Heart
            (NodeType.Anchor, 6) => 0.021f,   // Friction
            (NodeType.Anchor, 7) => 2e-5f,    // Resistance
            _ => 0f,
        };
    }
}
