using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace KexEdit.Legacy {
    public class KexEditManager : MonoBehaviour {
        public int TrainLayer = 0;

        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new InitializeEvent {
                TrainLayer = TrainLayer
            });
            ecb.Playback(entityManager);
        }
    }
}
