using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class UIManager : MonoBehaviour {
        private void Start() {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent<InitializeUIEvent>(entity);
            ecb.Playback(entityManager);
        }
    }
}
