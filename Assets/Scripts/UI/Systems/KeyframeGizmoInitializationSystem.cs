using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(KeyframeGizmoCleanupSystem))]
    [BurstCompile]
    public partial struct KeyframeGizmoInitializationSystem : ISystem {
        private NativeList<Keyframe> _keyframes;
        private NativeArray<PropertyType> _propertyTypes;

        public void OnCreate(ref SystemState state) {
            _keyframes = new NativeList<Keyframe>(Allocator.Persistent);
            
            var propertyTypes = System.Enum.GetValues(typeof(PropertyType));
            _propertyTypes = new NativeArray<PropertyType>(propertyTypes.Length, Allocator.Persistent);
            for (int i = 0; i < propertyTypes.Length; i++) {
                _propertyTypes[i] = (PropertyType)propertyTypes.GetValue(i);
            }

            state.RequireForUpdate<KeyframeGizmoPrefabReference>();
            state.RequireForUpdate<PreferencesSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            if (_keyframes.IsCreated) {
                _keyframes.Dispose();
            }
            if (_propertyTypes.IsCreated) {
                _propertyTypes.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (!SystemAPI.GetSingleton<PreferencesSingleton>().ShowGizmos) return;

            var prefabReference = SystemAPI.GetSingleton<KeyframeGizmoPrefabReference>();
            if (prefabReference.Value == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var existingGizmos = new NativeParallelHashSet<uint>(2048, Allocator.Temp);

            foreach (var (gizmo, entity) in SystemAPI
                .Query<KeyframeGizmo>()
                .WithAll<KeyframeGizmoTag>()
                .WithEntityAccess()
            ) {
                existingGizmos.Add(gizmo.KeyframeId);

            }

            foreach (var (node, entity) in SystemAPI.Query<Node>().WithEntityAccess()) {
                foreach (var propertyType in _propertyTypes) {
                    state.EntityManager.GetAllKeyframes(entity, propertyType, _keyframes);

                    foreach (var keyframe in _keyframes) {
                        if (existingGizmos.Contains(keyframe.Id)) continue;

                        var gizmoEntity = ecb.Instantiate(prefabReference.Value);
                        ecb.AddComponent<KeyframeGizmoTag>(gizmoEntity);
                        ecb.AddComponent(gizmoEntity, new KeyframeGizmo {
                            Section = entity,
                            PropertyType = propertyType,
                            KeyframeId = keyframe.Id
                        });
                        ecb.AddComponent<KeyframeSelectedBlend>(gizmoEntity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
