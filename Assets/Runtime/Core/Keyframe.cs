using Unity.Burst;

namespace KexEdit.Core {
    public enum InterpolationType { Constant, Linear, Bezier }

    [BurstCompile]
    public readonly struct Keyframe {
        public readonly float Time;
        public readonly float Value;
        public readonly InterpolationType InInterpolation;
        public readonly InterpolationType OutInterpolation;
        public readonly float InTangent;
        public readonly float OutTangent;
        public readonly float InWeight;
        public readonly float OutWeight;

        public Keyframe(
            float time,
            float value,
            InterpolationType inInterpolation = InterpolationType.Bezier,
            InterpolationType outInterpolation = InterpolationType.Bezier,
            float inTangent = 0f,
            float outTangent = 0f,
            float inWeight = 1f / 3f,
            float outWeight = 1f / 3f
        ) {
            Time = time;
            Value = value;
            InInterpolation = inInterpolation;
            OutInterpolation = outInterpolation;
            InTangent = inTangent;
            OutTangent = outTangent;
            InWeight = inWeight;
            OutWeight = outWeight;
        }
    }
}
