using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayheadGizmoCreationSystem : SystemBase {
        private Material _playheadMaterial;

        protected override void OnCreate() {
            _playheadMaterial = Resources.Load<Material>("PlayheadGizmo");
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<Coaster>()
                .WithAll<EditorCoasterTag>()
                .WithNone<PlayheadGizmoReference>()
                .WithEntityAccess()
            ) {
                var playheadGizmoEntity = ecb.CreateEntity();
                ecb.AddComponent(playheadGizmoEntity, LocalTransform.Identity);
                ecb.AddComponent<PlayheadGizmoReference>(entity, playheadGizmoEntity);
                ecb.AddComponent<CoasterReference>(playheadGizmoEntity, entity);
                ecb.AddComponent(playheadGizmoEntity, new Train { Enabled = true, Kinematic = true });
                ecb.AddComponent(playheadGizmoEntity, TrackFollower.Default);
                ecb.AddComponent(playheadGizmoEntity, new PendingMaterialUpdate {
                    Material = _playheadMaterial
                });
                ecb.SetName(playheadGizmoEntity, "Playhead Gizmo");
            }
            ecb.Playback(EntityManager);
        }
    }
}
