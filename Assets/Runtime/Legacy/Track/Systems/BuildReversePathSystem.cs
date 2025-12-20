using KexEdit.Nodes.ReversePath;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildReversePathSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                PathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ReversePathTag))]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in DynamicBuffer<InputPortReference> inputPorts,
                in DynamicBuffer<OutputPortReference> outputPorts,
                ref DynamicBuffer<Point> points,
                ref DynamicBuffer<CorePointBuffer> corePoints
            ) {
                if (inputPorts.Length < 1 ||
                    !PathPortLookup.TryGetBuffer(inputPorts[0], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildReversePathSystem: No path port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                using var sourcePath = PointConverter.ConvertPathBuffer(ref pathBuffer, Allocator.Temp);
                var result = new NativeList<CorePoint>(Allocator.Temp);

                ReversePathNode.Build(sourcePath, ref result);

                points.Clear();
                float totalLength = pathBuffer[^1].Value.TotalLength;
                PointData firstAnchor = pathBuffer[^1].Value;
                for (int i = 0; i < result.Length; i++) {
                    PointConverter.ToPointDataZeroAngles(in result.ElementAt(i), in firstAnchor, out PointData curr);
                    curr.TotalLength = totalLength - sourcePath[sourcePath.Length - 1 - i].HeartArc;
                    points.Add(curr);
                }

                int facing = firstAnchor.Facing;
                corePoints.Clear();
                for (int i = 0; i < result.Length; i++) {
                    CorePointBuffer.CreateFirst(in result.ElementAt(i), facing, out CorePointBuffer curr);
                    corePoints.Add(curr);
                }

                result.Dispose();

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }
        }
    }
}
