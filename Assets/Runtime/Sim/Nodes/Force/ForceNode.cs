using KexEdit.Sim;
using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Sim.Nodes.Force {
    public static class ForcePorts {
        public const int Anchor = 0;
        public const int Duration = 1;
    }

    [BurstCompile]
    public static class ForceNode {
        private const int MAX_ITERATIONS = 1_000_000;
        private const float MAX_ANGLE_RATE = 0.5f;

        [BurstCompile]
        private static void StepByForces(in Frame prev, float normalForce, float lateralForce, float velocity, float spineAdvance, out Frame result) {
            float3 forceVec = -normalForce * prev.Normal - lateralForce * prev.Lateral + math.down();
            float normalAccel = -math.dot(forceVec, prev.Normal) * Sim.G;
            float lateralAccel = -math.dot(forceVec, prev.Lateral) * Sim.G;

            float estimatedVelocity = math.abs(spineAdvance) < Sim.EPSILON
                ? velocity
                : spineAdvance * Sim.HZ;

            float safeEstimated = math.max(math.abs(estimatedVelocity), Sim.MIN_VELOCITY);
            float safeVelocity = math.max(math.abs(velocity), Sim.MIN_VELOCITY);

            float normalAngle = math.clamp(normalAccel / safeEstimated / Sim.HZ, -MAX_ANGLE_RATE, MAX_ANGLE_RATE);
            float lateralAngle = math.clamp(-lateralAccel / safeVelocity / Sim.HZ, -MAX_ANGLE_RATE, MAX_ANGLE_RATE);

            quaternion qNormal = quaternion.AxisAngle(prev.Lateral, normalAngle);
            quaternion qLateral = quaternion.AxisAngle(prev.Normal, lateralAngle);
            quaternion combined = math.mul(qNormal, qLateral);

            float3 newDirection = math.normalize(math.mul(combined, prev.Direction));
            float3 newLateral = math.normalize(math.mul(qLateral, prev.Lateral));
            float3 newNormal = math.normalize(math.cross(newDirection, newLateral));

            result = new Frame(newDirection, newNormal, newLateral).Reorthonormalize();
        }

        [BurstCompile]
        private static void Advance(
            in Point prev,
            float targetNormalForce,
            float targetLateralForce,
            in PhysicsParams physics,
            float rollSpeedVal,
            out Point result
        ) {
            Frame prevFrame = prev.Frame;
            StepByForces(in prevFrame, targetNormalForce, targetLateralForce, prev.Velocity, prev.HeartAdvance, out Frame newFrame);

            float3 currDirection = newFrame.Direction;
            float3 currLateral = newFrame.Lateral;
            float3 currNormal = newFrame.Normal;

            float halfStepDistance = prev.Velocity / (2f * Sim.HZ);
            float3 prevSpinePos = prev.SpinePosition(physics.HeartOffset);
            float3 currSpinePosIfHeartStatic = prev.HeartPosition + currNormal * physics.HeartOffset;

            float3 currHeartPosition = prev.HeartPosition;
            currHeartPosition += currDirection * halfStepDistance
                + prev.Direction * halfStepDistance
                + (prevSpinePos - currSpinePosIfHeartStatic);

            Frame rolledFrame = newFrame.WithRoll(physics.DeltaRoll);
            currLateral = rolledFrame.Lateral;
            currNormal = rolledFrame.Normal;

            float spineAdvance = math.distance(currHeartPosition + currNormal * physics.HeartOffset, prev.SpinePosition(physics.HeartOffset));
            float heartAdvance = math.distance(currHeartPosition, prev.HeartPosition);
            float newHeartArc = prev.HeartArc + heartAdvance;
            float newSpineArc = prev.SpineArc + spineAdvance;

            // Use delta-based velocity update for numerical stability
            float newVelocity = prev.Velocity;
            if (!physics.Driven) {
                float prevCenterY = (prev.HeartPosition + 0.9f * physics.HeartOffset * prev.Normal).y;
                float currCenterY = (currHeartPosition + 0.9f * physics.HeartOffset * currNormal).y;
                float deltaY = currCenterY - prevCenterY;
                newVelocity = Sim.UpdateVelocity(
                    prev.Velocity,
                    deltaY,
                    spineAdvance,
                    physics.Friction,
                    physics.Resistance
                );
            }

            Frame currFrame = new(currDirection, currNormal, currLateral);
            Curvature curvature = Curvature.FromFrames(in currFrame, in prevFrame);
            Forces forces = Forces.Compute(in curvature, in currFrame, newVelocity, heartAdvance);

            result = new Point(
                heartPosition: currHeartPosition,
                direction: currDirection,
                normal: currNormal,
                lateral: currLateral,
                velocity: newVelocity,
                normalForce: forces.Normal,
                lateralForce: forces.Lateral,
                heartArc: newHeartArc,
                spineArc: newSpineArc,
                heartAdvance: heartAdvance,
                frictionOrigin: prev.FrictionOrigin,
                rollSpeed: rollSpeedVal,
                heartOffset: physics.HeartOffset,
                friction: physics.Friction,
                resistance: physics.Resistance
            );
        }

        [BurstCompile]
        public static void Build(
            in Point anchor,
            in IterationConfig config,
            bool driven,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> normalForce,
            in NativeArray<Keyframe> lateralForce,
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

            if (config.DurationType == DurationType.Time) {
                BuildTimeSection(
                    config.Duration, driven,
                    in rollSpeed, in normalForce, in lateralForce,
                    in drivenVelocity, in heartOffset, in friction, in resistance,
                    anchorHeart, anchorFriction, anchorResistance,
                    ref state, ref result
                );
            }
            else {
                float anchorSpineArc = anchor.SpineArc;
                BuildDistanceSection(
                    config.Duration, driven, anchorSpineArc,
                    in rollSpeed, in normalForce, in lateralForce,
                    in drivenVelocity, in heartOffset, in friction, in resistance,
                    anchorHeart, anchorFriction, anchorResistance,
                    ref state, ref result
                );
            }
        }

        [BurstCompile]
        private static void BuildTimeSection(
            float duration,
            bool driven,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> normalForce,
            in NativeArray<Keyframe> lateralForce,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref Point state,
            ref NativeList<Point> result
        ) {
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            int pointCount = (int)math.floor(Sim.HZ * duration);
            for (int i = 1; i < pointCount; i++) {
                float t = i / Sim.HZ;

                Point prev = state;

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) {
                        break;
                    }
                    prev = prev.WithVelocity(velocity, heartOffsetVal, frictionVal, true);
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, heartOffsetVal, frictionVal, true);
                    }
                    else {
                        break;
                    }
                }

                float targetNormalForce = KeyframeEvaluator.Evaluate(in normalForce, t, 1f);
                float targetLateralForce = KeyframeEvaluator.Evaluate(in lateralForce, t, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, t, 0f);
                float deltaRoll = rollSpeedVal / Sim.HZ;

                PhysicsParams physics = new(heartOffsetVal, frictionVal, resistanceVal, deltaRoll, driven);
                Advance(in prev, targetNormalForce, targetLateralForce, in physics, rollSpeedVal, out Point curr);

                if (curr.Velocity > Sim.MAX_VELOCITY) break;
                float forceMag = math.sqrt(curr.NormalForce * curr.NormalForce + curr.LateralForce * curr.LateralForce);
                if (forceMag > Sim.MAX_FORCE) break;

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
            float anchorSpineArc,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> normalForce,
            in NativeArray<Keyframe> lateralForce,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref Point state,
            ref NativeList<Point> result
        ) {
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            float endLength = anchorSpineArc + duration;
            int iterations = 0;

            while (state.SpineArc < endLength) {
                if (iterations++ > MAX_ITERATIONS) {
                    break;
                }

                Point prev = state;
                float d = prev.SpineArc - anchorSpineArc + prev.Velocity / Sim.HZ;

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, d, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, d, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, d, anchorResistance);

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, d, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) {
                        break;
                    }
                    prev = prev.WithVelocity(velocity, heartOffsetVal, frictionVal, true);
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, heartOffsetVal, frictionVal, true);
                    }
                    else {
                        break;
                    }
                }

                float targetNormalForce = KeyframeEvaluator.Evaluate(in normalForce, d, 1f);
                float targetLateralForce = KeyframeEvaluator.Evaluate(in lateralForce, d, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, d, 0f);
                float deltaRoll = rollSpeedVal * (prev.Velocity / Sim.HZ);

                PhysicsParams physics = new(heartOffsetVal, frictionVal, resistanceVal, deltaRoll, driven);
                Advance(in prev, targetNormalForce, targetLateralForce, in physics, rollSpeedVal, out Point curr);

                if (curr.Velocity > Sim.MAX_VELOCITY) break;
                float forceMag = math.sqrt(curr.NormalForce * curr.NormalForce + curr.LateralForce * curr.LateralForce);
                if (forceMag > Sim.MAX_FORCE) break;

                result.Add(curr);
                state = curr;
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }
        }
    }
}
