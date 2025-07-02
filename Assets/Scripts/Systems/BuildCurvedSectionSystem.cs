using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildCurvedSectionSystem : ISystem {
        private ComponentLookup<AnchorPort> _anchorPortLookup;

        public void OnCreate(ref SystemState state) {
            _anchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true);
        }

        [BurstCompile]
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

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, CurvedSectionAspect section) {
                if (!section.Dirty) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                BuildCurvedSection(section);

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildCurvedSectionSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }

            private void BuildCurvedSection(CurvedSectionAspect section) {
                float angle = 0f;
                float leadOutStartAngle = section.Arc - section.LeadOut;
                bool leadOutStarted = false;
                PointData leadOutStartNode = default;
                float myLeadOut = 0f;

                int iters = 0;
                int index = 0;
                while (angle < section.Arc - EPSILON) {
                    if (iters++ > 1e6) {
                        UnityEngine.Debug.LogError("BuildCurvedSectionSystem: Infinite loop");
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

                    float deltaAngle = prev.Velocity / section.Radius / HZ * math.degrees(1f);

                    if (section.LeadIn > 0f) {
                        float distanceFromStart = prev.TotalLength - section.Anchor.TotalLength;
                        float expectedLeadInDistance = 1.997f / HZ * prev.Velocity / deltaAngle * section.LeadIn;
                        float fTrans = distanceFromStart / expectedLeadInDistance;
                        if (fTrans <= 1f) {
                            float dampening = fTrans * fTrans * (3f + fTrans * (-2f));
                            deltaAngle *= dampening;
                        }
                    }

                    if (!leadOutStarted && angle > section.Arc - section.LeadOut) {
                        leadOutStarted = true;
                        leadOutStartNode = prev;
                        myLeadOut = section.Arc - angle;
                    }

                    if (leadOutStarted && section.LeadOut > 0f) {
                        float distanceFromLeadOutStart = prev.TotalLength - leadOutStartNode.TotalLength;
                        float expectedLeadOutDistance = 1.997f / HZ * prev.Velocity / deltaAngle * myLeadOut;
                        float fTrans = 1f - distanceFromLeadOutStart / expectedLeadOutDistance;
                        if (fTrans >= 0f) {
                            float dampening = fTrans * fTrans * (3f + fTrans * (-2f));
                            deltaAngle *= dampening;
                        }
                        else {
                            break;
                        }
                    }

                    angle += deltaAngle;
                    curr.RollSpeed = section.RollSpeedKeyframes.Evaluate(angle);

                    UpdateCurvedPoint(section, ref curr, prev, deltaAngle);
                    section.Points.Add(curr);
                    index++;
                }
            }

            private void UpdateCurvedPoint(
                CurvedSectionAspect section,
                ref PointData curr,
                in PointData prev,
                float deltaAngle
            ) {
                float axisRad = math.radians(section.Axis);
                float3 axis = -math.cos(axisRad) * prev.Normal + math.sin(axisRad) * prev.Lateral;

                quaternion curveQuat = quaternion.AxisAngle(axis, math.radians(deltaAngle));
                curr.Direction = math.normalize(math.mul(curveQuat, prev.Direction));

                float originalRoll = math.degrees(math.atan2(prev.Lateral.y, -prev.Normal.y));
                originalRoll = (originalRoll + 540) % 360 - 180;

                curr.Lateral = math.normalize(math.cross(curr.Direction, math.up()));
                if (math.length(curr.Lateral) < EPSILON) {
                    curr.Lateral = math.right();
                }
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                if (math.abs(originalRoll) > EPSILON) {
                    quaternion preservedRollQuat = quaternion.AxisAngle(curr.Direction, math.radians(-originalRoll));
                    curr.Lateral = math.normalize(math.mul(preservedRollQuat, curr.Lateral));
                    curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));
                }

                curr.Position += curr.Direction * (curr.Velocity / (2f * HZ))
                    + prev.Direction * (curr.Velocity / (2f * HZ))
                    + (prev.GetHeartPosition(prev.Heart) - curr.GetHeartPosition(curr.Heart));

                float deltaRoll = curr.RollSpeed / HZ;
                quaternion rollQuat = quaternion.AxisAngle(curr.Direction, -deltaRoll);
                curr.Lateral = math.normalize(math.mul(rollQuat, curr.Lateral));
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));
                curr.Roll = math.degrees(math.atan2(curr.Lateral.y, -curr.Normal.y));
                curr.Roll = (curr.Roll + 540) % 360 - 180;

                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(curr.Heart), prev.GetHeartPosition(prev.Heart));
                curr.TotalLength += curr.DistanceFromLast;
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
        }
    }
}
