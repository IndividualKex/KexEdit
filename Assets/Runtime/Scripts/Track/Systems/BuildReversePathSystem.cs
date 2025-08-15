using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildReversePathSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                PathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ReversePathAspect section) {
                if (!section.Dirty) return;

                if (section.InputPorts.Length < 1
                    || !PathPortLookup.TryGetBuffer(section.InputPorts[0], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildReversePathSystem: No path port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                section.Points.Clear();
                float totalLength = pathBuffer[^1].Value.TotalLength;
                for (int i = pathBuffer.Length - 1; i >= 0; i--) {
                    PointData p = pathBuffer[i];
                    p.Reverse();
                    p.TotalLength = totalLength - p.TotalLength;
                    section.Points.Add(p);
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }
        }
    }
}
