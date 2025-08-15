using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ForceSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;

        private readonly RefRW<Duration> DurationRW;
        private readonly RefRW<Dirty> DirtyRW;
        private readonly RefRW<PropertyOverrides> PropertyOverridesRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;
        public readonly DynamicBuffer<NormalForceKeyframe> NormalForceKeyframes;
        public readonly DynamicBuffer<LateralForceKeyframe> LateralForceKeyframes;

        public readonly DynamicBuffer<FixedVelocityKeyframe> FixedVelocityKeyframes;
        public readonly DynamicBuffer<HeartKeyframe> HeartKeyframes;
        public readonly DynamicBuffer<FrictionKeyframe> FrictionKeyframes;
        public readonly DynamicBuffer<ResistanceKeyframe> ResistanceKeyframes;

        public PointData Anchor => AnchorRO.ValueRO;

        public DurationType DurationType {
            get => DurationRW.ValueRO.Type;
            set => DurationRW.ValueRW.Type = value;
        }

        public float Duration {
            get => DurationRW.ValueRO.Value;
            set => DurationRW.ValueRW.Value = value;
        }

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
    }
}
