using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Nodes.Force {
    [BurstCompile]
    public static class ForceNode {
        private const float MIN_VELOCITY = 1e-3f;
        private const int MAX_ITERATIONS = 1_000_000;

        [BurstCompile]
        private static void StepByForces(in Frame prev, float normalForce, float lateralForce, float velocity, float spineAdvance, out Frame result) {
            float3 forceVec = -normalForce * prev.Normal - lateralForce * prev.Lateral + math.down();
            float normalAccel = -math.dot(forceVec, prev.Normal) * Sim.G;
            float lateralAccel = -math.dot(forceVec, prev.Lateral) * Sim.G;

            float estimatedVelocity = math.abs(spineAdvance) < Sim.EPSILON
                ? velocity
                : spineAdvance * Sim.HZ;
            if (math.abs(estimatedVelocity) < Sim.EPSILON) estimatedVelocity = Sim.EPSILON;
            float safeVelocity = math.abs(velocity) < Sim.EPSILON ? Sim.EPSILON : velocity;

            float3 newDirection = math.normalize(math.mul(
                math.mul(
                    quaternion.AxisAngle(prev.Lateral, normalAccel / estimatedVelocity / Sim.HZ),
                    quaternion.AxisAngle(prev.Normal, -lateralAccel / safeVelocity / Sim.HZ)
                ),
                prev.Direction
            ));
            float3 newLateral = math.normalize(math.mul(
                quaternion.AxisAngle(prev.Normal, -lateralAccel / safeVelocity / Sim.HZ),
                prev.Lateral
            ));
            float3 newNormal = math.normalize(math.cross(newDirection, newLateral));

            result = new Frame(newDirection, newNormal, newLateral);
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

            float newEnergy = prev.Energy;
            float newVelocity = prev.Velocity;

            if (!physics.Driven) {
                float centerY = (currHeartPosition + 0.9f * physics.HeartOffset * currNormal).y;
                float frictionDistance = newHeartArc - prev.FrictionOrigin;
                Sim.UpdateEnergy(
                    prev.Energy, prev.Velocity, centerY,
                    frictionDistance, physics.Friction, physics.Resistance,
                    out newEnergy, out newVelocity
                );
            }

            Frame currFrame = new(currDirection, currNormal, currLateral);
            Curvature curvature = Curvature.FromFrames(in currFrame, in prevFrame);
            Forces forces = Forces.Compute(in curvature, in currFrame, newVelocity, spineAdvance);

            result = new Point(
                heartPosition: currHeartPosition,
                direction: currDirection,
                normal: currNormal,
                lateral: currLateral,
                velocity: newVelocity,
                energy: newEnergy,
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
                float anchorHeartArc = anchor.HeartArc;
                BuildDistanceSection(
                    config.Duration, driven, anchorHeartArc,
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

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < MIN_VELOCITY) {
                        break;
                    }
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                }
                else if (prev.Velocity < MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(MIN_VELOCITY, prevHeartOffset, prevFriction, true);
                    }
                    else {
                        break;
                    }
                }

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                float targetNormalForce = KeyframeEvaluator.Evaluate(in normalForce, t, 1f);
                float targetLateralForce = KeyframeEvaluator.Evaluate(in lateralForce, t, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, t, 0f);
                float deltaRoll = rollSpeedVal / Sim.HZ;

                PhysicsParams physics = new(heartOffsetVal, frictionVal, resistanceVal, deltaRoll, driven);
                Advance(in prev, targetNormalForce, targetLateralForce, in physics, rollSpeedVal, out Point curr);

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
            float anchorHeartArc,
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

            float endLength = anchorHeartArc + duration;
            int iterations = 0;

            while (state.HeartArc < endLength) {
                if (iterations++ > MAX_ITERATIONS) {
                    break;
                }

                Point prev = state;
                float d = prev.HeartArc - anchorHeartArc + prev.Velocity / Sim.HZ;

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, d, prev.Velocity);
                    if (velocity < MIN_VELOCITY) {
                        break;
                    }
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                }
                else if (prev.Velocity < MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(MIN_VELOCITY, prevHeartOffset, prevFriction, true);
                    }
                    else {
                        break;
                    }
                }

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, d, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, d, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, d, anchorResistance);

                float targetNormalForce = KeyframeEvaluator.Evaluate(in normalForce, d, 1f);
                float targetLateralForce = KeyframeEvaluator.Evaluate(in lateralForce, d, 0f);
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, d, 0f);
                float deltaRoll = rollSpeedVal * (prev.Velocity / Sim.HZ);

                PhysicsParams physics = new(heartOffsetVal, frictionVal, resistanceVal, deltaRoll, driven);
                Advance(in prev, targetNormalForce, targetLateralForce, in physics, rollSpeedVal, out Point curr);

                result.Add(curr);
                state = curr;
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }
        }
    }
}
