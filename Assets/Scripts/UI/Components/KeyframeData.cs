using System;

using KexEdit.Legacy;
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
    }
}
