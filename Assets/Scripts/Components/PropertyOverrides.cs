using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct PropertyOverrides : IComponentData {
        public PropertyOverrideFlags Flags;

        public bool FixedVelocity {
            get => (Flags & PropertyOverrideFlags.FixedVelocity) != 0;
            set => Flags = value ? Flags | PropertyOverrideFlags.FixedVelocity : Flags & ~PropertyOverrideFlags.FixedVelocity;
        }

        public bool Heart {
            get => (Flags & PropertyOverrideFlags.Heart) != 0;
            set => Flags = value ? Flags | PropertyOverrideFlags.Heart : Flags & ~PropertyOverrideFlags.Heart;
        }

        public bool Friction {
            get => (Flags & PropertyOverrideFlags.Friction) != 0;
            set => Flags = value ? Flags | PropertyOverrideFlags.Friction : Flags & ~PropertyOverrideFlags.Friction;
        }

        public bool Resistance {
            get => (Flags & PropertyOverrideFlags.Resistance) != 0;
            set => Flags = value ? Flags | PropertyOverrideFlags.Resistance : Flags & ~PropertyOverrideFlags.Resistance;
        }

        public bool TrackStyle {
            get => (Flags & PropertyOverrideFlags.TrackStyle) != 0;
            set => Flags = value ? Flags | PropertyOverrideFlags.TrackStyle : Flags & ~PropertyOverrideFlags.TrackStyle;
        }

        public static PropertyOverrides Default => new() {
            Flags = PropertyOverrideFlags.None
        };
    }
}
