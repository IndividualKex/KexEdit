using System.Collections;
using KexEdit.UI;
using UnityEngine;

namespace KexEdit {
    public class GameManager : MonoBehaviour {
        private void Awake() {
            Physics.simulationMode = SimulationMode.Script;
        }

        private IEnumerator Start() {
            yield return null;
            ProjectOperations.RecoverLastSession();
        }
    }
}
