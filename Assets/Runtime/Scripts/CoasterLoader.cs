using UnityEngine;
using KexEdit.Serialization;
using Unity.Entities;
using System.Collections;

namespace KexEdit {
    public class CoasterLoader : MonoBehaviour {
        public Track Track;
        public TrackStyleSettingsData TrackStyle;
        public TrainStyleData TrainStyle;

        private IEnumerator Start() {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(GlobalSettings));
            while (query.IsEmpty) yield return null;

            var coaster = SerializationSystem.Instance.DeserializeGraph(Track.Data, restoreUIState: false);
            if (coaster == Entity.Null) {
                Debug.LogError("Failed to deserialize coaster");
                yield break;
            }

            if (TrackStyle != null) {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new LoadTrackStyleSettingsEvent {
                    Data = TrackStyle
                });
                entityManager.AddComponentData<TrackStyleSettingsReference>(coaster, entity);
                entityManager.SetName(entity, "Load Track Style Settings Event");
            }

            if (TrainStyle != null) {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new LoadTrainStyleEvent {
                    Data = TrainStyle
                });
                entityManager.AddComponentData<TrainStyleReference>(coaster, entity);
                entityManager.SetName(entity, "Load Train Style Event");
            }
        }
    }
}
