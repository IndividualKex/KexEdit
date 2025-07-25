using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    public static class Extensions {
        public static float GetCenter(this PointData p) {
            return p.Heart * 0.9f;
        }

        public static float ComputeEnergy(this PointData p) {
            float energy = 0.5f * p.Velocity * p.Velocity + G * p.GetHeartPosition(p.GetCenter()).y;
            energy += G * (p.TotalLength - p.FrictionCompensation) * p.Friction;
            return energy;
        }

        public static float GetPitch(this PointData p) {
            float magnitude = math.sqrt(p.Direction.x * p.Direction.x + p.Direction.z * p.Direction.z);
            return math.degrees(math.atan2(p.Direction.y, magnitude));
        }

        public static float GetYaw(this PointData p) {
            return math.degrees(math.atan2(-p.Direction.x, -p.Direction.z));
        }

        public static float3 GetHeartPosition(this PointData p, float heart) {
            return p.Position + p.Normal * heart;
        }

        public static float3 GetRelativePosition(this PointData p, float3 position) {
            return p.Position
                - position.y * p.Normal
                + position.x * p.GetHeartLateral(position.y)
                + position.z * p.GetHeartDirection(position.y);
        }

        public static float3 GetHeartDirection(this PointData p, float heart) {
            float dist;
            if (p.AngleFromLast < 1e-3f) {
                dist = p.HeartDistanceFromLast;
            }
            else {
                dist = p.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? p.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = p.Lateral * math.radians(rollSpeed * heart);
            return math.normalize(p.Direction + deviation);
        }

        public static float3 GetHeartLateral(this PointData p, float heart) {
            float dist;
            if (p.AngleFromLast < 1e-3f) {
                dist = p.HeartDistanceFromLast;
            }
            else {
                dist = p.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? p.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = -p.Direction * math.radians(rollSpeed * heart);
            return math.normalize(p.Lateral + deviation);
        }

        public static float Evaluate(this NativeArray<Keyframe> keyframes, float t, in PointData anchor) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Time) return keyframes[0].Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value;

            Keyframe start = keyframes[i];
            Keyframe end = keyframes[i + 1];

            if (start.OutInterpolation == InterpolationType.Constant) {
                return start.Value;
            }
            if (end.InInterpolation == InterpolationType.Constant) {
                return end.Value;
            }

            var interpolationType = GetMaxInterpolation(start.OutInterpolation, end.InInterpolation);

            switch (interpolationType) {
                case InterpolationType.Linear:
                    float segmentT = (t - start.Time) / (end.Time - start.Time);
                    return math.lerp(start.Value, end.Value, segmentT);
                case InterpolationType.Bezier:
                    return EvaluateBezier2D(start, end, t);
                default:
                    return start.Value;
            }
        }

        private static float EvaluateBezier2D(Keyframe start, Keyframe end, float targetTime) {
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

        private static float EvaluateKeyframes(Keyframe start, Keyframe end, float segmentT) {
            if (start.OutInterpolation == InterpolationType.Constant && segmentT < 1f) {
                return start.Value;
            }
            if (end.InInterpolation == InterpolationType.Constant) {
                return end.Value;
            }

            var interpolationType = GetMaxInterpolation(start.OutInterpolation, end.InInterpolation);

            switch (interpolationType) {
                case InterpolationType.Linear:
                    return math.lerp(start.Value, end.Value, segmentT);
                case InterpolationType.Bezier:
                    // Convert segmentT back to actual time for 2D bezier evaluation
                    float targetTime = start.Time + segmentT * (end.Time - start.Time);
                    return EvaluateBezier2D(start, end, targetTime);
                default:
                    return start.Value;
            }
        }

        private static InterpolationType GetMaxInterpolation(
            InterpolationType a,
            InterpolationType b
        ) {
            return (InterpolationType)math.max((int)a, (int)b);
        }

        public static float3 CubicBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t) {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        public static float3 CubicBezierDerivative(float3 p0, float3 p1, float3 p2, float3 p3, float t) {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        public static void GetAllKeyframes(
            this EntityManager entityManager, 
            Entity entity, 
            PropertyType propertyType, 
            NativeList<Keyframe> keyframes
        ) {
            keyframes.Clear();
            switch (propertyType) {
                case PropertyType.RollSpeed:
                    if (!entityManager.HasBuffer<RollSpeedKeyframe>(entity)) return;
                    var rollSpeedBuffer = entityManager.GetBuffer<RollSpeedKeyframe>(entity);
                    for (int i = 0; i < rollSpeedBuffer.Length; i++) {
                        keyframes.Add(rollSpeedBuffer[i].Value);
                    }
                    break;

                case PropertyType.NormalForce:
                    if (!entityManager.HasBuffer<NormalForceKeyframe>(entity)) return;
                    var normalForceBuffer = entityManager.GetBuffer<NormalForceKeyframe>(entity);
                    for (int i = 0; i < normalForceBuffer.Length; i++) {
                        keyframes.Add(normalForceBuffer[i].Value);
                    }
                    break;

                case PropertyType.LateralForce:
                    if (!entityManager.HasBuffer<LateralForceKeyframe>(entity)) return;
                    var lateralForceBuffer = entityManager.GetBuffer<LateralForceKeyframe>(entity);
                    for (int i = 0; i < lateralForceBuffer.Length; i++) {
                        keyframes.Add(lateralForceBuffer[i].Value);
                    }
                    break;

                case PropertyType.PitchSpeed:
                    if (!entityManager.HasBuffer<PitchSpeedKeyframe>(entity)) return;
                    var pitchSpeedBuffer = entityManager.GetBuffer<PitchSpeedKeyframe>(entity);
                    for (int i = 0; i < pitchSpeedBuffer.Length; i++) {
                        keyframes.Add(pitchSpeedBuffer[i].Value);
                    }
                    break;

                case PropertyType.YawSpeed:
                    if (!entityManager.HasBuffer<YawSpeedKeyframe>(entity)) return;
                    var yawSpeedBuffer = entityManager.GetBuffer<YawSpeedKeyframe>(entity);
                    for (int i = 0; i < yawSpeedBuffer.Length; i++) {
                        keyframes.Add(yawSpeedBuffer[i].Value);
                    }
                    break;

                case PropertyType.FixedVelocity:
                    if (!entityManager.HasBuffer<FixedVelocityKeyframe>(entity)) return;
                    var fixedVelocityBuffer = entityManager.GetBuffer<FixedVelocityKeyframe>(entity);
                    for (int i = 0; i < fixedVelocityBuffer.Length; i++) {
                        keyframes.Add(fixedVelocityBuffer[i].Value);
                    }
                    break;

                case PropertyType.Heart:
                    if (!entityManager.HasBuffer<HeartKeyframe>(entity)) return;
                    var heartBuffer = entityManager.GetBuffer<HeartKeyframe>(entity);
                    for (int i = 0; i < heartBuffer.Length; i++) {
                        keyframes.Add(heartBuffer[i].Value);
                    }
                    break;

                case PropertyType.Friction:
                    if (!entityManager.HasBuffer<FrictionKeyframe>(entity)) return;
                    var frictionBuffer = entityManager.GetBuffer<FrictionKeyframe>(entity);
                    for (int i = 0; i < frictionBuffer.Length; i++) {
                        keyframes.Add(frictionBuffer[i].Value);
                    }
                    break;

                case PropertyType.Resistance:
                    if (!entityManager.HasBuffer<ResistanceKeyframe>(entity)) return;
                    var resistanceBuffer = entityManager.GetBuffer<ResistanceKeyframe>(entity);
                    for (int i = 0; i < resistanceBuffer.Length; i++) {
                        keyframes.Add(resistanceBuffer[i].Value);
                    }
                    break;

                case PropertyType.TrackStyle:
                    if (!entityManager.HasBuffer<TrackStyleKeyframe>(entity)) return;
                    var trackStyleBuffer = entityManager.GetBuffer<TrackStyleKeyframe>(entity);
                    for (int i = 0; i < trackStyleBuffer.Length; i++) {
                        keyframes.Add(trackStyleBuffer[i].Value);
                    }
                    break;

                default:
                    throw new System.NotImplementedException($"GetAllKeyframes not implemented for PropertyType: {propertyType}");
            }
        }

        public static Keyframe? GetKeyframe(
            this EntityManager entityManager,
            Entity entity, 
            PropertyType propertyType, 
            uint keyframeId
        ) {
            switch (propertyType) {
                case PropertyType.RollSpeed:
                    if (!entityManager.HasBuffer<RollSpeedKeyframe>(entity)) return null;
                    var rollSpeedBuffer = entityManager.GetBuffer<RollSpeedKeyframe>(entity);
                    for (int i = 0; i < rollSpeedBuffer.Length; i++) {
                        if (rollSpeedBuffer[i].Value.Id == keyframeId) {
                            return rollSpeedBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.NormalForce:
                    if (!entityManager.HasBuffer<NormalForceKeyframe>(entity)) return null;
                    var normalForceBuffer = entityManager.GetBuffer<NormalForceKeyframe>(entity);
                    for (int i = 0; i < normalForceBuffer.Length; i++) {
                        if (normalForceBuffer[i].Value.Id == keyframeId) {
                            return normalForceBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.LateralForce:
                    if (!entityManager.HasBuffer<LateralForceKeyframe>(entity)) return null;
                    var lateralForceBuffer = entityManager.GetBuffer<LateralForceKeyframe>(entity);
                    for (int i = 0; i < lateralForceBuffer.Length; i++) {
                        if (lateralForceBuffer[i].Value.Id == keyframeId) {
                            return lateralForceBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.PitchSpeed:
                    if (!entityManager.HasBuffer<PitchSpeedKeyframe>(entity)) return null;
                    var pitchSpeedBuffer = entityManager.GetBuffer<PitchSpeedKeyframe>(entity);
                    for (int i = 0; i < pitchSpeedBuffer.Length; i++) {
                        if (pitchSpeedBuffer[i].Value.Id == keyframeId) {
                            return pitchSpeedBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.YawSpeed:
                    if (!entityManager.HasBuffer<YawSpeedKeyframe>(entity)) return null;
                    var yawSpeedBuffer = entityManager.GetBuffer<YawSpeedKeyframe>(entity);
                    for (int i = 0; i < yawSpeedBuffer.Length; i++) {
                        if (yawSpeedBuffer[i].Value.Id == keyframeId) {
                            return yawSpeedBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.FixedVelocity:
                    if (!entityManager.HasBuffer<FixedVelocityKeyframe>(entity)) return null;
                    var fixedVelocityBuffer = entityManager.GetBuffer<FixedVelocityKeyframe>(entity);
                    for (int i = 0; i < fixedVelocityBuffer.Length; i++) {
                        if (fixedVelocityBuffer[i].Value.Id == keyframeId) {
                            return fixedVelocityBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.Heart:
                    if (!entityManager.HasBuffer<HeartKeyframe>(entity)) return null;
                    var heartBuffer = entityManager.GetBuffer<HeartKeyframe>(entity);
                    for (int i = 0; i < heartBuffer.Length; i++) {
                        if (heartBuffer[i].Value.Id == keyframeId) {
                            return heartBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.Friction:
                    if (!entityManager.HasBuffer<FrictionKeyframe>(entity)) return null;
                    var frictionBuffer = entityManager.GetBuffer<FrictionKeyframe>(entity);
                    for (int i = 0; i < frictionBuffer.Length; i++) {
                        if (frictionBuffer[i].Value.Id == keyframeId) {
                            return frictionBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.Resistance:
                    if (!entityManager.HasBuffer<ResistanceKeyframe>(entity)) return null;
                    var resistanceBuffer = entityManager.GetBuffer<ResistanceKeyframe>(entity);
                    for (int i = 0; i < resistanceBuffer.Length; i++) {
                        if (resistanceBuffer[i].Value.Id == keyframeId) {
                            return resistanceBuffer[i].Value;
                        }
                    }
                    return null;

                case PropertyType.TrackStyle:
                    if (!entityManager.HasBuffer<TrackStyleKeyframe>(entity)) return null;
                    var trackStyleBuffer = entityManager.GetBuffer<TrackStyleKeyframe>(entity);
                    for (int i = 0; i < trackStyleBuffer.Length; i++) {
                        if (trackStyleBuffer[i].Value.Id == keyframeId) {
                            return trackStyleBuffer[i].Value;
                        }
                    }
                    return null;

                default:
                    throw new System.NotImplementedException($"GetKeyframe not implemented for PropertyType: {propertyType}");
            }
        }

        public static float Evaluate(this DynamicBuffer<RollSpeedKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.RollSpeed.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<NormalForceKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.NormalForce.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<LateralForceKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.LateralForce.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<PitchSpeedKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.PitchSpeed.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<YawSpeedKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.YawSpeed.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<FixedVelocityKeyframe> keyframes, float t, in PointData anchor) {
            float defaultValue = PropertyType.FixedVelocity.Previous(t, anchor);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<HeartKeyframe> keyframes, float t, in PointData anchor) {
            float defaultValue = PropertyType.Heart.Previous(t, anchor);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<FrictionKeyframe> keyframes, float t, in PointData anchor) {
            float defaultValue = PropertyType.Friction.Previous(t, anchor);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Evaluate(this DynamicBuffer<ResistanceKeyframe> keyframes, float t, in PointData anchor) {
            float defaultValue = PropertyType.Resistance.Previous(t, anchor);
            if (keyframes.Length == 0) return defaultValue;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

            Keyframe start = keyframes[i].Value;
            Keyframe end = keyframes[i + 1].Value;
            float segmentT = (t - start.Time) / (end.Time - start.Time);
            return EvaluateKeyframes(start, end, segmentT);
        }

        public static float Default(this PropertyType type, float t) {
            PointData defaultPoint = default;
            return type switch {
                PropertyType.RollSpeed => 0f,
                PropertyType.NormalForce => 1f,
                PropertyType.LateralForce => 0f,
                PropertyType.PitchSpeed => 0f,
                PropertyType.YawSpeed => 0f,
                PropertyType.FixedVelocity => defaultPoint.Velocity,
                PropertyType.Heart => defaultPoint.Heart,
                PropertyType.Friction => defaultPoint.Friction,
                PropertyType.Resistance => defaultPoint.Resistance,
                PropertyType.TrackStyle => 0f,
                _ => throw new System.Exception($"Invalid property type: {type}")
            };
        }

        public static float Previous(this PropertyType type, float t, PointData anchor) {
            return type switch {
                PropertyType.RollSpeed => anchor.RollSpeed,
                PropertyType.NormalForce => anchor.NormalForce,
                PropertyType.LateralForce => anchor.LateralForce,
                PropertyType.PitchSpeed => math.radians(anchor.PitchFromLast * HZ),
                PropertyType.YawSpeed => math.radians(anchor.YawFromLast * HZ),
                PropertyType.FixedVelocity => anchor.Velocity,
                PropertyType.Heart => anchor.Heart,
                PropertyType.Friction => anchor.Friction,
                PropertyType.Resistance => anchor.Resistance,
                PropertyType.TrackStyle => 0f,
                _ => throw new System.Exception($"Invalid property type: {type}")
            };
        }

        public static float Previous(this PropertyType type, float t, PointData anchor, DurationType durationType) {
            return type switch {
                PropertyType.RollSpeed => anchor.RollSpeed,
                PropertyType.NormalForce => anchor.NormalForce,
                PropertyType.LateralForce => anchor.LateralForce,
                PropertyType.PitchSpeed => durationType == DurationType.Distance
                    ? math.radians(anchor.PitchFromLast * HZ) / math.max(anchor.Velocity, MIN_VELOCITY)
                    : math.radians(anchor.PitchFromLast * HZ),
                PropertyType.YawSpeed => durationType == DurationType.Distance
                    ? math.radians(anchor.YawFromLast * HZ) / math.max(anchor.Velocity, MIN_VELOCITY)
                    : math.radians(anchor.YawFromLast * HZ),
                PropertyType.FixedVelocity => anchor.Velocity,
                PropertyType.Heart => anchor.Heart,
                PropertyType.Friction => anchor.Friction,
                PropertyType.Resistance => anchor.Resistance,
                PropertyType.TrackStyle => 0f,
                _ => throw new System.Exception($"Invalid property type: {type}")
            };
        }

        public static float Evaluate(this DynamicBuffer<TrackStyleKeyframe> keyframes, float t) {
            float defaultValue = PropertyType.TrackStyle.Default(t);
            if (keyframes.Length == 0) return defaultValue;
            
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;
            if (t >= keyframes[^1].Value.Time) return keyframes[^1].Value.Value;

            for (int i = 0; i < keyframes.Length - 1; i++) {
                var current = keyframes[i].Value;
                var next = keyframes[i + 1].Value;
                
                if (t >= current.Time && t < next.Time) {
                    return current.Value;
                }
            }

            return keyframes[^1].Value.Value;
        }
    }
}
