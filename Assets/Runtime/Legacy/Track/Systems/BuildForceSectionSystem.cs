using KexEdit.Nodes;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;
#if USE_RUST_BACKEND
using KexEdit.Native.RustCore;
#else
using Unity.Burst;
using KexEdit.Nodes.Force;
#endif

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildForceSectionSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

#if !USE_RUST_BACKEND
        [BurstCompile]
#endif
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in Duration duration,
                in PropertyOverrides propertyOverrides,
                in DynamicBuffer<RollSpeedKeyframe> rollSpeedKeyframes,
                in DynamicBuffer<NormalForceKeyframe> normalForceKeyframes,
                in DynamicBuffer<LateralForceKeyframe> lateralForceKeyframes,
                in DynamicBuffer<FixedVelocityKeyframe> fixedVelocityKeyframes,
                in DynamicBuffer<HeartKeyframe> heartKeyframes,
                in DynamicBuffer<FrictionKeyframe> frictionKeyframes,
                in DynamicBuffer<ResistanceKeyframe> resistanceKeyframes,
                in DynamicBuffer<OutputPortReference> outputPorts,
                ref DynamicBuffer<Point> points,
                ref DynamicBuffer<CorePointBuffer> corePoints
            ) {
                PointConverter.ToPoint(in anchor.Value, out CorePoint anchorState);

                var config = new IterationConfig(
                    duration.Value,
                    (Nodes.DurationType)(int)duration.Type
                );

                using var rollSpeedKf = PointConverter.ConvertKeyframes(rollSpeedKeyframes, Allocator.Temp);
                using var normalForceKf = PointConverter.ConvertKeyframes(normalForceKeyframes, Allocator.Temp);
                using var lateralForceKf = PointConverter.ConvertKeyframes(lateralForceKeyframes, Allocator.Temp);
                using var drivenVelocityKf = PointConverter.ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = PointConverter.ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = PointConverter.ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = PointConverter.ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

#if USE_RUST_BACKEND
                int returnCode = RustForceNode.Build(
                    in anchorState,
                    duration.Value,
                    (int)duration.Type,
                    propertyOverrides.FixedVelocity,
                    in rollSpeedKf,
                    in normalForceKf,
                    in lateralForceKf,
                    in drivenVelocityKf,
                    in heartOffsetKf,
                    in frictionKf,
                    in resistanceKf,
                    anchor.Value.Heart,
                    anchor.Value.Friction,
                    anchor.Value.Resistance,
                    ref result
                );

                if (returnCode != 0) {
                    UnityEngine.Debug.LogWarning($"Rust ForceNode.Build failed with error code: {returnCode}");
                    result.Dispose();
                    dirty.ValueRW = false;
                    return;
                }
#else
                ForceNode.Build(
                    in anchorState,
                    in config,
                    propertyOverrides.FixedVelocity,
                    rollSpeedKf,
                    normalForceKf,
                    lateralForceKf,
                    drivenVelocityKf,
                    heartOffsetKf,
                    frictionKf,
                    resistanceKf,
                    anchor.Value.Heart,
                    anchor.Value.Friction,
                    anchor.Value.Resistance,
                    ref result
                );
#endif

                points.Clear();
                points.Add(anchor.Value);
                PointData prev = anchor;
                for (int i = 1; i < result.Length; i++) {
                    PointConverter.ToPointData(in result.ElementAt(i), in prev, out PointData curr);
                    points.Add(curr);
                    prev = curr;
                }

                int facing = anchor.Value.Facing;
                corePoints.Clear();
                CorePointBuffer.CreateFirst(in result.ElementAt(0), facing, out CorePointBuffer first);
                corePoints.Add(first);
                for (int i = 1; i < result.Length; i++) {
                    CorePointBuffer.Create(in result.ElementAt(i), in result.ElementAt(i - 1), facing, out CorePointBuffer curr);
                    corePoints.Add(curr);
                }

                result.Dispose();

                if (outputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(outputPorts[0], out var anchorPort)) {
                    anchorPort.Value = points[^1].Value;
                    Ecb.SetComponent(chunkIndex, outputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildForceSectionSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
