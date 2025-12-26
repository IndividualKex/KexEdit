using System;
using KexEdit.Legacy;
using KexEdit.Nodes;

namespace KexEdit.UI.Timeline {
    public static class PropertyMapping {
        public static Nodes.NodeType ToNodeType(Legacy.NodeType type) => type switch {
            Legacy.NodeType.ForceSection => Nodes.NodeType.Force,
            Legacy.NodeType.GeometricSection => Nodes.NodeType.Geometric,
            Legacy.NodeType.CurvedSection => Nodes.NodeType.Curved,
            Legacy.NodeType.CopyPathSection => Nodes.NodeType.CopyPath,
            Legacy.NodeType.Anchor => Nodes.NodeType.Anchor,
            Legacy.NodeType.Reverse => Nodes.NodeType.Reverse,
            Legacy.NodeType.ReversePath => Nodes.NodeType.ReversePath,
            Legacy.NodeType.Bridge => Nodes.NodeType.Bridge,
            _ => Nodes.NodeType.Scalar
        };

        public static PropertyId ToPropertyId(PropertyType type) => type switch {
            PropertyType.RollSpeed => PropertyId.RollSpeed,
            PropertyType.NormalForce => PropertyId.NormalForce,
            PropertyType.LateralForce => PropertyId.LateralForce,
            PropertyType.PitchSpeed => PropertyId.PitchSpeed,
            PropertyType.YawSpeed => PropertyId.YawSpeed,
            PropertyType.FixedVelocity => PropertyId.DrivenVelocity,
            PropertyType.Heart => PropertyId.HeartOffset,
            PropertyType.Friction => PropertyId.Friction,
            PropertyType.Resistance => PropertyId.Resistance,
            PropertyType.TrackStyle => PropertyId.TrackStyle,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported PropertyType")
        };

        public static PropertyType ToPropertyType(PropertyId id) => id switch {
            PropertyId.RollSpeed => PropertyType.RollSpeed,
            PropertyId.NormalForce => PropertyType.NormalForce,
            PropertyId.LateralForce => PropertyType.LateralForce,
            PropertyId.PitchSpeed => PropertyType.PitchSpeed,
            PropertyId.YawSpeed => PropertyType.YawSpeed,
            PropertyId.DrivenVelocity => PropertyType.FixedVelocity,
            PropertyId.HeartOffset => PropertyType.Heart,
            PropertyId.Friction => PropertyType.Friction,
            PropertyId.Resistance => PropertyType.Resistance,
            PropertyId.TrackStyle => PropertyType.TrackStyle,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unsupported PropertyId")
        };
    }
}
