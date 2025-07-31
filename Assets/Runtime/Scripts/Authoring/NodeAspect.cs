using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public readonly partial struct NodeAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Node> NodeRO;
        private readonly RefRO<CoasterReference> CoasterReferenceRO;
        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public float2 Position => NodeRO.ValueRO.Position;
        public NodeType Type => NodeRO.ValueRO.Type;
        public int Priority => NodeRO.ValueRO.Priority;
        public bool Selected => NodeRO.ValueRO.Selected;

        public Entity Coaster => CoasterReferenceRO.ValueRO.Value;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }
    }
}
