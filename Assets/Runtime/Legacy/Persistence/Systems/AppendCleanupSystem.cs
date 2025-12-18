using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct AppendCleanupSystem : ISystem {
        private EntityQuery _coasterQuery;
        private EntityQuery _appendQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _coasterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Coaster, AppendedCoasterTag>()
                .Build(state.EntityManager);
            _appendQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AppendReference>()
                .Build(state.EntityManager);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var coasters = _coasterQuery.ToEntityArray(Allocator.Temp);
            using var appended = _appendQuery.ToComponentDataArray<AppendReference>(Allocator.Temp);
            
            using var existing = new NativeHashSet<Entity>(appended.Length, Allocator.Temp);
            foreach (var item in appended) {
                if (item.Value != Entity.Null) {
                    existing.Add(item.Value);
                }
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var coaster in coasters) {
                if (!existing.Contains(coaster)) {
                    ecb.DestroyEntity(coaster);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
