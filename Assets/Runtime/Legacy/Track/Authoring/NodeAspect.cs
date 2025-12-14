using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public readonly partial struct NodeAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Node> node;
        private readonly RefRO<CoasterReference> coasterReference;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public float2 Position => node.ValueRO.Position;
        public NodeType Type => node.ValueRO.Type;
        public int Priority => node.ValueRO.Priority;
        public bool Selected => node.ValueRO.Selected;

        public Entity Coaster => coasterReference.ValueRO.Value;
    }
}
