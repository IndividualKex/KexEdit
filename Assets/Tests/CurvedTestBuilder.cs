using System;
using System.Collections.Generic;
using KexEdit.Sim;
using Unity.Collections;
using Keyframe = KexEdit.Sim.Keyframe;
using InterpolationType = KexEdit.Sim.InterpolationType;

namespace Tests {
    public struct CurvedTestData : IDisposable {
        public Point Anchor;
        public float Radius;
        public float Arc;
        public float Axis;
        public float LeadIn;
        public float LeadOut;
        public bool FixedVelocity;
        public NativeArray<Keyframe> RollSpeed;
        public NativeArray<Keyframe> FixedVelocityKeyframes;
        public NativeArray<Keyframe> HeartOffset;
        public NativeArray<Keyframe> Friction;
        public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;

        public void Dispose() {
            if (RollSpeed.IsCreated) RollSpeed.Dispose();
            if (FixedVelocityKeyframes.IsCreated) FixedVelocityKeyframes.Dispose();
            if (HeartOffset.IsCreated) HeartOffset.Dispose();
            if (Friction.IsCreated) Friction.Dispose();
            if (Resistance.IsCreated) Resistance.Dispose();
        }
    }

    public static class CurvedTestBuilder {
        public static CurvedTestData FromGold(GoldSection section, Allocator allocator = Allocator.TempJob) {
            var anchorData = section.inputs.anchor;

            Point anchor = ForceTestBuilder.ToPoint(anchorData);

            var curveData = section.inputs.curveData;

            return new CurvedTestData {
                Anchor = anchor,
                Radius = curveData?.radius ?? 0f,
                Arc = curveData?.arc ?? 0f,
                Axis = curveData?.axis ?? 0f,
                LeadIn = curveData?.leadIn ?? 0f,
                LeadOut = curveData?.leadOut ?? 0f,
                FixedVelocity = section.inputs.propertyOverrides?.driven ?? false,
                RollSpeed = ToKeyframeArray(section.inputs.keyframes?.rollSpeed, allocator),
                FixedVelocityKeyframes = ToKeyframeArray(section.inputs.keyframes?.drivenVelocity, allocator),
                HeartOffset = ToKeyframeArray(section.inputs.keyframes?.heart, allocator),
                Friction = ToKeyframeArray(section.inputs.keyframes?.friction, allocator),
                Resistance = ToKeyframeArray(section.inputs.keyframes?.resistance, allocator),
                AnchorHeart = anchorData.heartOffset,
                AnchorFriction = anchorData.friction,
                AnchorResistance = anchorData.resistance,
            };
        }

        private static NativeArray<Keyframe> ToKeyframeArray(List<GoldKeyframe> keyframes, Allocator allocator) {
            if (keyframes == null || keyframes.Count == 0) {
                return new NativeArray<Keyframe>(0, allocator);
            }

            var result = new NativeArray<Keyframe>(keyframes.Count, allocator);
            for (int i = 0; i < keyframes.Count; i++) {
                result[i] = ToKeyframe(keyframes[i]);
            }
            return result;
        }

        private static Keyframe ToKeyframe(GoldKeyframe k) {
            return new Keyframe(
                time: k.time,
                value: k.value,
                inInterpolation: ParseInterpolationType(k.inInterpolation),
                outInterpolation: ParseInterpolationType(k.outInterpolation),
                inTangent: k.inTangent,
                outTangent: k.outTangent,
                inWeight: k.inWeight,
                outWeight: k.outWeight
            );
        }

        private static InterpolationType ParseInterpolationType(string type) {
            return type switch {
                "Constant" => InterpolationType.Constant,
                "Linear" => InterpolationType.Linear,
                "Bezier" => InterpolationType.Bezier,
                _ => InterpolationType.Bezier
            };
        }
    }
}
