using System.Collections;
using KexEdit.UI.Serialization;
using KexEdit.UI.Timeline;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    public class UIManager : MonoBehaviour {
        private VisualElement _root;

        private void Awake() {
            Physics.simulationMode = SimulationMode.Script;
        }

        private IEnumerator Start() {
            _root = UIService.Instance.UIDocument.rootVisualElement;

            InitializeHandles();

            while (!HasCameraState()) {
                yield return null;
            }

            ProjectOperations.RecoverLastSession();
        }

        private void InitializeHandles() {
            var bottom = _root.Q<VisualElement>("Bottom");
            bottom.Add(new ResizeHandle(ResizeHandle.ResizeMode.Vertical));

            var topLeft = _root.Q<VisualElement>("TopLeftPanel");
            topLeft.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalRight));

            var outliner = _root.Q<TimelineOutliner>();
            outliner.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalRight));

            var keyframeEditor = _root.Q<KeyframeEditor>();
            keyframeEditor.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalLeft));
        }

        private bool HasCameraState() {
            if (SerializationSystem.Instance == null) return false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return false;

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(typeof(CameraState));
            bool hasCameraState = !query.IsEmpty;
            return hasCameraState;
        }
    }
}
