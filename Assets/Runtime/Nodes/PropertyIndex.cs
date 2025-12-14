namespace KexEdit.Nodes {
    public static class PropertyIndex {
        public static int ToIndex(PropertyId property, NodeType node) => (property, node) switch {
            // Force: 0-6
            (PropertyId.RollSpeed, NodeType.Force) => 0,
            (PropertyId.NormalForce, NodeType.Force) => 1,
            (PropertyId.LateralForce, NodeType.Force) => 2,
            (PropertyId.DrivenVelocity, NodeType.Force) => 3,
            (PropertyId.HeartOffset, NodeType.Force) => 4,
            (PropertyId.Friction, NodeType.Force) => 5,
            (PropertyId.Resistance, NodeType.Force) => 6,

            // Geometric: 0-6 (PitchSpeed/YawSpeed at indices 1,2)
            (PropertyId.RollSpeed, NodeType.Geometric) => 0,
            (PropertyId.PitchSpeed, NodeType.Geometric) => 1,
            (PropertyId.YawSpeed, NodeType.Geometric) => 2,
            (PropertyId.DrivenVelocity, NodeType.Geometric) => 3,
            (PropertyId.HeartOffset, NodeType.Geometric) => 4,
            (PropertyId.Friction, NodeType.Geometric) => 5,
            (PropertyId.Resistance, NodeType.Geometric) => 6,

            // Curved: 0-4
            (PropertyId.RollSpeed, NodeType.Curved) => 0,
            (PropertyId.DrivenVelocity, NodeType.Curved) => 1,
            (PropertyId.HeartOffset, NodeType.Curved) => 2,
            (PropertyId.Friction, NodeType.Curved) => 3,
            (PropertyId.Resistance, NodeType.Curved) => 4,

            // CopyPath: 0-3
            (PropertyId.DrivenVelocity, NodeType.CopyPath) => 0,
            (PropertyId.HeartOffset, NodeType.CopyPath) => 1,
            (PropertyId.Friction, NodeType.CopyPath) => 2,
            (PropertyId.Resistance, NodeType.CopyPath) => 3,

            // Bridge: 0-4
            (PropertyId.DrivenVelocity, NodeType.Bridge) => 0,
            (PropertyId.HeartOffset, NodeType.Bridge) => 1,
            (PropertyId.Friction, NodeType.Bridge) => 2,
            (PropertyId.Resistance, NodeType.Bridge) => 3,
            (PropertyId.TrackStyle, NodeType.Bridge) => 4,

            _ => -1,
        };

        public static PropertyId FromIndex(int index, NodeType node) => (index, node) switch {
            // Force
            (0, NodeType.Force) => PropertyId.RollSpeed,
            (1, NodeType.Force) => PropertyId.NormalForce,
            (2, NodeType.Force) => PropertyId.LateralForce,
            (3, NodeType.Force) => PropertyId.DrivenVelocity,
            (4, NodeType.Force) => PropertyId.HeartOffset,
            (5, NodeType.Force) => PropertyId.Friction,
            (6, NodeType.Force) => PropertyId.Resistance,

            // Geometric
            (0, NodeType.Geometric) => PropertyId.RollSpeed,
            (1, NodeType.Geometric) => PropertyId.PitchSpeed,
            (2, NodeType.Geometric) => PropertyId.YawSpeed,
            (3, NodeType.Geometric) => PropertyId.DrivenVelocity,
            (4, NodeType.Geometric) => PropertyId.HeartOffset,
            (5, NodeType.Geometric) => PropertyId.Friction,
            (6, NodeType.Geometric) => PropertyId.Resistance,

            // Curved
            (0, NodeType.Curved) => PropertyId.RollSpeed,
            (1, NodeType.Curved) => PropertyId.DrivenVelocity,
            (2, NodeType.Curved) => PropertyId.HeartOffset,
            (3, NodeType.Curved) => PropertyId.Friction,
            (4, NodeType.Curved) => PropertyId.Resistance,

            // CopyPath
            (0, NodeType.CopyPath) => PropertyId.DrivenVelocity,
            (1, NodeType.CopyPath) => PropertyId.HeartOffset,
            (2, NodeType.CopyPath) => PropertyId.Friction,
            (3, NodeType.CopyPath) => PropertyId.Resistance,

            // Bridge
            (0, NodeType.Bridge) => PropertyId.DrivenVelocity,
            (1, NodeType.Bridge) => PropertyId.HeartOffset,
            (2, NodeType.Bridge) => PropertyId.Friction,
            (3, NodeType.Bridge) => PropertyId.Resistance,
            (4, NodeType.Bridge) => PropertyId.TrackStyle,

            _ => (PropertyId)255,
        };
    }
}
