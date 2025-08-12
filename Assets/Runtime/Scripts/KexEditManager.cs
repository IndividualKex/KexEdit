using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    public class KexEditManager : MonoBehaviour {
        public bool EnableShadows;

        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new InitializeEvent {
                EnableShadows = EnableShadows
            });
            ecb.Playback(entityManager);
        }
    }
}
