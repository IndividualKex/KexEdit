using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ReversePathAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<ReversePathTag> ReversePathTagRO;

        private readonly RefRW<Dirty> DirtyRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public ReversePathTag ReversePathTag => ReversePathTagRO.ValueRO;
    }
}
