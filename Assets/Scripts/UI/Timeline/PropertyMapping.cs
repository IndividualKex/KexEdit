using System;
using KexEdit.Legacy;
using KexEdit.Sim.Schema;
using SchemaNodeType = KexEdit.Sim.Schema.NodeType;

namespace KexEdit.UI.Timeline {
    public static class PropertyMapping {
        public static SchemaNodeType ToNodeType(Legacy.NodeType type) => type switch {
            Legacy.NodeType.ForceSection => SchemaNodeType.Force,
            Legacy.NodeType.GeometricSection => SchemaNodeType.Geometric,
            Legacy.NodeType.CurvedSection => SchemaNodeType.Curved,
            Legacy.NodeType.CopyPathSection => SchemaNodeType.CopyPath,
            Legacy.NodeType.Anchor => SchemaNodeType.Anchor,
            Legacy.NodeType.Reverse => SchemaNodeType.Reverse,
            Legacy.NodeType.ReversePath => SchemaNodeType.ReversePath,
            Legacy.NodeType.Bridge => SchemaNodeType.Bridge,
            _ => SchemaNodeType.Scalar
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
