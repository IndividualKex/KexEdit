using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ReverseAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> anchor;
        private readonly RefRO<ReverseTag> reverseTag;

        private readonly RefRW<Dirty> dirty;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public PointData Anchor => anchor.ValueRO;

        public bool Dirty {
            get => dirty.ValueRO.Value;
            set => dirty.ValueRW.Value = value;
        }

        public ReverseTag ReverseTag => reverseTag.ValueRO;
    }
}
