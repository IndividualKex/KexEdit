using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildBridgeSystem : ISystem {
        private ComponentLookup<AnchorPort> _anchorPortLookup;

        public void OnCreate(ref SystemState state) {
            _anchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true);
        }

        public void OnUpdate(ref SystemState state) {
            _anchorPortLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = _anchorPortLookup,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, BridgeAspect section) {
                if (!section.Dirty) return;

                if (section.InputPorts.Length < 2 ||
                    !AnchorPortLookup.TryGetComponent(section.InputPorts[1], out var targetAnchor)) {
                    UnityEngine.Debug.LogError("BuildBridgeSystem: Missing source or target anchor port");
                    return;
                }

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                PointData source = section.Anchor;
                PointData target = targetAnchor;

                float3 vector = target.Position - source.Position;
                float length = math.length(vector);

                if (length < EPSILON) {
                    UnityEngine.Debug.LogWarning("BuildBridgeSystem: Bridge length is too small");
                    return;
                }

                var bridgePath = new NativeList<PointData>(Allocator.Temp);
                CreateBridgePath(ref bridgePath, source, target, length);

                if (bridgePath.Length < 2) {
                    bridgePath.Dispose();
                    return;
                }

                float pathDistance = 0f;
                float endDistance = bridgePath[^1].TotalLength;
                int pathIndex = 0;
                int iters = 0;
                int index = 0;

                while (pathDistance < endDistance) {
                    if (iters++ > 1e6) {
                        UnityEngine.Debug.LogError("BuildBridgeSystem: Infinite loop");
                        break;
                    }

                    PointData prev = section.Points[^1];
                    float t = index / HZ;

                    if (section.FixedVelocity) {
                        float velocity = section.FixedVelocityKeyframes.Evaluate(t, section.Anchor);
                        if (velocity < MIN_VELOCITY) {
                            UnityEngine.Debug.LogWarning("BuildBridgeSystem: Velocity is too low");
                            break;
                        }
                        prev.SetVelocity(velocity, true);
                    }
                    else if (prev.Velocity < MIN_VELOCITY) {
                        float pitch = prev.GetPitch();
                        if (pitch < -EPSILON) {
                            prev.SetVelocity(MIN_VELOCITY, true);
                        }
                        else {
                            UnityEngine.Debug.LogWarning("BuildBridgeSystem: Velocity is too low");
                            break;
                        }
                    }

                    prev.Heart = section.HeartKeyframes.Evaluate(t, section.Anchor);
                    prev.Friction = section.FrictionKeyframes.Evaluate(t, section.Anchor);
                    prev.Resistance = section.ResistanceKeyframes.Evaluate(t, section.Anchor);

                    PointData curr = prev;

                    UpdateBridgePoint(section, ref curr, prev, ref bridgePath, ref pathIndex, ref pathDistance);

                    section.Points.Add(curr);
                    index++;
                }

                bridgePath.Dispose();

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildBridgeSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }
                section.Dirty = false;
            }

            private void CreateBridgePath(ref NativeList<PointData> path, PointData source, PointData target, float length) {
                int pathPoints = math.max(10, (int)(length * 2f));

                float3 p0 = source.Position;
                float3 p1 = source.Position + source.Direction * (length * 0.3f);
                float3 p2 = target.Position - target.Direction * (length * 0.3f);
                float3 p3 = target.Position;

                for (int i = 0; i <= pathPoints; i++) {
                    float t = (float)i / pathPoints;

                    PointData pathPoint = new() {
                        Position = Extensions.CubicBezier(p0, p1, p2, p3, t),
                        Direction = math.normalize(Extensions.CubicBezierDerivative(p0, p1, p2, p3, t)),
                        Facing = source.Facing
                    };

                    float rollDiff = target.Roll - source.Roll;
                    if (rollDiff > 180f) rollDiff -= 360f;
                    if (rollDiff < -180f) rollDiff += 360f;
                    pathPoint.Roll = source.Roll + rollDiff * math.smoothstep(0f, 1f, t);

                    pathPoint.Lateral = math.normalize(math.cross(pathPoint.Direction, math.up()));
                    if (math.length(pathPoint.Lateral) < EPSILON) {
                        pathPoint.Lateral = math.right();
                    }
                    pathPoint.Normal = math.normalize(math.cross(pathPoint.Direction, pathPoint.Lateral));

                    if (math.abs(pathPoint.Roll) > EPSILON) {
                        quaternion rollQuat = quaternion.AxisAngle(pathPoint.Direction, math.radians(-pathPoint.Roll));
                        pathPoint.Lateral = math.normalize(math.mul(rollQuat, pathPoint.Lateral));
                        pathPoint.Normal = math.normalize(math.cross(pathPoint.Direction, pathPoint.Lateral));
                    }

                    pathPoint.TotalLength = i == 0 ? 0f :
                        path[i - 1].TotalLength + math.distance(pathPoint.Position, path[i - 1].Position);

                    path.Add(pathPoint);
                }
            }

            private void UpdateBridgePoint(
                BridgeAspect section,
                ref PointData curr,
                in PointData prev,
                ref NativeList<PointData> path,
                ref int pathIndex,
                ref float pathDistance
            ) {
                float expectedAdvancement = prev.Velocity / HZ;
                float desiredDistance = pathDistance + expectedAdvancement;
                var (start, end, t) = Project(ref path, ref pathIndex, desiredDistance);

                if (t == -1f) {
                    curr.Position = end.Position;
                    curr.Direction = end.Direction;
                    curr.Lateral = end.Lateral;
                    curr.Normal = end.Normal;
                    curr.Roll = end.Roll;
                    pathDistance = path[^1].TotalLength;
                }
                else {
                    curr.Position = math.lerp(start.Position, end.Position, t);
                    curr.Direction = math.normalize(math.lerp(start.Direction, end.Direction, t));
                    curr.Lateral = math.normalize(math.lerp(start.Lateral, end.Lateral, t));
                    curr.Normal = math.normalize(math.lerp(start.Normal, end.Normal, t));

                    float rollDiff = end.Roll - start.Roll;
                    if (rollDiff > 180f) rollDiff -= 360f;
                    if (rollDiff < -180f) rollDiff += 360f;
                    curr.Roll = start.Roll + rollDiff * t;
                }

                curr.Facing = section.Anchor.Facing;

                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(curr.Heart), prev.GetHeartPosition(prev.Heart));
                curr.TotalLength += curr.DistanceFromLast;
                pathDistance += expectedAdvancement;
                curr.HeartDistanceFromLast = math.distance(curr.Position, prev.Position);
                curr.TotalHeartLength += curr.HeartDistanceFromLast;

                float3 diff = curr.Direction - prev.Direction;
                if (math.length(diff) < EPSILON) {
                    curr.PitchFromLast = 0f;
                    curr.YawFromLast = 0f;
                }
                else {
                    curr.PitchFromLast = (curr.GetPitch() - prev.GetPitch() + 540) % 360 - 180;
                    curr.YawFromLast = (curr.GetYaw() - prev.GetYaw() + 540) % 360 - 180;
                }
                float yawScaleFactor = math.cos(math.abs(math.radians(curr.GetPitch())));
                curr.AngleFromLast = math.sqrt(yawScaleFactor * yawScaleFactor * curr.YawFromLast * curr.YawFromLast + curr.PitchFromLast * curr.PitchFromLast);

                if (!section.FixedVelocity) {
                    float pe = G * (curr.GetHeartPosition(curr.GetCenter()).y + (curr.TotalLength - curr.FrictionCompensation) * curr.Friction);
                    curr.Energy -= curr.Velocity * curr.Velocity * curr.Velocity * curr.Resistance / HZ;
                    curr.Velocity = math.sqrt(2f * math.max(0, curr.Energy - pe));
                }

                float3 forceVec;
                if (math.abs(curr.AngleFromLast) < EPSILON) {
                    forceVec = math.up();
                }
                else {
                    float cosRoll = math.cos(math.radians(curr.Roll));
                    float sinRoll = math.sin(math.radians(curr.Roll));
                    float normalAngle = math.radians(-curr.PitchFromLast * cosRoll
                        - yawScaleFactor * curr.YawFromLast * sinRoll);
                    float lateralAngle = math.radians(curr.PitchFromLast * sinRoll
                        - yawScaleFactor * curr.YawFromLast * cosRoll);
                    forceVec = math.up()
                        + curr.Velocity * HZ * lateralAngle * curr.Lateral / G
                        + curr.HeartDistanceFromLast * HZ * HZ * normalAngle * curr.Normal / G;
                }
                curr.NormalForce = -math.dot(forceVec, curr.Normal);
                curr.LateralForce = -math.dot(forceVec, curr.Lateral);
            }

            private (PointData, PointData, float) Project(
                ref NativeList<PointData> path,
                ref int pathIndex,
                float distance
            ) {
                if (distance >= path[^1].TotalLength) {
                    pathIndex = path.Length - 1;
                    var end = path[^1];
                    return (end, end, -1f);
                }

                for (int i = pathIndex; i < path.Length - 1; i++) {
                    if (path[i + 1].TotalLength >= distance) {
                        pathIndex = i;
                        break;
                    }
                }

                var start = path[pathIndex];
                var endPoint = path[pathIndex + 1];
                float t = (distance - start.TotalLength) / (endPoint.TotalLength - start.TotalLength);
                return (start, endPoint, math.saturate(t));
            }
        }
    }
}
