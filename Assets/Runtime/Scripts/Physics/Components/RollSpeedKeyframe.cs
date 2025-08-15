using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct RollSpeedKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(RollSpeedKeyframe keyframe) => keyframe.Value;
        public static implicit operator RollSpeedKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
