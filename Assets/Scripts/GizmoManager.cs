#if UNITY_EDITOR
using UnityEngine;

namespace KexEdit {
    public class GizmoManager : MonoBehaviour {
        private void OnDrawGizmos() {
            DrawSectionGizmosSystem.Instance?.Draw();
        }
    }
}
#endif
