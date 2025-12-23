using System;

using KexEdit.Legacy;
using CoreKeyframe = KexEdit.Sim.Keyframe;

namespace KexEdit.UI {
    [Serializable]
    public struct KeyframeData {
        public PropertyType Type;
        public Keyframe Value;
        public float Offset;

        public KeyframeData(PropertyType type, Keyframe value) {
            Type = type;
            Value = value;
            Offset = 0f;
        }

        public KeyframeData(PropertyType type, Keyframe value, float offset) {
            Type = type;
            Value = value;
            Offset = offset;
        }

        public KeyframeData(PropertyType type, CoreKeyframe coreKeyframe) {
            Type = type;
            Value = new Keyframe {
                Time = coreKeyframe.Time,
                Value = coreKeyframe.Value,
                InInterpolation = (InterpolationType)coreKeyframe.InInterpolation,
                OutInterpolation = (InterpolationType)coreKeyframe.OutInterpolation,
                InTangent = coreKeyframe.InTangent,
                OutTangent = coreKeyframe.OutTangent,
                InWeight = coreKeyframe.InWeight,
                OutWeight = coreKeyframe.OutWeight,
                Selected = false
            };
            Offset = 0f;
        }
    }
}
