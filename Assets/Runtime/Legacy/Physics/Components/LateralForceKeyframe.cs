using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct LateralForceKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(LateralForceKeyframe keyframe) => keyframe.Value;
        public static implicit operator LateralForceKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
