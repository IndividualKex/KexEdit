using KexEdit.Sim;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Sim.Nodes.CopyPath {
    public static class CopyPathPorts {
        public const int Anchor = 0;
        public const int Path = 1;
        public const int Start = 2;
        public const int End = 3;
    }

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
            float3 translation = anchor.HeartPosition - math.mul(rotation, pathStart.HeartPosition);
            float4x4 transform = new(
                new float4(rotation.c0, 0f),
                new float4(rotation.c1, 0f),
                new float4(rotation.c2, 0f),
                new float4(translation, 1f)
            );

            float distance = sourcePath[startIndex].SpineArc;
            float endDistance = sourcePath[endIndex].SpineArc;
            int index = 0;
            int iters = 0;

            Point state = anchor;
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            while (distance < endDistance) {
                if (iters++ > MAX_ITERATIONS) break;

                Point prev = state;
                float t = index / Sim.HZ;

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                float advanceVelocity = prev.Velocity;
                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, heartOffsetVal, frictionVal, true);
                    advanceVelocity = velocity;
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, heartOffsetVal, frictionVal, true);
                        advanceVelocity = Sim.MIN_VELOCITY;
                    }
                    else {
                        break;
                    }
                }

                float expectedAdvancement = advanceVelocity / Sim.HZ;
                float desiredDistance = distance + expectedAdvancement;
                Project(in sourcePath, ref index, desiredDistance, startIndex, endIndex, out Point start_, out Point end_, out float interpT);

                float3 position, direction, lateral, normal;
                if (interpT < 0f) {
                    position = end_.HeartPosition;
                    direction = end_.Direction;
                    lateral = end_.Lateral;
                    normal = end_.Normal;
                    distance = endDistance;
                }
                else {
                    position = math.lerp(start_.HeartPosition, end_.HeartPosition, interpT);
                    direction = math.normalize(math.lerp(start_.Direction, end_.Direction, interpT));
                    lateral = math.normalize(math.lerp(start_.Lateral, end_.Lateral, interpT));
                    normal = math.normalize(math.lerp(start_.Normal, end_.Normal, interpT));
                    distance += expectedAdvancement;
                }

                position = math.transform(transform, position);
                direction = math.mul(rotation, direction);
                lateral = math.mul(rotation, lateral);
                normal = math.mul(rotation, normal);

                Frame currFrame = new(direction, normal, lateral);
                float3 currSpinePos = currFrame.SpinePosition(position, heartOffsetVal);
                float3 prevSpinePos = prev.Frame.SpinePosition(prev.HeartPosition, prevHeartOffset);
                float spineAdvance = math.distance(currSpinePos, prevSpinePos);
                float heartAdvance = math.distance(position, prev.HeartPosition);
                float heartArc = prev.HeartArc + heartAdvance;
                float spineArc = prev.SpineArc + spineAdvance;

                // Use delta-based velocity update for numerical stability
                float newVelocity;
                if (driven) {
                    newVelocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                }
                else {
                    float prevCenterY = prev.Frame.SpinePosition(prev.HeartPosition, prevHeartOffset * 0.9f).y;
                    float currCenterY = currFrame.SpinePosition(position, heartOffsetVal * 0.9f).y;
                    float deltaY = currCenterY - prevCenterY;
                    newVelocity = Sim.UpdateVelocity(
                        prev.Velocity,
                        deltaY,
                        spineAdvance,
                        frictionVal,
                        resistanceVal
                    );
                }

                ComputeForceVector(in prev, in currFrame, in position, heartAdvance, newVelocity, out float3 forceVec);
                float normalForce = -math.dot(forceVec, normal);
                float lateralForce = -math.dot(forceVec, lateral);

                state = new Point(
                    heartPosition: position,
                    direction: direction,
                    normal: normal,
                    lateral: lateral,
                    velocity: newVelocity,
                    normalForce: normalForce,
                    lateralForce: lateralForce,
                    heartArc: heartArc,
                    spineArc: spineArc,
                    heartAdvance: heartAdvance,
                    frictionOrigin: prev.FrictionOrigin,
                    rollSpeed: anchor.RollSpeed,
                    heartOffset: heartOffsetVal,
                    friction: frictionVal,
                    resistance: resistanceVal
                );

                if (state.Velocity > Sim.MAX_VELOCITY) break;
                float forceMag = math.sqrt(normalForce * normalForce + lateralForce * lateralForce);
                if (forceMag > Sim.MAX_FORCE) break;

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
            if (distance >= sourcePath[endIndex].SpineArc) {
                index = endIndex - startIndex;
                end = sourcePath[endIndex];
                start = end;
                t = -1f;
                return;
            }

            for (int i = startIndex + index; i < endIndex; i++) {
                if (sourcePath[i + 1].SpineArc >= distance) {
                    index = i - startIndex;
                    break;
                }
            }

            start = sourcePath[startIndex + index];
            end = sourcePath[startIndex + index + 1];

            float denom = end.SpineArc - start.SpineArc;
            t = denom > Sim.EPSILON ? math.saturate((distance - start.SpineArc) / denom) : 0f;
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
