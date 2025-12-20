using KexEdit.Nodes;
using KexEdit.Nodes.Geometric;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
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

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in Duration duration,
                in PropertyOverrides propertyOverrides,
                in Steering steering,
                in DynamicBuffer<RollSpeedKeyframe> rollSpeedKeyframes,
                in DynamicBuffer<PitchSpeedKeyframe> pitchSpeedKeyframes,
                in DynamicBuffer<YawSpeedKeyframe> yawSpeedKeyframes,
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
                using var pitchSpeedKf = PointConverter.ConvertKeyframes(pitchSpeedKeyframes, Allocator.Temp);
                using var yawSpeedKf = PointConverter.ConvertKeyframes(yawSpeedKeyframes, Allocator.Temp);
                using var drivenVelocityKf = PointConverter.ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = PointConverter.ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = PointConverter.ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = PointConverter.ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

                GeometricNode.Build(
                    in anchorState,
                    in config,
                    propertyOverrides.FixedVelocity,
                    steering.Value,
                    rollSpeedKf,
                    pitchSpeedKf,
                    yawSpeedKf,
                    drivenVelocityKf,
                    heartOffsetKf,
                    frictionKf,
                    resistanceKf,
                    anchor.Value.Heart,
                    anchor.Value.Friction,
                    anchor.Value.Resistance,
                    ref result
                );

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
                    UnityEngine.Debug.LogWarning("BuildGeometricSectionSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
