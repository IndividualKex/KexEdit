using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Resources = UnityEngine.Resources;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class InitializationSystem : SystemBase {
        private BlobAssetReference<Collider> _colliderBlob;
        private EntityArchetype _colliderArchetype;
        private bool _initialized;

        private EntityQuery _initializeEventQuery;

        protected override void OnCreate() {
            _initializeEventQuery = GetEntityQuery(typeof(InitializeEvent));
            RequireForUpdate(_initializeEventQuery);

            _colliderArchetype = EntityManager.CreateArchetype(
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex),
                typeof(NodeReference),
                typeof(SegmentReference),
                typeof(LocalTransform)
            );
        }

        protected override void OnDestroy() {
            if (_colliderBlob.IsCreated) {
                _colliderBlob.Dispose();
            }
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            if (_initialized) return;

            _initialized = true;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<InitializeEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }

            var boxGeometry = new BoxGeometry {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(1.1f, 0.5f, 0.5f)
            };

            _colliderBlob = BoxCollider.Create(boxGeometry, CollisionFilter.Default, Material.Default);

            var colliderTemplateEntity = ecb.CreateEntity();
            ecb.AddComponent(colliderTemplateEntity, new ColliderTemplate {
                Archetype = _colliderArchetype,
                ColliderBlob = _colliderBlob
            });
            ecb.SetName(colliderTemplateEntity, "Collider Template");

            var trackMeshCompute = Resources.Load<UnityEngine.ComputeShader>("TrackMeshCompute");
            var duplicationMaterial = Resources.Load<UnityEngine.Material>("Duplication");
            var extrusionMaterial = Resources.Load<UnityEngine.Material>("Extrusion");
            var gizmoMaterial = Resources.Load<UnityEngine.Material>("LineGizmo");

            var settingsEntity = ecb.CreateEntity();
            ecb.AddComponent(settingsEntity, new GlobalSettings {
                Compute = trackMeshCompute,
                DuplicationMaterial = duplicationMaterial,
                ExtrusionMaterial = extrusionMaterial,
                GizmoMaterial = gizmoMaterial,
            });

            var gizmoSettings = new GizmoSettings();

            var defaultGizmoMaterial = new UnityEngine.Material(gizmoMaterial);
            defaultGizmoMaterial.SetColor("_Color", new(0.7f, 0.7f, 0.7f));

            gizmoSettings.ExtrusionGizmos.Add(new ExtrusionGizmoSettings {
                Material = defaultGizmoMaterial,
                Heart = 0f
            });

            ecb.AddComponent(settingsEntity, gizmoSettings);
            ecb.SetName(settingsEntity, "Global Settings");

            var cartEntity = ecb.CreateEntity();
            ecb.AddComponent(cartEntity, LocalTransform.Identity);
            ecb.AddComponent(cartEntity, new Cart {
                Position = 1f,
                Active = true,
                Kinematic = false
            });
            ecb.AddComponent(cartEntity, new CartMeshReference());
            ecb.SetName(cartEntity, "Cart");

            ecb.Playback(EntityManager);
        }
    }
}
