using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ReversePathAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<ReversePathTag> reversePathTag;

        private readonly RefRW<Dirty> dirty;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public bool Dirty {
            get => dirty.ValueRO.Value;
            set => dirty.ValueRW.Value = value;
        }

        public ReversePathTag ReversePathTag => reversePathTag.ValueRO;
    }
}
