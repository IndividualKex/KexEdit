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
            if (!SystemAPI.HasComponent<TrackStyleReference>(editorCoaster)) return;

            Entity editorStyleReference = SystemAPI.GetComponent<TrackStyleReference>(editorCoaster).Value;
            if (editorStyleReference == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                bool needsStyleUpdate = false;

                if (SystemAPI.HasComponent<TrackStyleReference>(entity)) {
                    Entity currentStyleReference = SystemAPI.GetComponent<TrackStyleReference>(entity).Value;
                    if (currentStyleReference != editorStyleReference) {
                        ecb.RemoveComponent<TrackStyleReference>(entity);
                        needsStyleUpdate = true;
                    }
                }
                else {
                    needsStyleUpdate = true;
                }

                if (needsStyleUpdate) {
                    ecb.AddComponent(entity, new TrackStyleReference { Value = editorStyleReference });
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
