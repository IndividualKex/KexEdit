using System.Collections;
using KexEdit.UI;
using KexEdit.UI.Serialization;
using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    public class GameManager : MonoBehaviour {
        private void Awake() {
            Physics.simulationMode = SimulationMode.Script;
        }

        private IEnumerator Start() {
            while (!HasUIState()) {
                yield return null;
            }

            ProjectOperations.RecoverLastSession();
        }

        private bool HasUIState() {
            if (SerializationSystem.Instance == null) return false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return false;

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(typeof(UIState));
            bool hasUIState = !query.IsEmpty;
            return hasUIState;
        }
    }
}
