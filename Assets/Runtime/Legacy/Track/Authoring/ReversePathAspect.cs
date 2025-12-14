using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ReversePathAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<ReversePathTag> reversePathTag;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public ReversePathTag ReversePathTag => reversePathTag.ValueRO;
    }
}
