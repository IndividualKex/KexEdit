using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildReverseSystem : ISystem {
        private ComponentLookup<AnchorPort> _anchorPortLookup;

        public void OnCreate(ref SystemState state) {
            _anchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true);
        }

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

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ReverseAspect section) {
                if (!section.Dirty) return;

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    PointData p = section.Anchor;
                    p.Reverse();
                    anchorPort.Value = p;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildReverseSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }
        }
    }
}
