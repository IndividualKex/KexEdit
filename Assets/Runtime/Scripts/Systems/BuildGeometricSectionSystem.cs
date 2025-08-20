using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildGeometricSectionSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, GeometricSectionAspect section) {
                if (!section.Dirty) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                if (section.DurationType == DurationType.Time) {
                    BuildGeometricTimeSection(section);
                }
                else {
                    BuildGeometricDistanceSection(section);
                }

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildGeometricSectionSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }

            private void BuildGeometricTimeSection(GeometricSectionAspect section) {
                int pointCount = (int)(HZ * section.Duration);
                for (int i = 1; i < pointCount; i++) {
                    PointData prev = section.Points[i - 1];

                    float t = i / HZ;
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

                    float rollSpeed = section.RollSpeedKeyframes.Evaluate(t);
                    float pitchChangeRate = section.PitchSpeedKeyframes.Evaluate(t);
                    float yawChangeRate = section.YawSpeedKeyframes.Evaluate(t);

                    curr.RollSpeed = rollSpeed;

                    float deltaRoll = rollSpeed / HZ;
                    float deltaPitch = pitchChangeRate / HZ;
                    float deltaYaw = yawChangeRate / HZ;

                    UpdateGeometricPoint(section, ref curr, prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Points.Add(curr);
                }
            }

            private void BuildGeometricDistanceSection(GeometricSectionAspect section) {
                float anchorTotalLength = section.Anchor.TotalLength;
                float endLength = anchorTotalLength + section.Duration;
                int iters = 0;
                while (section.Points[^1].Value.TotalLength < endLength) {
                    if (iters++ > 1e6) {
                        UnityEngine.Debug.LogWarning("BuildGeometricDistanceSection: Too many iterations");
                        break;
                    }

                    PointData prev = section.Points[^1];
                    float d = prev.TotalLength - anchorTotalLength + prev.Velocity / HZ;

                    if (section.FixedVelocity) {
                        float velocity = section.FixedVelocityKeyframes.Evaluate(d, section.Anchor);
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

                    prev.Heart = section.HeartKeyframes.Evaluate(d, section.Anchor);
                    prev.Friction = section.FrictionKeyframes.Evaluate(d, section.Anchor);
                    prev.Resistance = section.ResistanceKeyframes.Evaluate(d, section.Anchor);

                    float rollSpeed = section.RollSpeedKeyframes.Evaluate(d);
                    float pitchChangeRate = section.PitchSpeedKeyframes.Evaluate(d);
                    float yawChangeRate = section.YawSpeedKeyframes.Evaluate(d);

                    PointData curr = prev;
                    curr.RollSpeed = rollSpeed;

                    float deltaRoll = rollSpeed * (prev.Velocity / HZ);
                    float deltaPitch = pitchChangeRate * (prev.Velocity / HZ);
                    float deltaYaw = yawChangeRate * (prev.Velocity / HZ);

                    UpdateGeometricPoint(section, ref curr, prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Points.Add(curr);
                }
            }

            private void UpdateGeometricPoint(
                GeometricSectionAspect section,
                ref PointData curr,
                in PointData prev,
                float deltaRoll,
                float deltaPitch,
                float deltaYaw
            ) {
                if (section.Steering) {
                    // Apply pitch
                    quaternion pitchQuat = quaternion.AxisAngle(math.right(), deltaPitch);
                    curr.Direction = math.normalize(math.mul(pitchQuat, prev.Direction));
                    curr.Lateral = math.normalize(math.mul(pitchQuat, prev.Lateral));

                    // Apply yaw
                    quaternion yawQuat = quaternion.AxisAngle(math.up(), deltaYaw);
                    curr.Direction = math.normalize(math.mul(yawQuat, curr.Direction));
                    curr.Lateral = math.normalize(math.mul(yawQuat, curr.Lateral));

                    // Update Normal
                    curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                    // Update position
                    curr.Position += curr.Direction * (curr.Velocity / (2f * HZ))
                        + prev.Direction * (curr.Velocity / (2f * HZ))
                        + (prev.GetHeartPosition(prev.Heart) - curr.GetHeartPosition(curr.Heart));

                    quaternion rollQuat = quaternion.AxisAngle(curr.Direction, -deltaRoll);
                    curr.Lateral = math.normalize(math.mul(rollQuat, curr.Lateral));
                    curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                    float3 worldRight = math.right();
                    float3 projectedLateral = curr.Lateral - math.dot(curr.Lateral, curr.Direction) * curr.Direction;
                    projectedLateral = math.normalize(projectedLateral);
                    float rollCos = math.dot(projectedLateral, worldRight);
                    float rollSin = math.dot(projectedLateral, math.cross(curr.Direction, worldRight));
                    curr.Roll = math.degrees(math.atan2(rollSin, rollCos));
                    curr.Roll = (curr.Roll + 540) % 360 - 180;
                }
                else {
                    // Apply pitch
                    float3 pitchAxis = math.normalize(math.cross(new float3(0f, curr.Normal.y, 0f), curr.Direction));
                    quaternion pitchQuat = quaternion.AxisAngle(pitchAxis, deltaPitch);
                    curr.Direction = math.normalize(math.mul(pitchQuat, prev.Direction));
                    curr.Lateral = math.normalize(math.mul(pitchQuat, prev.Lateral));

                    // Apply yaw
                    quaternion yawQuat = quaternion.AxisAngle(math.up(), deltaYaw);
                    curr.Direction = math.normalize(math.mul(yawQuat, curr.Direction));
                    curr.Lateral = math.normalize(math.mul(yawQuat, curr.Lateral));

                    // Update Normal
                    curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                    // Update position
                    curr.Position += curr.Direction * (curr.Velocity / (2f * HZ))
                        + prev.Direction * (curr.Velocity / (2f * HZ))
                        + (prev.GetHeartPosition(prev.Heart) - curr.GetHeartPosition(curr.Heart));

                    quaternion rollQuat = quaternion.AxisAngle(curr.Direction, -deltaRoll);
                    curr.Lateral = math.normalize(math.mul(rollQuat, curr.Lateral));
                    curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                    curr.Roll = math.degrees(math.atan2(curr.Lateral.y, -curr.Normal.y));
                    curr.Roll = (curr.Roll + 540) % 360 - 180;
                }

                // Compute point metrics
                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(curr.Heart), prev.GetHeartPosition(prev.Heart));
                curr.TotalLength += curr.DistanceFromLast;
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
        }
    }
}
