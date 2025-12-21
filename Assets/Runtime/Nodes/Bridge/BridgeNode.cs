using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Nodes.Bridge {
    [BurstCompile]
    public static class BridgeNode {
        private const int MAX_ITERATIONS = 1_000_000;

        [BurstCompile]
        public static void Build(
            in Point anchor,
            in Point targetAnchor,
            float inWeight,
            float outWeight,
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

            float3 vector = targetAnchor.HeartPosition - anchor.HeartPosition;
            float length = math.length(vector);

            if (length < Sim.EPSILON) return;

            float clampedOutWeight = math.clamp(outWeight, 1e-3f, 1f);
            float clampedInWeight = math.clamp(inWeight, 1e-3f, 1f);

            var bridgePath = new NativeList<PathPoint>(Allocator.Temp);
            CreateBridgePath(ref bridgePath, in anchor, in targetAnchor, length, clampedOutWeight, clampedInWeight);

            if (bridgePath.Length < 2) {
                bridgePath.Dispose();
                return;
            }

            float pathDistance = 0f;
            float endDistance = bridgePath[bridgePath.Length - 1].TotalLength;
            int pathIndex = 0;
            int iters = 0;

            Point state = anchor;
            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            while (pathDistance < endDistance) {
                if (iters++ > MAX_ITERATIONS) break;

                Point prev = state;
                float t = (result.Length - 1) / Sim.HZ;

                float advanceVelocity = prev.Velocity;
                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, prevHeartOffset, prevFriction, true);
                    advanceVelocity = velocity;
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
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
                float desiredDistance = pathDistance + expectedAdvancement;

                Project(in bridgePath, ref pathIndex, desiredDistance, out PathPoint start, out PathPoint end, out float interpT);

                float3 position, direction, lateral, normal;
                float roll;
                if (interpT < 0f) {
                    position = end.Position;
                    direction = end.Direction;
                    lateral = end.Lateral;
                    normal = end.Normal;
                    roll = end.Roll;
                    pathDistance = bridgePath[bridgePath.Length - 1].TotalLength;
                }
                else {
                    position = math.lerp(start.Position, end.Position, interpT);
                    direction = math.normalize(math.lerp(start.Direction, end.Direction, interpT));
                    lateral = math.normalize(math.lerp(start.Lateral, end.Lateral, interpT));
                    normal = math.normalize(math.lerp(start.Normal, end.Normal, interpT));

                    float rollDiff = end.Roll - start.Roll;
                    if (rollDiff > math.PI) rollDiff -= 2f * math.PI;
                    if (rollDiff < -math.PI) rollDiff += 2f * math.PI;
                    roll = start.Roll + rollDiff * interpT;
                    pathDistance += expectedAdvancement;
                }

                Frame currFrame = new(direction, normal, lateral);
                float3 currSpinePos = currFrame.SpinePosition(position, heartOffsetVal);
                float3 prevSpinePos = prev.Frame.SpinePosition(prev.HeartPosition, prevHeartOffset);
                float spineAdvance = math.distance(currSpinePos, prevSpinePos);
                float heartAdvance = math.distance(position, prev.HeartPosition);
                float heartArc = prev.HeartArc + heartAdvance;
                float spineArc = prev.SpineArc + spineAdvance;

                float centerY = currFrame.SpinePosition(position, heartOffsetVal * 0.9f).y;
                float frictionDistance = heartArc - state.FrictionOrigin;

                float newEnergy, newVelocity;
                if (driven) {
                    newVelocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    float prevCenterY = prev.Frame.SpinePosition(prev.HeartPosition, prevHeartOffset * 0.9f).y;
                    newEnergy = 0.5f * newVelocity * newVelocity + Sim.G * prevCenterY;
                }
                else {
                    Sim.UpdateEnergy(prev.Energy, prev.Velocity, centerY, frictionDistance, frictionVal, resistanceVal,
                        out newEnergy, out newVelocity);
                }

                ComputeForceVector(in prev, in currFrame, spineAdvance, newVelocity, out float3 forceVec);
                float normalForce = -math.dot(forceVec, normal);
                float lateralForce = -math.dot(forceVec, lateral);

                state = new Point(
                    heartPosition: position,
                    direction: direction,
                    normal: normal,
                    lateral: lateral,
                    velocity: newVelocity,
                    energy: newEnergy,
                    normalForce: normalForce,
                    lateralForce: lateralForce,
                    heartArc: heartArc,
                    spineArc: spineArc,
                    heartAdvance: heartAdvance,
                    frictionOrigin: state.FrictionOrigin,
                    rollSpeed: anchor.RollSpeed,
                    heartOffset: heartOffsetVal,
                    friction: frictionVal,
                    resistance: resistanceVal
                );

                result.Add(state);
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
            }

            bridgePath.Dispose();
        }

        private struct PathPoint {
            public float3 Position;
            public float3 Direction;
            public float3 Lateral;
            public float3 Normal;
            public float Roll;
            public float TotalLength;
        }

        [BurstCompile]
        private static void CreateBridgePath(
            ref NativeList<PathPoint> path,
            in Point source,
            in Point target,
            float length,
            float outWeight,
            float inWeight
        ) {
            int pathPoints = math.max(10, (int)(length * 2f));

            float3 p0 = source.HeartPosition;
            float3 p1 = source.HeartPosition + source.Direction * (length * outWeight);
            float3 p2 = target.HeartPosition - target.Direction * (length * inWeight);
            float3 p3 = target.HeartPosition;

            float sourceRoll = source.Frame.Roll;
            float targetRoll = target.Frame.Roll;

            for (int i = 0; i <= pathPoints; i++) {
                float t = (float)i / pathPoints;

                CubicBezier(in p0, in p1, in p2, in p3, t, out float3 position);
                CubicBezierDerivative(in p0, in p1, in p2, in p3, t, out float3 direction);
                PathPoint pathPoint = new() {
                    Position = position,
                    Direction = math.normalize(direction)
                };

                float rollDiff = targetRoll - sourceRoll;
                if (rollDiff > math.PI) rollDiff -= 2f * math.PI;
                if (rollDiff < -math.PI) rollDiff += 2f * math.PI;
                pathPoint.Roll = sourceRoll + rollDiff * Smoothstep(t);

                pathPoint.Lateral = math.normalize(math.cross(pathPoint.Direction, math.up()));
                if (math.length(pathPoint.Lateral) < Sim.EPSILON) {
                    pathPoint.Lateral = math.right();
                }
                pathPoint.Normal = math.normalize(math.cross(pathPoint.Direction, pathPoint.Lateral));

                if (math.abs(pathPoint.Roll) > Sim.EPSILON) {
                    quaternion rollQuat = quaternion.AxisAngle(pathPoint.Direction, -pathPoint.Roll);
                    pathPoint.Lateral = math.normalize(math.mul(rollQuat, pathPoint.Lateral));
                    pathPoint.Normal = math.normalize(math.cross(pathPoint.Direction, pathPoint.Lateral));
                }

                pathPoint.TotalLength = i == 0 ? 0f :
                    path[i - 1].TotalLength + math.distance(pathPoint.Position, path[i - 1].Position);

                path.Add(pathPoint);
            }
        }

        [BurstCompile]
        private static void Project(
            in NativeList<PathPoint> path,
            ref int pathIndex,
            float distance,
            out PathPoint start,
            out PathPoint end,
            out float t
        ) {
            if (distance >= path[^1].TotalLength) {
                pathIndex = path.Length - 1;
                end = path[^1];
                start = end;
                t = -1f;
                return;
            }

            for (int i = pathIndex; i < path.Length - 1; i++) {
                if (path[i + 1].TotalLength >= distance) {
                    pathIndex = i;
                    break;
                }
            }

            start = path[pathIndex];
            end = path[pathIndex + 1];
            float denom = end.TotalLength - start.TotalLength;
            t = denom > Sim.EPSILON ? math.saturate((distance - start.TotalLength) / denom) : 0f;
        }

        [BurstCompile]
        private static void ComputeForceVector(
            in Point prev,
            in Frame curr,
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

        [BurstCompile]
        private static void CubicBezier(in float3 p0, in float3 p1, in float3 p2, in float3 p3, float t, out float3 result) {
            float u = 1f - t;
            float uu = u * u;
            float uuu = uu * u;
            float tt = t * t;
            float ttt = tt * t;
            result = uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        [BurstCompile]
        private static void CubicBezierDerivative(in float3 p0, in float3 p1, in float3 p2, in float3 p3, float t, out float3 result) {
            float u = 1f - t;
            result = 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        [BurstCompile]
        private static float Smoothstep(float t) {
            return t * t * (3f - 2f * t);
        }
    }
}
