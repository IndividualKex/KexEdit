using KexEdit.Nodes.CopyPath;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildCopyPathSectionSystem : ISystem {
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
        [WithAll(typeof(CopyPathSectionTag))]
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

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in PropertyOverrides propertyOverrides,
                in DynamicBuffer<FixedVelocityKeyframe> fixedVelocityKeyframes,
                in DynamicBuffer<HeartKeyframe> heartKeyframes,
                in DynamicBuffer<FrictionKeyframe> frictionKeyframes,
                in DynamicBuffer<ResistanceKeyframe> resistanceKeyframes,
                in DynamicBuffer<InputPortReference> inputPorts,
                in DynamicBuffer<OutputPortReference> outputPorts,
                ref DynamicBuffer<Point> points
            ) {
                if (inputPorts.Length != 4) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: Expected 4 input ports (anchor, path, start, end)");
                    return;
                }

                if (!PathPortLookup.TryGetBuffer(inputPorts[1], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No path port found");
                    return;
                }

                if (!StartPortLookup.TryGetComponent(inputPorts[2], out var startPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No start port found");
                    return;
                }

                if (!EndPortLookup.TryGetComponent(inputPorts[3], out var endPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No end port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                PointConverter.ToPoint(in anchor.Value, out CorePoint anchorState);

                float startTime = startPort.Value;
                float endTime = endPort.Value;

                using var sourcePath = PointConverter.ConvertPathBuffer(ref pathBuffer, Allocator.Temp);
                using var drivenVelocityKf = PointConverter.ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = PointConverter.ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = PointConverter.ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = PointConverter.ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

                CopyPathNode.Build(
                    in anchorState,
                    sourcePath,
                    startTime,
                    endTime,
                    propertyOverrides.FixedVelocity,
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
                result.Dispose();

                if (outputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(outputPorts[0], out var anchorPort)) {
                    anchorPort.Value = points[^1].Value;
                    Ecb.SetComponent(chunkIndex, outputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildCopyPathSectionSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
