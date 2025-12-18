using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct TrackStyleKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(TrackStyleKeyframe keyframe) => keyframe.Value;
        public static implicit operator TrackStyleKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}