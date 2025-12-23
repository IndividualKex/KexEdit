using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Sim {
    [BurstCompile]
    public static class KeyframeEvaluator {
        [BurstCompile]
        public static float Evaluate(in NativeArray<Keyframe> keyframes, float t, float defaultValue = 0f) {
            if (!keyframes.IsCreated || keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Time) return keyframes[0].Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value;

            var start = keyframes[i];
            var end = keyframes[i + 1];
            return EvaluateSegment(in start, in end, t);
        }

        [BurstCompile]
        public static float EvaluateSegment(in Keyframe start, in Keyframe end, float t) {
            if (start.OutInterpolation == InterpolationType.Constant) {
                return start.Value;
            }

            var interpolationType = GetMaxInterpolation(start.OutInterpolation, end.InInterpolation);

            switch (interpolationType) {
                case InterpolationType.Linear:
                    float segmentT = (t - start.Time) / (end.Time - start.Time);
                    return math.lerp(start.Value, end.Value, segmentT);
                case InterpolationType.Bezier:
                    return EvaluateBezier2D(in start, in end, t);
                default:
                    return start.Value;
            }
        }

        [BurstCompile]
        private static float EvaluateBezier2D(in Keyframe start, in Keyframe end, float targetTime) {
            float dt = end.Time - start.Time;

            float p0X = start.Time;
            float p0Y = start.Value;
            float p1X = start.Time + (dt * start.OutWeight);
            float p1Y = start.Value + (start.OutTangent * dt * start.OutWeight);
            float p2X = end.Time - (dt * end.InWeight);
            float p2Y = end.Value - (end.InTangent * dt * end.InWeight);
            float p3X = end.Time;
            float p3Y = end.Value;

            float u = (targetTime - start.Time) / dt;
            for (int iter = 0; iter < 8; iter++) {
                float oneMinusU = 1f - u;
                float oneMinusU2 = oneMinusU * oneMinusU;
                float oneMinusU3 = oneMinusU2 * oneMinusU;
                float u2 = u * u;
                float u3 = u2 * u;

                float bezierTime = oneMinusU3 * p0X
                    + 3f * oneMinusU2 * u * p1X
                    + 3f * oneMinusU * u2 * p2X
                    + u3 * p3X;

                float timeDiff = bezierTime - targetTime;
                if (math.abs(timeDiff) < 1e-6f) break;

                float bezierTimeDerivative = -3f * oneMinusU2 * p0X
                    + 3f * oneMinusU2 * p1X - 6f * oneMinusU * u * p1X
                    + 6f * oneMinusU * u * p2X - 3f * u2 * p2X
                    + 3f * u2 * p3X;

                if (math.abs(bezierTimeDerivative) < 1e-9f) break;

                u -= timeDiff / bezierTimeDerivative;
                u = math.clamp(u, 0f, 1f);
            }

            float finalOneMinusU = 1f - u;
            float finalOneMinusU2 = finalOneMinusU * finalOneMinusU;
            float finalOneMinusU3 = finalOneMinusU2 * finalOneMinusU;
            float finalU2 = u * u;
            float finalU3 = finalU2 * u;

            return finalOneMinusU3 * p0Y
                + 3f * finalOneMinusU2 * u * p1Y
                + 3f * finalOneMinusU * finalU2 * p2Y
                + finalU3 * p3Y;
        }

        [BurstCompile]
        private static InterpolationType GetMaxInterpolation(InterpolationType a, InterpolationType b) {
            return (InterpolationType)math.max((int)a, (int)b);
        }
    }
}
