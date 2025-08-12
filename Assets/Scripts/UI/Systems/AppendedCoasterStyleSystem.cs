using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class AppendedCoasterStyleSystem : SystemBase {
        private EntityQuery _editorCoasterQuery;

        protected override void OnCreate() {
            _editorCoasterQuery = GetEntityQuery(typeof(Coaster), typeof(EditorCoasterTag));
        }

        protected override void OnUpdate() {
            if (_editorCoasterQuery.IsEmpty) return;

            Entity editorCoaster = _editorCoasterQuery.GetSingletonEntity();
            if (!SystemAPI.HasComponent<TrackStyleSettingsReference>(editorCoaster)) return;

            Entity editorStyleReference = SystemAPI.GetComponent<TrackStyleSettingsReference>(editorCoaster).Value;
            if (editorStyleReference == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                bool needsStyleUpdate = false;

                if (SystemAPI.HasComponent<TrackStyleSettingsReference>(entity)) {
                    Entity currentStyleReference = SystemAPI.GetComponent<TrackStyleSettingsReference>(entity).Value;
                    if (currentStyleReference != editorStyleReference) {
                        ecb.RemoveComponent<TrackStyleSettingsReference>(entity);
                        needsStyleUpdate = true;
                    }
                }
                else {
                    needsStyleUpdate = true;
                }

                if (needsStyleUpdate) {
                    ecb.AddComponent(entity, new TrackStyleSettingsReference { Value = editorStyleReference });
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
