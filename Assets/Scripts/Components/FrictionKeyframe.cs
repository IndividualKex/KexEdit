using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct FrictionKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(FrictionKeyframe keyframe) => keyframe.Value;
        public static implicit operator FrictionKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
