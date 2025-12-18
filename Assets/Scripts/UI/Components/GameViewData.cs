using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public struct GameViewData : IComponentData {
        public KeyframeReference IntersectionKeyframe;
    }
}
