using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildCopyPathSectionSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
                StartPortLookup = SystemAPI.GetComponentLookup<StartPort>(true),
                EndPortLookup = SystemAPI.GetComponentLookup<EndPort>(true),
                PathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            [ReadOnly]
            public ComponentLookup<StartPort> StartPortLookup;

            [ReadOnly]
            public ComponentLookup<EndPort> EndPortLookup;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, CopyPathSectionAspect section) {
                if (!section.Dirty) return;

                if (section.InputPorts.Length != 4) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: Expected 4 input ports (anchor, path, start, end)");
                    return;
                }

                if (!PathPortLookup.TryGetBuffer(section.InputPorts[1], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No path port found");
                    return;
                }

                if (!StartPortLookup.TryGetComponent(section.InputPorts[2], out var startPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No start port found");
                    return;
                }

                if (!EndPortLookup.TryGetComponent(section.InputPorts[3], out var endPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No end port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                float startTime = startPort.Value;
                float endTime = endPort.Value;

                float pathDuration = (pathBuffer.Length - 1) / HZ;
                int startIndex = startTime <= 0f ? 0 : math.clamp((int)(startTime * HZ), 0, pathBuffer.Length - 1);
                int endIndex = endTime < 0f ? pathBuffer.Length - 1 : math.clamp((int)(endTime * HZ), startIndex, pathBuffer.Length - 1);

                if (endIndex - startIndex < 1) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                PointData pathStart = pathBuffer[startIndex].Value;
                PointData anchor = section.Anchor;

                float3x3 pathBasis = new(pathStart.Lateral, pathStart.Normal, pathStart.Direction);
                float3x3 anchorBasis = new(anchor.Lateral, anchor.Normal, anchor.Direction);
                float3x3 rotation = math.mul(anchorBasis, math.transpose(pathBasis));
                float3 translation = anchor.Position - math.mul(rotation, pathStart.Position);
                float4x4 transform = new(
                    new float4(rotation.c0, 0f),
                    new float4(rotation.c1, 0f),
                    new float4(rotation.c2, 0f),
                    new float4(translation, 1f)
                );

                float distance = pathBuffer[startIndex].Value.TotalLength;
                float endDistance = pathBuffer[endIndex].Value.TotalLength;
                int index = 0;
                int iters = 0;
                while (distance < endDistance) {
                    if (iters++ > 1e6) {
                        UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: Infinite loop");
                        break;
                    }

                    PointData prev = section.Points[^1];

                    float t = index / HZ;
                    if (section.FixedVelocity) {
                        float velocity = section.FixedVelocityKeyframes.Evaluate(t, section.Anchor);
                        if (velocity < MIN_VELOCITY) {
                            UnityEngine.Debug.LogWarning("Velocity is too low");
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
                            UnityEngine.Debug.LogWarning("Velocity is too low");
                            break;
                        }
                    }

                    prev.Heart = section.HeartKeyframes.Evaluate(t, section.Anchor);
                    prev.Friction = section.FrictionKeyframes.Evaluate(t, section.Anchor);
                    prev.Resistance = section.ResistanceKeyframes.Evaluate(t, section.Anchor);

                    PointData curr = prev;

                    UpdateCopyPoint(
                        section,
                        anchor,
                        ref curr,
                        prev,
                        ref pathBuffer,
                        ref index,
                        ref distance,
                        transform,
                        rotation,
                        startIndex,
                        endIndex
                    );

                    section.Points.Add(curr);
                }

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildCopyPathSectionSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }
                section.Dirty = false;
            }

            private void UpdateCopyPoint(
                CopyPathSectionAspect section,
                in PointData anchor,
                ref PointData curr,
                in PointData prev,
                ref DynamicBuffer<PathPort> pathBuffer,
                ref int index,
                ref float distance,
                float4x4 transform,
                float3x3 rotation,
                int startIndex,
                int endIndex
            ) {
                float expectedAdvancement = prev.Velocity / HZ;
                float desiredDistance = distance + expectedAdvancement;
                var (start, end, t) = Project(ref pathBuffer, ref index, desiredDistance, startIndex, endIndex);

                if (t == -1f) {
                    curr.Position = end.Position;
                    curr.Direction = end.Direction;
                    curr.Lateral = end.Lateral;
                    curr.Normal = end.Normal;
                    distance = pathBuffer[endIndex].Value.TotalLength;
                }
                else {
                    curr.Position = math.lerp(start.Position, end.Position, t);
                    curr.Direction = math.normalize(math.lerp(start.Direction, end.Direction, t));
                    curr.Lateral = math.normalize(math.lerp(start.Lateral, end.Lateral, t));
                    curr.Normal = math.normalize(math.lerp(start.Normal, end.Normal, t));
                }

                curr.Position = math.transform(transform, curr.Position);
                curr.Direction = math.mul(rotation, curr.Direction);
                curr.Lateral = math.mul(rotation, curr.Lateral);
                curr.Normal = math.mul(rotation, curr.Normal);
                curr.Roll = math.degrees(math.atan2(curr.Lateral.y, -curr.Normal.y));
                curr.Roll = (curr.Roll + 540) % 360 - 180;

                curr.Facing = anchor.Facing;

                // Compute point metrics
                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(curr.Heart), prev.GetHeartPosition(prev.Heart));
                curr.TotalLength += curr.DistanceFromLast;
                distance += expectedAdvancement;
                curr.HeartDistanceFromLast = math.distance(curr.Position, prev.Position);
                curr.TotalHeartLength += curr.HeartDistanceFromLast;

                // Compute orientation changes
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

                // Update energy and velocity
                if (!section.FixedVelocity) {
                    float pe = G * (curr.GetHeartPosition(curr.GetCenter()).y + (curr.TotalLength - curr.FrictionCompensation) * curr.Friction);
                    curr.Energy -= curr.Velocity * curr.Velocity * curr.Velocity * curr.Resistance / HZ;
                    curr.Velocity = math.sqrt(2f * math.max(0, curr.Energy - pe));
                }

                // Compute actual forces
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
                ref DynamicBuffer<PathPort> pathBuffer,
                ref int index,
                float distance,
                int startIndex,
                int endIndex
            ) {
                PointData start;
                PointData end;

                if (distance >= pathBuffer[endIndex].Value.TotalLength) {
                    index = endIndex - startIndex;
                    end = pathBuffer[endIndex].Value;
                    return (end, end, -1f);
                }

                for (int i = startIndex + index; i < endIndex; i++) {
                    if (pathBuffer[i + 1].Value.TotalLength >= distance) {
                        index = i - startIndex;
                        break;
                    }
                }

                start = pathBuffer[startIndex + index].Value;
                end = pathBuffer[startIndex + index + 1].Value;

                float t = (distance - start.TotalLength) / (end.TotalLength - start.TotalLength);
                t = math.saturate(t);

                return (start, end, t);
            }

        }
    }
}
