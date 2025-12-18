using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;

namespace KexEdit.Legacy {
    [BurstCompile]
    public partial struct TrackColliderCreationSystem : ISystem {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackPoint, TrackColliderReference>()
                .WithAll<TrackHash, TrackColliderHash>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_query);
            state.RequireForUpdate<TrackColliderTemplate>();
            state.RequireForUpdate<Preferences>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var preferences = SystemAPI.GetSingleton<Preferences>();
            if (!preferences.EnableColliders) return;

            Entity updateEntity = Entity.Null;
            Entity sectionEntity = Entity.Null;
            foreach (var (trackHash, colliderHash, render, section, entity) in SystemAPI
                .Query<TrackHash, RefRW<TrackColliderHash>, Render, SectionReference>()
                .WithEntityAccess()
            ) {
                if (!render || colliderHash.ValueRO == trackHash.Value) continue;
                colliderHash.ValueRW = trackHash.Value;
                updateEntity = entity;
                sectionEntity = section;
                break;
            }

            if (updateEntity == Entity.Null) return;

            var colliderTemplate = SystemAPI.GetSingleton<TrackColliderTemplate>();

            var colliderReferenceBuffer = SystemAPI.GetBuffer<TrackColliderReference>(updateEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < colliderReferenceBuffer.Length; i++) {
                ecb.DestroyEntity(colliderReferenceBuffer[i]);
            }
            colliderReferenceBuffer.Clear();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            ecb = new EntityCommandBuffer(Allocator.TempJob);
            colliderReferenceBuffer = SystemAPI.GetBuffer<TrackColliderReference>(updateEntity);
            var trackPointBuffer = SystemAPI.GetBuffer<TrackPoint>(updateEntity);
            var colliderEntities = state.EntityManager.CreateEntity(
                colliderTemplate.Archetype,
                trackPointBuffer.Length,
                Allocator.TempJob
            );
            new CreateJob {
                Ecb = ecb,
                Entity = updateEntity,
                SectionEntity = sectionEntity,
                ColliderReferenceBuffer = colliderReferenceBuffer,
                TrackPointBuffer = trackPointBuffer,
                ColliderEntities = colliderEntities,
                ColliderBlob = colliderTemplate.ColliderBlob,
            }.Run();
            colliderEntities.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private struct CreateJob : IJob {
            public EntityCommandBuffer Ecb;
            public Entity Entity;
            public Entity SectionEntity;
            public DynamicBuffer<TrackColliderReference> ColliderReferenceBuffer;
            public DynamicBuffer<TrackPoint> TrackPointBuffer;
            public NativeArray<Entity> ColliderEntities;
            public BlobAssetReference<Collider> ColliderBlob;

            public void Execute() {
                for (int i = 0; i < TrackPointBuffer.Length; i++) {
                    var colliderEntity = ColliderEntities[i];
                    var trackPoint = TrackPointBuffer[i];
                    var rotation = quaternion.LookRotation(trackPoint.Direction, trackPoint.Normal);
                    Ecb.SetComponent(colliderEntity, new PhysicsCollider { Value = ColliderBlob });
                    Ecb.SetComponent(colliderEntity, new LocalTransform {
                        Position = trackPoint.Position,
                        Rotation = rotation,
                        Scale = 1f,
                    });
                    Ecb.SetComponent<NodeReference>(colliderEntity, SectionEntity);
                    Ecb.SetComponent<SegmentReference>(colliderEntity, Entity);
                    Ecb.SetName(colliderEntity, "Collider");
                    ColliderReferenceBuffer.Add(colliderEntity);
                }
            }
        }
    }
}
