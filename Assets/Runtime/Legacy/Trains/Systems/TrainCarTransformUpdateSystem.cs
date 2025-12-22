using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackFollowerUpdateSystem))]
    [BurstCompile]
    public partial struct TrainCarTransformUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var query = SystemAPI.QueryBuilder()
                .WithAll<TrainCar, LocalTransform>()
                .WithAll<WheelAssemblyReference>()
                .Build();
            state.Dependency = new Job {
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                WheelAssemblyLookup = SystemAPI.GetComponentLookup<WheelAssembly>(true),
                TrainLookup = SystemAPI.GetComponentLookup<Train>(true),
            }.ScheduleParallel(query, state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly]
            public ComponentLookup<WheelAssembly> WheelAssemblyLookup;
            [ReadOnly]
            public ComponentLookup<Train> TrainLookup;

            public void Execute(Entity entity, in TrainCar trainCar, DynamicBuffer<WheelAssemblyReference> wheelAssemblies) {
                ref var transformRef = ref TransformLookup.GetRefRW(entity).ValueRW;
                transformRef = LocalTransform.FromPosition(new float3(0f, -999f, 0f));

                if (!TrainLookup.TryGetComponent(trainCar.Train, out var train) || !train.Enabled) return;

                if (wheelAssemblies.Length == 0) return;

                if (wheelAssemblies.Length == 1) {
                    if (!TransformLookup.TryGetComponent(wheelAssemblies[0], out var wheelAssemblyTransform)) return;
                    transformRef = LocalTransform.FromPositionRotation(
                        wheelAssemblyTransform.Position,
                        wheelAssemblyTransform.Rotation
                    );
                    return;
                }

                float4x4 front = float4x4.identity;
                float4x4 back = float4x4.identity;
                float minOffset = float.MaxValue;
                float maxOffset = float.MinValue;
                for (int i = 0; i < wheelAssemblies.Length; i++) {
                    if (!WheelAssemblyLookup.TryGetComponent(wheelAssemblies[i], out var wheelAssembly) ||
                        !TransformLookup.TryGetComponent(wheelAssemblies[i], out var wheelAssemblyTransform)) continue;
                    float offset = wheelAssembly.Offset * train.Facing;
                    if (offset < minOffset) {
                        minOffset = offset;
                        back = new float4x4(wheelAssemblyTransform.Rotation, wheelAssemblyTransform.Position);
                    }
                    if (offset > maxOffset) {
                        maxOffset = offset;
                        front = new float4x4(wheelAssemblyTransform.Rotation, wheelAssemblyTransform.Position);
                    }
                }

                float3 frontPos = front.c3.xyz;
                float3 backPos = back.c3.xyz;

                float3 forwardVector = backPos - frontPos;
                if (math.lengthsq(forwardVector) < 1e-3f) return;

                float3 forward = train.Facing >= 0 ? math.normalize(backPos - frontPos) : math.normalize(frontPos - backPos);
                float3 upFront = front.c1.xyz;
                float3 upBack = back.c1.xyz;
                float3 up = math.normalize(upFront + upBack);
                float3 right = math.normalize(math.cross(up, forward));
                up = math.cross(forward, right);
                quaternion rotation = quaternion.LookRotation(forward, up);
                float3 pivotPos = train.Facing >= 0 ? frontPos : backPos;
                transformRef = LocalTransform.FromPositionRotation(
                    pivotPos,
                    rotation
                );
            }
        }
    }
}
