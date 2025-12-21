using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct ReadOnlyForceComputationSystem : ISystem {
        private EntityQuery _segmentQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _segmentQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CorePointBuffer>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_segmentQuery);
            state.RequireForUpdate<ReadPivot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var pivot = SystemAPI.GetSingleton<ReadPivot>();

            state.Dependency = new ComputeReadOnlyForcesJob {
                PointLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(true),
                ReadNormalForceLookup = SystemAPI.GetBufferLookup<ReadNormalForce>(false),
                ReadLateralForceLookup = SystemAPI.GetBufferLookup<ReadLateralForce>(false),
                ReadPitchSpeedLookup = SystemAPI.GetBufferLookup<ReadPitchSpeed>(false),
                ReadYawSpeedLookup = SystemAPI.GetBufferLookup<ReadYawSpeed>(false),
                ReadRollSpeedLookup = SystemAPI.GetBufferLookup<ReadRollSpeed>(false),
                Pivot = pivot
            }.ScheduleParallel(_segmentQuery, state.Dependency);
        }

        [BurstCompile]
        private partial struct ComputeReadOnlyForcesJob : IJobEntity {
            [ReadOnly] public BufferLookup<CorePointBuffer> PointLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ReadNormalForce> ReadNormalForceLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ReadLateralForce> ReadLateralForceLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ReadPitchSpeed> ReadPitchSpeedLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ReadYawSpeed> ReadYawSpeedLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ReadRollSpeed> ReadRollSpeedLookup;
            [ReadOnly] public ReadPivot Pivot;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex) {
                var points = PointLookup[entity];
                if (points.Length == 0) return;

                var normalForces = ReadNormalForceLookup[entity];
                normalForces.Clear();
                var lateralForces = ReadLateralForceLookup[entity];
                lateralForces.Clear();
                var pitchSpeeds = ReadPitchSpeedLookup[entity];
                pitchSpeeds.Clear();
                var yawSpeeds = ReadYawSpeedLookup[entity];
                yawSpeeds.Clear();
                var rollSpeeds = ReadRollSpeedLookup[entity];
                rollSpeeds.Clear();

                for (int i = 0; i < points.Length; i++) {
                    var centroidPoint = points[i];
                    var offsetPoint = GetOffsetPoint(points, i, Pivot.Offset);
                    float centroidVelocity = centroidPoint.Velocity();

                    float adjustedPitchFromLast = offsetPoint.PitchFromLast;
                    float adjustedYawFromLast = offsetPoint.YawFromLast;

                    if (math.abs(offsetPoint.Velocity) > Constants.EPSILON &&
                        math.abs(centroidVelocity - offsetPoint.Velocity) > Constants.EPSILON) {
                        float pitchPerMeter = offsetPoint.PitchFromLast * Constants.HZ / offsetPoint.Velocity;
                        float yawPerMeter = offsetPoint.YawFromLast * Constants.HZ / offsetPoint.Velocity;
                        adjustedPitchFromLast = pitchPerMeter * centroidVelocity / Constants.HZ;
                        adjustedYawFromLast = yawPerMeter * centroidVelocity / Constants.HZ;
                    }

                    float3 forceVec;
                    float yawScaleFactor = math.cos(math.abs(math.radians(offsetPoint.GetPitch())));
                    float angleFromLast = math.sqrt(
                        yawScaleFactor * yawScaleFactor * adjustedYawFromLast * adjustedYawFromLast
                        + adjustedPitchFromLast * adjustedPitchFromLast
                    );

                    if (math.abs(angleFromLast) < Constants.EPSILON) {
                        forceVec = math.up();
                    }
                    else {
                        float cosRoll = math.cos(math.radians(offsetPoint.Roll));
                        float sinRoll = math.sin(math.radians(offsetPoint.Roll));

                        float normalAngle = math.radians(-adjustedPitchFromLast * cosRoll
                            - yawScaleFactor * adjustedYawFromLast * sinRoll);
                        float lateralAngle = math.radians(adjustedPitchFromLast * sinRoll
                            - yawScaleFactor * adjustedYawFromLast * cosRoll);

                        float distancePerStep = centroidVelocity / Constants.HZ;

                        forceVec = math.up()
                            + centroidVelocity * Constants.HZ * lateralAngle * offsetPoint.Lateral / Constants.G
                            + distancePerStep * Constants.HZ * Constants.HZ * normalAngle * offsetPoint.Normal / Constants.G;
                    }

                    float normalForce = -math.dot(forceVec, offsetPoint.Normal);
                    float lateralForce = -math.dot(forceVec, offsetPoint.Lateral);

                    normalForces.Add(new ReadNormalForce { Value = normalForce });
                    lateralForces.Add(new ReadLateralForce { Value = lateralForce });
                    pitchSpeeds.Add(new ReadPitchSpeed { Value = adjustedPitchFromLast });
                    yawSpeeds.Add(new ReadYawSpeed { Value = adjustedYawFromLast });
                    rollSpeeds.Add(new ReadRollSpeed { Value = offsetPoint.RollSpeed });
                }
            }

            private PointData GetOffsetPoint(DynamicBuffer<CorePointBuffer> points, int currentIndex, float offset) {
                if (math.abs(offset) < 0.01f || points.Length <= 1) {
                    return points[currentIndex].ToPointData();
                }

                float currentLength = points[currentIndex].HeartArc();
                float targetLength = currentLength + offset;

                if (targetLength <= 0) {
                    return points[0].ToPointData();
                }
                if (targetLength >= points[^1].HeartArc()) {
                    return points[^1].ToPointData();
                }

                int startIndex = 0;
                int endIndex = points.Length - 1;

                while (endIndex - startIndex > 1) {
                    int midIndex = (startIndex + endIndex) / 2;
                    if (points[midIndex].HeartArc() <= targetLength) {
                        startIndex = midIndex;
                    } else {
                        endIndex = midIndex;
                    }
                }

                var p0 = points[startIndex].ToPointData();
                var p1 = points[endIndex].ToPointData();
                float segmentLength = p1.HeartArc - p0.HeartArc;

                if (segmentLength < 0.001f) {
                    return p0;
                }

                float t = (targetLength - p0.HeartArc) / segmentLength;
                return PointData.Lerp(p0, p1, t);
            }
        }
    }
}
