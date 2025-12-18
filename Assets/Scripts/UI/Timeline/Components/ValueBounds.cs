using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI.Timeline {
    public struct ValueBounds {
        public float Min;
        public float Max;

        public float Range => Max - Min;

        public ValueBounds(float min, float max) {
            Min = min;
            Max = max;
        }

        public static ValueBounds Default => new(0f, 1f);

        public float Clamp(float value) {
            return Mathf.Clamp(value, Min, Max);
        }

        public void Pan(float amount) {
            Min += amount;
            Max += amount;
        }
    }
}
