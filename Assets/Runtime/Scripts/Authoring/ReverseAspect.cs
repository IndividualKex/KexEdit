using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ReverseAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;
        private readonly RefRO<ReverseTag> ReverseTagRO;

        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public PointData Anchor => AnchorRO.ValueRO;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public ReverseTag ReverseTag => ReverseTagRO.ValueRO;
    }
}
