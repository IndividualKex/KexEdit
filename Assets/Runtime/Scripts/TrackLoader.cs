using UnityEngine;
using KexEdit.Serialization;
using Unity.Entities;
using Unity.Collections;
using System.Collections;

namespace KexEdit {
    public class TrackLoader : MonoBehaviour {
        public Track Track;
        public TrackStyleData TrackStyle;

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
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new LoadTrackStyleEvent {
                    Target = coaster,
                    TrackStyle = TrackStyle
                });
                ecb.SetName(entity, "Load Track Style Event");
                ecb.Playback(entityManager);
            }
        }

    }
}
