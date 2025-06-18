using System;

namespace KexEdit.UI {
    public enum UnitsType {
        None,
        Meters,
        Radians,
        Seconds,
        MetersPerSecond,
        RadiansPerSecond,
        RadiansPerMeter,
        Gs,
        OneOverMicrometers
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
    }
}
