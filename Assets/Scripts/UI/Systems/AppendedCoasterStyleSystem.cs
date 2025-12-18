using Unity.Collections;
using Unity.Entities;

using KexEdit.Legacy;
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

            Entity editorTrackStyleReference = SystemAPI.GetComponent<TrackStyleSettingsReference>(editorCoaster);
            Entity editorTrainStyleReference = SystemAPI.GetComponent<TrainStyleReference>(editorCoaster);
            if (editorTrackStyleReference == Entity.Null || editorTrainStyleReference == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                bool needsTrackStyleUpdate = false;
                bool needsTrainStyleUpdate = false;

                if (SystemAPI.HasComponent<TrackStyleSettingsReference>(entity)) {
                    Entity currentTrackStyleReference = SystemAPI.GetComponent<TrackStyleSettingsReference>(entity).Value;
                    if (currentTrackStyleReference != editorTrackStyleReference) {
                        ecb.RemoveComponent<TrackStyleSettingsReference>(entity);
                        needsTrackStyleUpdate = true;
                    }
                }
                else {
                    needsTrackStyleUpdate = true;
                }

                if (SystemAPI.HasComponent<TrainStyleReference>(entity)) {
                    Entity currentTrainStyleReference = SystemAPI.GetComponent<TrainStyleReference>(entity).Value;
                    if (currentTrainStyleReference != editorTrainStyleReference) {
                        ecb.RemoveComponent<TrainStyleReference>(entity);
                        needsTrainStyleUpdate = true;
                    }
                }
                else {
                    needsTrainStyleUpdate = true;
                }

                if (needsTrackStyleUpdate) {
                    ecb.AddComponent(entity, new TrackStyleSettingsReference { Value = editorTrackStyleReference });
                }
                if (needsTrainStyleUpdate) {
                    ecb.AddComponent(entity, new TrainStyleReference { Value = editorTrainStyleReference });
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
