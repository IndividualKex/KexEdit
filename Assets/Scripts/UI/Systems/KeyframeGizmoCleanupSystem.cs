using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(KeyframeGizmoInitializationSystem))]
    [BurstCompile]
    public partial struct KeyframeGizmoCleanupSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<PreferencesSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var preferences = SystemAPI.GetSingleton<PreferencesSingleton>();

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (gizmo, entity) in SystemAPI
                .Query<KeyframeGizmo>()
                .WithAll<KeyframeGizmoTag>()
                .WithEntityAccess()
            ) {
                if (!preferences.ShowGizmos ||
                    !SystemAPI.HasComponent<Node>(gizmo.Section)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var keyframe = state.EntityManager.GetKeyframe(gizmo.Section, gizmo.PropertyType, gizmo.KeyframeId);
                if (!keyframe.HasValue) {   
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
