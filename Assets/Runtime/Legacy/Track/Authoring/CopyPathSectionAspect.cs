using Unity.Entities;

namespace KexEdit {
    public readonly partial struct CopyPathSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> anchor;

        private readonly RefRO<CopyPathSectionTag> copyPathSectionTag;
        private readonly RefRW<PropertyOverrides> propertyOverrides;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<FixedVelocityKeyframe> FixedVelocityKeyframes;
        public readonly DynamicBuffer<HeartKeyframe> HeartKeyframes;
        public readonly DynamicBuffer<FrictionKeyframe> FrictionKeyframes;
        public readonly DynamicBuffer<ResistanceKeyframe> ResistanceKeyframes;

        public PointData Anchor => anchor.ValueRO;

        public bool FixedVelocity {
            get => propertyOverrides.ValueRO.FixedVelocity;
            set => propertyOverrides.ValueRW.FixedVelocity = value;
        }

        public bool HeartOverride {
            get => propertyOverrides.ValueRO.Heart;
            set => propertyOverrides.ValueRW.Heart = value;
        }

        public bool FrictionOverride {
            get => propertyOverrides.ValueRO.Friction;
            set => propertyOverrides.ValueRW.Friction = value;
        }

        public bool ResistanceOverride {
            get => propertyOverrides.ValueRO.Resistance;
            set => propertyOverrides.ValueRW.Resistance = value;
        }

        public CopyPathSectionTag CopyPathSectionTag => copyPathSectionTag.ValueRO;
    }
}
