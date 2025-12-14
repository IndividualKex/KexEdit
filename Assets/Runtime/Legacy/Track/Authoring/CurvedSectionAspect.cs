using Unity.Entities;

namespace KexEdit {
    public readonly partial struct CurvedSectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Anchor> anchor;

        private readonly RefRW<PropertyOverrides> propertyOverrides;
        private readonly RefRW<CurveData> curveData;

        public readonly DynamicBuffer<Point> Points;

        public readonly DynamicBuffer<InputPortReference> InputPorts;
        public readonly DynamicBuffer<OutputPortReference> OutputPorts;

        public readonly DynamicBuffer<RollSpeedKeyframe> RollSpeedKeyframes;

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

        public float Radius {
            get => curveData.ValueRO.Radius;
            set => curveData.ValueRW.Radius = value;
        }

        public float Arc {
            get => curveData.ValueRO.Arc;
            set => curveData.ValueRW.Arc = value;
        }

        public float Axis {
            get => curveData.ValueRO.Axis;
            set => curveData.ValueRW.Axis = value;
        }

        public float LeadIn {
            get => curveData.ValueRO.LeadIn;
            set => curveData.ValueRW.LeadIn = value;
        }

        public float LeadOut {
            get => curveData.ValueRO.LeadOut;
            set => curveData.ValueRW.LeadOut = value;
        }
    }
}
