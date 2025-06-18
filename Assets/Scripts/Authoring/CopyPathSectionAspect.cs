using Unity.Entities;

namespace KexEdit {
    public readonly partial struct CopyPathSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;

        private readonly RefRO<CopyPathSectionTag> CopyPathSectionTagRO;
        private readonly RefRW<Dirty> DirtyRW;
        private readonly RefRW<PropertyOverrides> PropertyOverridesRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<FixedVelocityKeyframe> FixedVelocityKeyframes;
        public readonly DynamicBuffer<HeartKeyframe> HeartKeyframes;
        public readonly DynamicBuffer<FrictionKeyframe> FrictionKeyframes;
        public readonly DynamicBuffer<ResistanceKeyframe> ResistanceKeyframes;

        public PointData Anchor => AnchorRO.ValueRO;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }

        public bool FixedVelocity {
            get => PropertyOverridesRW.ValueRO.FixedVelocity;
            set => PropertyOverridesRW.ValueRW.FixedVelocity = value;
        }

        public bool HeartOverride {
            get => PropertyOverridesRW.ValueRO.Heart;
            set => PropertyOverridesRW.ValueRW.Heart = value;
        }

        public bool FrictionOverride {
            get => PropertyOverridesRW.ValueRO.Friction;
            set => PropertyOverridesRW.ValueRW.Friction = value;
        }

        public bool ResistanceOverride {
            get => PropertyOverridesRW.ValueRO.Resistance;
            set => PropertyOverridesRW.ValueRW.Resistance = value;
        }

        public CopyPathSectionTag CopyPathSectionTag => CopyPathSectionTagRO.ValueRO;
    }
}
