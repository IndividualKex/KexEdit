using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct FixedVelocityKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(FixedVelocityKeyframe keyframe) => keyframe.Value;
        public static implicit operator FixedVelocityKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
