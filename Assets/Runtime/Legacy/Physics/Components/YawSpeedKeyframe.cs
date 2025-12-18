using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct YawSpeedKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(YawSpeedKeyframe keyframe) => keyframe.Value;
        public static implicit operator YawSpeedKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
