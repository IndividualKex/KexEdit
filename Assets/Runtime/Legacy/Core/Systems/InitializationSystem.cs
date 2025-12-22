using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class InitializationSystem : SystemBase {
        private BlobAssetReference<Collider> _trackColliderBlob;
        private EntityArchetype _trackColliderArchetype;
        private bool _initialized;

        protected override void OnCreate() {
            _trackColliderArchetype = EntityManager.CreateArchetype(
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex),
                typeof(NodeReference),
                typeof(SegmentReference),
                typeof(LocalTransform)
            );

            RequireForUpdate<InitializeEvent>();
        }

        protected override void OnDestroy() {
            if (_trackColliderBlob.IsCreated) {
                _trackColliderBlob.Dispose();
            }
        }

        protected override void OnUpdate() {
            if (_initialized) {
                throw new System.Exception("Runtime already initialized");
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            int trainLayer = 0;
            foreach (var (evt, entity) in SystemAPI.Query<InitializeEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
                if (_initialized) {
                    throw new System.Exception("Runtime already initialized");
                }
                trainLayer = evt.TrainLayer;
                _initialized = true;
            }

            var boxGeometry = new BoxGeometry {
                Center = new float3(0f, -0.185f, 0f),
                Size = new float3(1.5f, 0.475f, 0.5f),
                Orientation = quaternion.identity,
            };

            _trackColliderBlob = BoxCollider.Create(boxGeometry, CollisionFilter.Default, Material.Default);

            var colliderTemplateEntity = ecb.CreateEntity();
            ecb.AddComponent(colliderTemplateEntity, new TrackColliderTemplate {
                Archetype = _trackColliderArchetype,
                ColliderBlob = _trackColliderBlob
            });
            ecb.SetName(colliderTemplateEntity, "Track Collider Template");

            var settingsEntity = ecb.CreateEntity();
            var preferences = Preferences.Default;
            preferences.TrainLayer = trainLayer;
            ecb.AddComponent(settingsEntity, preferences);
            ecb.SetName(settingsEntity, "Settings");

            ecb.Playback(EntityManager);
        }
    }
}
