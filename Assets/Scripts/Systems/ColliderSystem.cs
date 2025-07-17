using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct ColliderSystem : ISystem {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state) {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackPoint, ColliderReference>()
                .WithAll<TrackHash, ColliderHash>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_query);
            state.RequireForUpdate<ColliderPrefabReference>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity updateEntity = Entity.Null;
            Entity sectionEntity = Entity.Null;
            foreach (var (trackHash, colliderHash, section, entity) in SystemAPI
                .Query<TrackHash, RefRW<ColliderHash>, SectionReference>()
                .WithEntityAccess()
            ) {
                if (colliderHash.ValueRO == trackHash.Value) continue;
                colliderHash.ValueRW = trackHash.Value;
                updateEntity = entity;
                sectionEntity = section;
                break;
            }

            if (updateEntity == Entity.Null) return;

            var colliderPrefab = SystemAPI.GetSingleton<ColliderPrefabReference>().Value;

            var colliderReferenceBuffer = SystemAPI.GetBuffer<ColliderReference>(updateEntity);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            new CleanJob {
                Ecb = ecb,
                ColliderReferenceBuffer = colliderReferenceBuffer
            }.Run();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            ecb = new EntityCommandBuffer(Allocator.TempJob);
            colliderReferenceBuffer = SystemAPI.GetBuffer<ColliderReference>(updateEntity);
            var trackPointBuffer = SystemAPI.GetBuffer<TrackPoint>(updateEntity);
            var colliderEntities = state.EntityManager.Instantiate(colliderPrefab, trackPointBuffer.Length, Allocator.TempJob);
            new CreateJob {
                Ecb = ecb,
                Entity = updateEntity,
                SectionEntity = sectionEntity,
                ColliderReferenceBuffer = colliderReferenceBuffer,
                TrackPointBuffer = trackPointBuffer,
                ColliderEntities = colliderEntities,
            }.Run();
            colliderEntities.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private struct CleanJob : IJob {
            public EntityCommandBuffer Ecb;
            public DynamicBuffer<ColliderReference> ColliderReferenceBuffer;

            public void Execute() {
                for (int i = 0; i < ColliderReferenceBuffer.Length; i++) {
                    Ecb.DestroyEntity(ColliderReferenceBuffer[i]);
                }
                ColliderReferenceBuffer.Clear();
            }
        }

        [BurstCompile]
        private struct CreateJob : IJob {
            public EntityCommandBuffer Ecb;
            public Entity Entity;
            public Entity SectionEntity;
            public DynamicBuffer<ColliderReference> ColliderReferenceBuffer;
            public DynamicBuffer<TrackPoint> TrackPointBuffer;
            public NativeArray<Entity> ColliderEntities;

            public void Execute() {
                for (int i = 0; i < TrackPointBuffer.Length; i++) {
                    var colliderEntity = ColliderEntities[i];
                    var trackPoint = TrackPointBuffer[i];
                    var rotation = quaternion.LookRotation(trackPoint.Direction, trackPoint.Normal);
                    Ecb.SetComponent(colliderEntity, new LocalTransform {
                        Position = trackPoint.Position,
                        Rotation = rotation,
                        Scale = 1f,
                    });
                    Ecb.SetComponent<NodeReference>(colliderEntity, SectionEntity);
                    Ecb.SetComponent<SegmentReference>(colliderEntity, Entity);
                    ColliderReferenceBuffer.Add(colliderEntity);
                }
            }
        }
    }
}
