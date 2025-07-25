using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class UIInitializationSystem : SystemBase {
        private bool _initialized;

        private EntityQuery _initializeEventQuery;

        protected override void OnCreate() {
            _initializeEventQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InitializeUIEvent>()
                .Build(EntityManager);
            RequireForUpdate(_initializeEventQuery);
        }

        protected override void OnUpdate() {
            if (_initialized) {
                throw new System.Exception("UI already initialized");
            }

            World.GetOrCreateSystemManaged<UIInitializationSystemGroup>().Enabled = true;
            World.GetOrCreateSystemManaged<UISimulationSystemGroup>().Enabled = true;
            World.GetOrCreateSystemManaged<UIPresentationSystemGroup>().Enabled = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<InitializeUIEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
                if (_initialized) {
                    throw new System.Exception("UI already initialized");
                }
                _initialized = true;
            }

            var stateEntity = ecb.CreateEntity();
            float3 defaultPosition = new(6f, 6f, 6f);
            float3 defaultEuler = new(30f, -135f, 0f);
            ecb.AddComponent(stateEntity, new TimelineState {
                Offset = 0f,
                Zoom = 1f
            });
            ecb.AddComponent(stateEntity, new NodeGraphState {
                Pan = float2.zero,
                Zoom = 1f
            });
            ecb.AddComponent(stateEntity, new CameraState {
                Position = defaultPosition,
                TargetPosition = defaultPosition,
                Distance = math.length(defaultPosition),
                TargetDistance = math.length(defaultPosition),
                Pitch = defaultEuler.x,
                TargetPitch = defaultEuler.x,
                Yaw = defaultEuler.y,
                TargetYaw = defaultEuler.y,
                SpeedMultiplier = 1f,
                OrthographicSize = 1f,
                TargetOrthographicSize = 1f,
                Orthographic = false,
                TargetOrthographic = false
            });
            ecb.AddComponent<Gizmos>(stateEntity);
            ecb.SetName(stateEntity, "UI State");

            var playheadGizmoEntity = ecb.CreateEntity();
            ecb.AddComponent(playheadGizmoEntity, LocalTransform.Identity);
            ecb.AddComponent(playheadGizmoEntity, new Cart {
                Position = 1f,
                Active = true,
                Kinematic = true
            });
            ecb.AddComponent(playheadGizmoEntity, new CartStyleReference {
                StyleIndex = 0,
                Version = 0
            });
            ecb.AddComponent(playheadGizmoEntity, new CartMeshReference());
            ecb.AddComponent(playheadGizmoEntity, new RenderTag {
                Type = RenderTagType.Playhead
            });
            ecb.SetName(playheadGizmoEntity, "Playhead Gizmo");

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
