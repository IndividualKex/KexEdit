using UnityEngine;
using UnityEngine.UIElements;
using KexEdit.UI.Timeline;

namespace KexEdit.UI {
    public class PanelManager : MonoBehaviour {
        private void Start() {
            var root = UIService.Instance.UIDocument.rootVisualElement;

            var bottom = root.Q<VisualElement>("Bottom");
            bottom.Add(new ResizeHandle(ResizeHandle.ResizeMode.Vertical));

            var topLeft = root.Q<VisualElement>("TopLeftPanel");
            topLeft.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalRight));

            var outliner = root.Q<TimelineOutliner>();
            outliner.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalRight));

            var keyframeEditor = root.Q<KeyframeEditor>();
            keyframeEditor.Add(new ResizeHandle(ResizeHandle.ResizeMode.HorizontalLeft));
        }
    }
}
