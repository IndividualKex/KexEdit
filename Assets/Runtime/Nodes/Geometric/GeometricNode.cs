using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Nodes.Geometric {
    [BurstCompile]
    public static class GeometricNode {
        private const int MAX_ITERATIONS = 1_000_000;

        [BurstCompile]
        public static void Build(
            in Point anchor,
            in IterationConfig config,
            bool driven,
            bool steering,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> pitchSpeed,
            in NativeArray<Keyframe> yawSpeed,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref NativeList<Point> result
        ) {
            result.Clear();
            result.Add(anchor);

            Point state = anchor;
            float accumulatedRoll = 0f;

            if (config.DurationType == DurationType.Time) {
                BuildTimeSection(
                    config.Duration, driven, steering,
                    in rollSpeed, in pitchSpeed, in yawSpeed,
                    in drivenVelocity, in heartOffset, in friction, in resistance,
                    anchorHeart, anchorFriction, anchorResistance,
                    ref state, ref accumulatedRoll, ref result
                );
            }
            else {
                float anchorHeartArc = anchor.HeartArc;
                BuildDistanceSection(
                    config.Duration, driven, steering, anchorHeartArc,
                    in rollSpeed, in pitchSpeed, in yawSpeed,
                    in drivenVelocity, in heartOffset, in friction, in resistance,
                    anchorHeart, anchorFriction, anchorResistance,
                    ref state, ref accumulatedRoll, ref result
                );
            }
        }

        [BurstCompile]
        private static void BuildTimeSection(
            float duration,
            bool driven,
            bool steering,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> pitchSpeed,
            in NativeArray<Keyframe> yawSpeed,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref Point state,
            ref float accumulatedRoll,
            ref NativeList<Point> result
        ) {
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            int pointCount = (int)math.floor(Sim.HZ * duration);
            for (int i = 1; i < pointCount; i++) {
                float t = i / Sim.HZ;

                Point prev = state;

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, prevHeartOffset, prevFriction, true);
                    }
                    else break;
                }

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                float pitchSpeedVal = KeyframeEvaluator.Evaluate(in pitchSpeed, t, 0f);
                float yawSpeedVal = KeyframeEvaluator.Evaluate(in yawSpeed, t, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, t, 0f);

                float deltaRoll = rollSpeedVal / Sim.HZ;
                float deltaPitch = pitchSpeedVal / Sim.HZ;
                float deltaYaw = yawSpeedVal / Sim.HZ;

                StepGeometric(
                    in prev, heartOffsetVal, frictionVal, resistanceVal,
                    deltaRoll, deltaPitch, deltaYaw, driven, steering, rollSpeedVal,
                    ref accumulatedRoll, out Point curr
                );

                result.Add(curr);
                state = curr;
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }
        }

        [BurstCompile]
        private static void BuildDistanceSection(
            float duration,
            bool driven,
            bool steering,
            float anchorHeartArc,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> pitchSpeed,
            in NativeArray<Keyframe> yawSpeed,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref Point state,
            ref float accumulatedRoll,
            ref NativeList<Point> result
        ) {
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            float endLength = anchorHeartArc + duration;
            int iterations = 0;

            while (state.HeartArc < endLength) {
                if (iterations++ > MAX_ITERATIONS) break;

                Point prev = state;
                float d = prev.HeartArc - anchorHeartArc + prev.Velocity / Sim.HZ;

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, d, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, prevHeartOffset, prevFriction, true);
                    }
                    else break;
                }

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, d, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, d, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, d, anchorResistance);

                float pitchSpeedVal = KeyframeEvaluator.Evaluate(in pitchSpeed, d, 0f);
                float yawSpeedVal = KeyframeEvaluator.Evaluate(in yawSpeed, d, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, d, 0f);

                float deltaRoll = rollSpeedVal * (prev.Velocity / Sim.HZ);
                float deltaPitch = pitchSpeedVal * (prev.Velocity / Sim.HZ);
                float deltaYaw = yawSpeedVal * (prev.Velocity / Sim.HZ);

                StepGeometric(
                    in prev, heartOffsetVal, frictionVal, resistanceVal,
                    deltaRoll, deltaPitch, deltaYaw, driven, steering, rollSpeedVal,
                    ref accumulatedRoll, out Point curr
                );

                result.Add(curr);
                state = curr;
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }
        }

        [BurstCompile]
        private static void StepGeometric(
            in Point prev,
            float heartOffsetVal,
            float frictionVal,
            float resistanceVal,
            float deltaRoll,
            float deltaPitch,
            float deltaYaw,
            bool driven,
            bool steering,
            float rollSpeedVal,
            ref float accumulatedRoll,
            out Point result
        ) {
            Frame prevFrame = prev.Frame;
            float3 prevDirection = prev.Direction;
            float3 prevSpinePosition = prev.SpinePosition;

            float3 currDirection;
            float3 currLateral;
            float3 currNormal;
            float3 currSpinePosition;

            if (steering) {
                Frame unrolledFrame = prevFrame;
                if (math.abs(accumulatedRoll) > Sim.EPSILON) {
                    unrolledFrame = prevFrame.WithRoll(-accumulatedRoll);
                }

                float3 up = unrolledFrame.Normal.y >= 0f ? math.up() : -math.up();
                float3 pitchAxis = math.normalize(math.cross(up, prevDirection));
                Frame rotated = unrolledFrame.RotateAround(pitchAxis, deltaPitch).WithYaw(deltaYaw);
                currDirection = rotated.Direction;
                float3 lateralUnrolled = rotated.Lateral;
                float3 normalUnrolled = rotated.Normal;

                float halfStepDistance = prev.Velocity / (2f * Sim.HZ);
                currSpinePosition = prevSpinePosition
                    + currDirection * halfStepDistance
                    + prevDirection * halfStepDistance;

                accumulatedRoll += deltaRoll;

                if (math.abs(accumulatedRoll) > Sim.EPSILON) {
                    Frame rerolled = new Frame(currDirection, normalUnrolled, lateralUnrolled).WithRoll(accumulatedRoll);
                    currLateral = rerolled.Lateral;
                    currNormal = rerolled.Normal;
                }
                else {
                    currLateral = lateralUnrolled;
                    currNormal = normalUnrolled;
                }
            }
            else {
                Frame rotated = prevFrame.WithPitch(deltaPitch).WithYaw(deltaYaw);
                currDirection = rotated.Direction;
                currNormal = rotated.Normal;
                currLateral = rotated.Lateral;

                float halfStepDistance = prev.Velocity / (2f * Sim.HZ);
                float3 prevHeartPos = prev.HeartPosition(heartOffsetVal);
                float3 currHeartPosIfSpineStatic = prevSpinePosition + currNormal * heartOffsetVal;

                currSpinePosition = prevSpinePosition
                    + currDirection * halfStepDistance
                    + prevDirection * halfStepDistance
                    + (prevHeartPos - currHeartPosIfSpineStatic);

                Frame rolled = rotated.WithRoll(deltaRoll);
                currLateral = rolled.Lateral;
                currNormal = rolled.Normal;
            }

            float heartAdvance = math.distance(
                currSpinePosition + currNormal * heartOffsetVal,
                prev.HeartPosition(heartOffsetVal)
            );
            float newHeartArc = prev.HeartArc + heartAdvance;
            float spineAdvance = math.distance(currSpinePosition, prevSpinePosition);
            float newSpineArc = prev.SpineArc + spineAdvance;

            float newEnergy = prev.Energy;
            float newVelocity = prev.Velocity;

            if (!driven) {
                float centerY = (currSpinePosition + 0.9f * heartOffsetVal * currNormal).y;
                float frictionDistance = newHeartArc - prev.FrictionOrigin;
                Sim.UpdateEnergy(
                    prev.Energy, prev.Velocity, centerY,
                    frictionDistance, frictionVal, resistanceVal,
                    out newEnergy, out newVelocity
                );
            }

            Frame currFrame = new(currDirection, currNormal, currLateral);
            Curvature curvature = Curvature.FromFrames(in currFrame, in prevFrame);
            Forces forces = Forces.Compute(in curvature, in currFrame, newVelocity, spineAdvance);

            result = new Point(
                spinePosition: currSpinePosition,
                direction: currDirection,
                normal: currNormal,
                lateral: currLateral,
                velocity: newVelocity,
                energy: newEnergy,
                normalForce: forces.Normal,
                lateralForce: forces.Lateral,
                heartArc: newHeartArc,
                spineArc: newSpineArc,
                spineAdvance: spineAdvance,
                frictionOrigin: prev.FrictionOrigin,
                rollSpeed: rollSpeedVal,
                heartOffset: heartOffsetVal,
                friction: frictionVal,
                resistance: resistanceVal
            );
        }
    }
}
