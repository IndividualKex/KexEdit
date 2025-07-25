using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct NodeGraphState : IComponentData {
        public float2 Pan;
        public float Zoom;
    }
}
