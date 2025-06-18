using UnityEngine;

namespace KexEdit {
    public class GameManager : MonoBehaviour {
        private void Awake() {
            Physics.simulationMode = SimulationMode.Script;
        }
    }
}
