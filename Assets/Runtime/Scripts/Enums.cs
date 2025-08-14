using System;

namespace KexEdit {
    public enum DurationType {
        Time,
        Distance,
    }

    public enum InterpolationType {
        Constant,
        Linear,
        Bezier,
    }

    public enum HandleType {
        Free,
        Aligned,
    }

    public enum EasingType {
        Sine,
        Quadratic,
        Cubic,
        Quartic,
        Quintic,
        Exponential,
    }

    public enum NodeType {
        ForceSection,
        GeometricSection,
        CurvedSection,
        CopyPathSection,
        Anchor,
        Reverse,
        ReversePath,
        Bridge,
        Mesh,
        Append,
    }

    public enum PortType {
        Anchor,
        Path,
        Duration,
        Position,
        Roll,
        Pitch,
        Yaw,
        Velocity,
        Heart,
        Friction,
        Resistance,
        Radius,
        Arc,
        Axis,
        LeadIn,
        LeadOut,
        InWeight,
        OutWeight,
        Rotation,
        Scale,
        Start,
        End,
    }

    public enum PropertyType {
        RollSpeed,
        NormalForce,
        LateralForce,
        PitchSpeed,
        YawSpeed,
        FixedVelocity,
        Heart,
        Friction,
        Resistance,
        TrackStyle,
    }

    [Flags]
    public enum PropertyOverrideFlags : byte {
        None = 0,
        FixedVelocity = 1 << 0,  // 0x01
        Heart = 1 << 1,          // 0x02  
        Friction = 1 << 2,       // 0x04
        Resistance = 1 << 3,     // 0x08
        TrackStyle = 1 << 4,     // 0x10
        All = FixedVelocity | Heart | Friction | Resistance | TrackStyle
    }

    [Flags]
    public enum NodeFlags : byte {
        None = 0,
        Render = 1 << 0,      // 0x01
        Selected = 1 << 1,    // 0x02
        // Reserve bits for future serialization flags
        Reserved1 = 1 << 2,   // 0x04
        Reserved2 = 1 << 3,   // 0x08
        Reserved3 = 1 << 4,   // 0x10
        Reserved4 = 1 << 5,   // 0x20
        Reserved5 = 1 << 6,   // 0x40
        Reserved6 = 1 << 7,   // 0x80
    }

    [Flags]
    public enum KeyframeFlags : byte {
        None = 0,
        LockTime = 1 << 0,    // 0x01
        LockValue = 1 << 1,   // 0x02
        // Reserve bits for future keyframe flags
        Reserved1 = 1 << 2,   // 0x04
        Reserved2 = 1 << 3,   // 0x08
        Reserved3 = 1 << 4,   // 0x10
        Reserved4 = 1 << 5,   // 0x20
        Reserved5 = 1 << 6,   // 0x40
        Reserved6 = 1 << 7,   // 0x80
    }

    public enum VisualizationMode {
        None,
        Velocity,
        NormalForce,
        LateralForce,
        RollSpeed,
        PitchSpeed,
        YawSpeed,
        Curvature,
    }
}
