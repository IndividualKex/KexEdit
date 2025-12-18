using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct NormalForceKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(NormalForceKeyframe keyframe) => keyframe.Value;
        public static implicit operator NormalForceKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
