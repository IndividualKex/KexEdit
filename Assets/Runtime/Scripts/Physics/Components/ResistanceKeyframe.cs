using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct ResistanceKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(ResistanceKeyframe keyframe) => keyframe.Value;
        public static implicit operator ResistanceKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
