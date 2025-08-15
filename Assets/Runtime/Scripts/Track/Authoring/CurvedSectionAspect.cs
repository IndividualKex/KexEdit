using Unity.Entities;

namespace KexEdit {
    public readonly partial struct CurvedSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> AnchorRO;

        private readonly RefRW<Dirty> DirtyRW;
        private readonly RefRW<PropertyOverrides> PropertyOverridesRW;
        private readonly RefRW<CurveData> CurveDataRW;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;

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

        public float Radius {
            get => CurveDataRW.ValueRO.Radius;
            set => CurveDataRW.ValueRW.Radius = value;
        }

        public float Arc {
            get => CurveDataRW.ValueRO.Arc;
            set => CurveDataRW.ValueRW.Arc = value;
        }

        public float Axis {
            get => CurveDataRW.ValueRO.Axis;
            set => CurveDataRW.ValueRW.Axis = value;
        }

        public float LeadIn {
            get => CurveDataRW.ValueRO.LeadIn;
            set => CurveDataRW.ValueRW.LeadIn = value;
        }

        public float LeadOut {
            get => CurveDataRW.ValueRO.LeadOut;
            set => CurveDataRW.ValueRW.LeadOut = value;
        }
    }
}
