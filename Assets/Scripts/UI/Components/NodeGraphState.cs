using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.UI {
    public struct NodeGraphState : IComponentData {
        public float2 Pan;
        public float Zoom;
    }
}
