using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Nodes.CopyPath {
    [BurstCompile]
    public static class CopyPathNode {
        private const int MAX_ITERATIONS = 1_000_000;

        [BurstCompile]
        public static void Build(
            in Point anchor,
            in NativeArray<Point> sourcePath,
            float start,
            float end,
            bool driven,
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

            if (sourcePath.Length < 2) return;

            int startIndex = start <= 0f ? 0 : math.clamp((int)math.round(start * Sim.HZ), 0, sourcePath.Length - 1);
            int endIndex = end < 0f ? sourcePath.Length - 1 : math.clamp((int)math.round(end * Sim.HZ), startIndex, sourcePath.Length - 1);

            if (endIndex - startIndex < 1) return;

            Point pathStart = sourcePath[startIndex];
            float3x3 pathBasis = new(pathStart.Lateral, pathStart.Normal, pathStart.Direction);
            float3x3 anchorBasis = new(anchor.Lateral, anchor.Normal, anchor.Direction);
            float3x3 rotation = math.mul(anchorBasis, math.transpose(pathBasis));
            float3 translation = anchor.SpinePosition - math.mul(rotation, pathStart.SpinePosition);
            float4x4 transform = new(
                new float4(rotation.c0, 0f),
                new float4(rotation.c1, 0f),
                new float4(rotation.c2, 0f),
                new float4(translation, 1f)
            );

            float distance = sourcePath[startIndex].HeartArc;
            float endDistance = sourcePath[endIndex].HeartArc;
            int index = 0;
            int iters = 0;

            Point state = anchor;
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            if (!driven && anchor.Velocity < Sim.MIN_VELOCITY && anchor.Frame.Pitch < 0f) {
                float centerY = anchor.Frame.HeartPosition(anchor.SpinePosition, prevHeartOffset * 0.9f).y;
                float energy = 0.5f * Sim.MIN_VELOCITY * Sim.MIN_VELOCITY + Sim.G * centerY;
                state = new Point(
                    spinePosition: anchor.SpinePosition,
                    direction: anchor.Direction,
                    normal: anchor.Normal,
                    lateral: anchor.Lateral,
                    velocity: Sim.MIN_VELOCITY,
                    energy: energy,
                    normalForce: anchor.NormalForce,
                    lateralForce: anchor.LateralForce,
                    heartArc: anchor.HeartArc,
                    spineArc: anchor.SpineArc,
                    spineAdvance: anchor.SpineAdvance,
                    frictionOrigin: anchor.HeartArc
                );
            }

            while (distance < endDistance) {
                if (iters++ > MAX_ITERATIONS) break;

                Point prev = state;
                float t = index / Sim.HZ;

                float advanceVelocity = prev.Velocity;
                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                    advanceVelocity = velocity;
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, prevHeartOffset, prevFriction, true);
                        advanceVelocity = Sim.MIN_VELOCITY;
                    }
                    else {
                        break;
                    }
                }

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                float expectedAdvancement = advanceVelocity / Sim.HZ;
                float desiredDistance = distance + expectedAdvancement;
                Project(in sourcePath, ref index, desiredDistance, startIndex, endIndex, out Point start_, out Point end_, out float interpT);

                float3 position, direction, lateral, normal;
                if (interpT < 0f) {
                    position = end_.SpinePosition;
                    direction = end_.Direction;
                    lateral = end_.Lateral;
                    normal = end_.Normal;
                }
                else {
                    position = math.lerp(start_.SpinePosition, end_.SpinePosition, interpT);
                    direction = math.normalize(math.lerp(start_.Direction, end_.Direction, interpT));
                    lateral = math.normalize(math.lerp(start_.Lateral, end_.Lateral, interpT));
                    normal = math.normalize(math.lerp(start_.Normal, end_.Normal, interpT));
                }

                position = math.transform(transform, position);
                direction = math.mul(rotation, direction);
                lateral = math.mul(rotation, lateral);
                normal = math.mul(rotation, normal);

                Frame currFrame = new(direction, normal, lateral);
                float3 currHeartPos = currFrame.HeartPosition(position, heartOffsetVal);
                float3 prevHeartPos = prev.Frame.HeartPosition(prev.SpinePosition, prevHeartOffset);
                float spineAdvance = math.distance(currHeartPos, prevHeartPos);
                float heartAdvance = math.distance(position, prev.SpinePosition);
                float heartArc = prev.HeartArc + spineAdvance;
                float spineArc = prev.SpineArc + heartAdvance;

                distance += expectedAdvancement;

                float centerY = currFrame.HeartPosition(position, heartOffsetVal * 0.9f).y;
                float frictionDistance = heartArc - state.FrictionOrigin;

                float newEnergy, newVelocity;
                if (driven) {
                    newVelocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    float prevCenterY = prev.Frame.HeartPosition(prev.SpinePosition, prevHeartOffset * 0.9f).y;
                    newEnergy = 0.5f * newVelocity * newVelocity + Sim.G * prevCenterY;
                }
                else {
                    Sim.UpdateEnergy(prev.Energy, prev.Velocity, centerY, frictionDistance, frictionVal, resistanceVal,
                        out newEnergy, out newVelocity);
                }

                ComputeForceVector(in prev, in currFrame, in position, heartAdvance, newVelocity, out float3 forceVec);
                float normalForce = -math.dot(forceVec, normal);
                float lateralForce = -math.dot(forceVec, lateral);

                state = new Point(
                    spinePosition: position,
                    direction: direction,
                    normal: normal,
                    lateral: lateral,
                    velocity: newVelocity,
                    energy: newEnergy,
                    normalForce: normalForce,
                    lateralForce: lateralForce,
                    heartArc: heartArc,
                    spineArc: spineArc,
                    spineAdvance: spineAdvance,
                    frictionOrigin: prev.FrictionOrigin,
                    rollSpeed: anchor.RollSpeed,
                    heartOffset: heartOffsetVal,
                    friction: frictionVal,
                    resistance: resistanceVal
                );

                result.Add(state);
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }
        }

        [BurstCompile]
        private static void Project(
            in NativeArray<Point> sourcePath,
            ref int index,
            float distance,
            int startIndex,
            int endIndex,
            out Point start,
            out Point end,
            out float t
        ) {
            if (distance >= sourcePath[endIndex].HeartArc) {
                index = endIndex - startIndex;
                end = sourcePath[endIndex];
                start = end;
                t = -1f;
                return;
            }

            for (int i = startIndex + index; i < endIndex; i++) {
                if (sourcePath[i + 1].HeartArc >= distance) {
                    index = i - startIndex;
                    break;
                }
            }

            start = sourcePath[startIndex + index];
            end = sourcePath[startIndex + index + 1];

            float denom = end.HeartArc - start.HeartArc;
            t = denom > Sim.EPSILON ? math.saturate((distance - start.HeartArc) / denom) : 0f;
        }

        [BurstCompile]
        private static void ComputeForceVector(
            in Point prev,
            in Frame curr,
            in float3 currPosition,
            float heartAdvance,
            float velocity,
            out float3 result
        ) {
            float roll = curr.Roll;
            float pitch = curr.Pitch;
            float yaw = curr.Yaw;
            float prevPitch = prev.Frame.Pitch;
            float prevYaw = prev.Frame.Yaw;

            float pitchFromLast = Sim.WrapAngle(pitch - prevPitch);
            float yawFromLast = Sim.WrapAngle(yaw - prevYaw);
            float yawScaleFactor = math.cos(math.abs(pitch));
            float angleFromLast = math.sqrt(yawScaleFactor * yawScaleFactor * yawFromLast * yawFromLast + pitchFromLast * pitchFromLast);

            if (math.abs(angleFromLast) < Sim.EPSILON) {
                result = math.up();
                return;
            }

            float cosRoll = math.cos(roll);
            float sinRoll = math.sin(roll);
            float normalAngle = -pitchFromLast * cosRoll - yawScaleFactor * yawFromLast * sinRoll;
            float lateralAngle = pitchFromLast * sinRoll - yawScaleFactor * yawFromLast * cosRoll;

            result = math.up()
                + velocity * Sim.HZ * lateralAngle * curr.Lateral / Sim.G
                + heartAdvance * Sim.HZ * Sim.HZ * normalAngle * curr.Normal / Sim.G;
        }
    }
}
