using System;

namespace KexEdit.UI {
    public enum UnitsType {
        None,
        Time,
        Distance,
        Angle,
        AnglePerTime,
        AnglePerDistance,
        Force,
        Velocity,
        Resistance
    }

    [Flags]
    public enum InteractionState {
        None = 0,
        Hovered = 1 << 0,
        Selected = 1 << 1
    }

    [Flags]
    public enum PortState {
        None = 0,
        Hovered = 1 << 0,
        Dragging = 1 << 1,
        Connected = 1 << 2
    }

    public enum TargetValueType {
        Roll,
        Pitch,
        Yaw,
        X,
        Y,
        Z,
        NormalForce,
        LateralForce,
    }

    public enum DistanceUnitsType {
        Meters,
        Feet,
    }

    public enum AngleUnitsType {
        Degrees,
        Radians,
    }

    public enum AngleChangeUnitsType {
        Degrees,
        Radians,
    }

    public enum SpeedUnitsType {
        MetersPerSecond,
        KilometersPerHour,
        MilesPerHour,
    }

    public enum KeyframeFieldType {
        Value,
        Time,
        InWeight,
        InTangent,
        OutWeight,
        OutTangent
    }

    public enum SkyType {
        Solid,
        Procedural
    }

    public enum VisualizationGradientType {
        TwoColorPositive,
        ThreeColorCrossesZero
    }
}
