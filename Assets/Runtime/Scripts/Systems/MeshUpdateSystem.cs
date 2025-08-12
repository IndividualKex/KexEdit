using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MeshUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (mesh, anchor, render, dirtyRW) in SystemAPI.Query<NodeMeshReference, Anchor, Render, RefRW<Dirty>>()) {
                if (mesh.Value == Entity.Null) continue;

                if (render.Value && SystemAPI.HasComponent<DisableRendering>(mesh.Value)) {
                    ecb.RemoveComponent<DisableRendering>(mesh.Value);
                }
                else if (!render.Value && !SystemAPI.HasComponent<DisableRendering>(mesh.Value)) {
                    ecb.AddComponent<DisableRendering>(mesh.Value);
                }

                if (!render.Value) continue;

                ref bool dirty = ref dirtyRW.ValueRW.Value;
                if (!dirty) continue;
                dirty = false;

                float3 position = anchor.Value.Position;
                quaternion rotation = quaternion.Euler(
                    math.radians(anchor.Value.Roll),
                    math.radians(anchor.Value.Velocity),
                    math.radians(anchor.Value.Energy)
                );
                float scale = anchor.Value.NormalForce;

                ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(mesh.Value).ValueRW;
                transform = LocalTransform.FromPositionRotationScale(position, rotation, scale);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
