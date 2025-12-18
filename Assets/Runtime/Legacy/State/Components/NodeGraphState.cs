using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    public struct NodeGraphState : IComponentData {
        public float2 Pan;
        public float Zoom;
    }
}
