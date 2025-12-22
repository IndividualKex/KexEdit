using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct HeartKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(HeartKeyframe keyframe) => keyframe.Value;
        public static implicit operator HeartKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
