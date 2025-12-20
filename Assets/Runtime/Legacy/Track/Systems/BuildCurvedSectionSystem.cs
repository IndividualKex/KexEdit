using KexEdit.Nodes.Curved;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildCurvedSectionSystem : ISystem {
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

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in CurveData curveData,
                in PropertyOverrides propertyOverrides,
                in DynamicBuffer<RollSpeedKeyframe> rollSpeedKeyframes,
                in DynamicBuffer<FixedVelocityKeyframe> fixedVelocityKeyframes,
                in DynamicBuffer<HeartKeyframe> heartKeyframes,
                in DynamicBuffer<FrictionKeyframe> frictionKeyframes,
                in DynamicBuffer<ResistanceKeyframe> resistanceKeyframes,
                in DynamicBuffer<InputPortReference> inputPorts,
                in DynamicBuffer<OutputPortReference> outputPorts,
                ref DynamicBuffer<CorePointBuffer> corePoints
            ) {
                PointConverter.ToPoint(in anchor.Value, out CorePoint anchorState);

                using var rollSpeedKf = PointConverter.ConvertKeyframes(rollSpeedKeyframes, Allocator.Temp);
                using var drivenVelocityKf = PointConverter.ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = PointConverter.ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = PointConverter.ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = PointConverter.ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

                CurvedNode.Build(
                    in anchorState,
                    curveData.Radius,
                    curveData.Arc,
                    curveData.Axis,
                    curveData.LeadIn,
                    curveData.LeadOut,
                    propertyOverrides.FixedVelocity,
                    rollSpeedKf,
                    drivenVelocityKf,
                    heartOffsetKf,
                    frictionKf,
                    resistanceKf,
                    anchor.Value.Heart,
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
                    UnityEngine.Debug.LogWarning("BuildCurvedSectionSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
