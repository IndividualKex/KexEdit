using UnityEngine;
using KexEdit.Serialization;
using Unity.Entities;
using Unity.Collections;
using System.Collections;

namespace KexEdit {
    public class TrackLoader : MonoBehaviour {
        public Track Track;
        public GameObject CartRenderer;
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

            if (CartRenderer != null) {
                while (!entityManager.HasComponent<CartReference>(coaster)) {
                    yield return null;
                }

                Entity cartEntity = entityManager.GetComponentData<CartReference>(coaster);
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new LoadCartMeshEvent {
                    Cart = CartRenderer,
                    Target = cartEntity
                });
                ecb.SetName(entity, "Load Cart Mesh Event");
                ecb.Playback(entityManager);
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
