using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Resources = UnityEngine.Resources;

namespace KexEdit {
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

            foreach (var (_, entity) in SystemAPI.Query<InitializeEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
                if (_initialized) {
                    throw new System.Exception("Runtime already initialized");
                }
                _initialized = true;
            }

            var boxGeometry = new BoxGeometry {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(1.1f, 0.5f, 0.5f)
            };

            _trackColliderBlob = BoxCollider.Create(boxGeometry, CollisionFilter.Default, Material.Default);

            var colliderTemplateEntity = ecb.CreateEntity();
            ecb.AddComponent(colliderTemplateEntity, new TrackColliderTemplate {
                Archetype = _trackColliderArchetype,
                ColliderBlob = _trackColliderBlob
            });
            ecb.SetName(colliderTemplateEntity, "Track Collider Template");

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

            ecb.Playback(EntityManager);
        }
    }
}
