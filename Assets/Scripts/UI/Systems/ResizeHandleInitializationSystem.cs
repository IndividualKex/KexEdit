using Unity.Entities;
using UnityEngine.UIElements;
using KexEdit.UI.Timeline;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class ResizeHandleInitializationSystem : SystemBase {
        private bool _initialized = false;

        protected override void OnUpdate() {
            if (_initialized) return;

            _initialized = true;

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
