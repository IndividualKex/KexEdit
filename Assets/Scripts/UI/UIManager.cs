using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class UIManager : MonoBehaviour {
        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent<InitializeEvent>(entity);
            ecb.Playback(entityManager);
        }
    }
}
