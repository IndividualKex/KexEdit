using KexEdit.Nodes.Bridge;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildBridgeSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
                InWeightLookup = SystemAPI.GetComponentLookup<InWeightPort>(true),
                OutWeightLookup = SystemAPI.GetComponentLookup<OutWeightPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(BridgeTag))]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;
            [ReadOnly]
            public ComponentLookup<InWeightPort> InWeightLookup;
            [ReadOnly]
            public ComponentLookup<OutWeightPort> OutWeightLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in PropertyOverrides propertyOverrides,
                in DynamicBuffer<InputPortReference> inputPorts,
                in DynamicBuffer<OutputPortReference> outputPorts,
                in DynamicBuffer<FixedVelocityKeyframe> fixedVelocityKeyframes,
                in DynamicBuffer<HeartKeyframe> heartKeyframes,
                in DynamicBuffer<FrictionKeyframe> frictionKeyframes,
                in DynamicBuffer<ResistanceKeyframe> resistanceKeyframes,
                ref DynamicBuffer<CorePointBuffer> corePoints
            ) {
                if (inputPorts.Length < 2 ||
                    !AnchorPortLookup.TryGetComponent(inputPorts[1], out var targetAnchorPort)) {
                    UnityEngine.Debug.LogError("BuildBridgeSystem: Missing source or target anchor port");
                    return;
                }

                PointData target = targetAnchorPort;
                PointConverter.ToPoint(in anchor.Value, out CorePoint anchorState);
                PointConverter.ToPoint(in target, out CorePoint targetState);

                float outWeight = 0.3f;
                float inWeight = 0.3f;
                if (inputPorts.Length > 2 && OutWeightLookup.TryGetComponent(inputPorts[2], out var outW)) {
                    outWeight = outW.Value;
                }
                if (inputPorts.Length > 3 && InWeightLookup.TryGetComponent(inputPorts[3], out var inW)) {
                    inWeight = inW.Value;
                }

                using var drivenVelocityKf = PointConverter.ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = PointConverter.ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = PointConverter.ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = PointConverter.ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

                BridgeNode.Build(
                    in anchorState,
                    in targetState,
                    inWeight,
                    outWeight,
                    propertyOverrides.FixedVelocity,
                    drivenVelocityKf,
                    heartOffsetKf,
                    frictionKf,
                    resistanceKf,
                    anchor.Value.HeartOffset,
                    anchor.Value.Friction,
                    anchor.Value.Resistance,
                    ref result
                );

                corePoints.Clear();
                CorePointBuffer.CreateFromAnchor(in result.ElementAt(0), in anchor.Value, out CorePointBuffer first);
                corePoints.Add(first);
                for (int i = 1; i < result.Length; i++) {
                    CorePointBuffer.Create(in result.ElementAt(i), in result.ElementAt(i - 1), anchor.Value.Facing, out CorePointBuffer curr);
                    corePoints.Add(curr);
                }

                result.Dispose();

                if (outputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(outputPorts[0], out var anchorPort)) {
                    anchorPort.Value = corePoints[^1].ToPointData();
                    Ecb.SetComponent(chunkIndex, outputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildBridgeSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
