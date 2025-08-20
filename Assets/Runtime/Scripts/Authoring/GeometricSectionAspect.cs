using Unity.Entities;

namespace KexEdit {
    public readonly partial struct GeometricSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;

        private readonly RefRW<Duration> DurationRW;
        private readonly RefRW<Dirty> DirtyRW;
        private readonly RefRW<PropertyOverrides> PropertyOverridesRW;
        private readonly RefRW<Steering> SteeringRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;
        public readonly DynamicBuffer<PitchSpeedKeyframe> PitchSpeedKeyframes;
        public readonly DynamicBuffer<YawSpeedKeyframe> YawSpeedKeyframes;

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

        public bool Steering {
            get => SteeringRW.ValueRO.Value;
            set => SteeringRW.ValueRW.Value = value;
        }
    }
}
