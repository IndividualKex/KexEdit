using System;
using System.Collections.Generic;
using KexEdit.Core;
using Unity.Collections;
using Unity.Mathematics;
using Keyframe = KexEdit.Core.Keyframe;
using InterpolationType = KexEdit.Core.InterpolationType;

namespace Tests {
    public struct BridgeTestData : IDisposable {
        public Point Anchor;
        public Point TargetAnchor;
        public float InWeight;
        public float OutWeight;
        public bool FixedVelocity;
        public NativeArray<Keyframe> FixedVelocityKeyframes;
        public NativeArray<Keyframe> HeartOffset;
        public NativeArray<Keyframe> Friction;
        public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;

        public void Dispose() {
            if (FixedVelocityKeyframes.IsCreated) FixedVelocityKeyframes.Dispose();
            if (HeartOffset.IsCreated) HeartOffset.Dispose();
            if (Friction.IsCreated) Friction.Dispose();
            if (Resistance.IsCreated) Resistance.Dispose();
        }
    }

    public static class BridgeTestBuilder {
        public static BridgeTestData FromGold(GoldSection section, Allocator allocator = Allocator.TempJob) {
            var anchorData = section.inputs.anchor;

            Point anchor = ForceTestBuilder.ToPoint(anchorData);

            Point targetAnchor;
            if (section.inputs.targetAnchor != null) {
                var targetData = section.inputs.targetAnchor;
                targetAnchor = ForceTestBuilder.ToPoint(targetData);
            }
            else {
                targetAnchor = Point.Default;
            }

            return new BridgeTestData {
                Anchor = anchor,
                TargetAnchor = targetAnchor,
                InWeight = section.inputs.inWeight > 0f ? section.inputs.inWeight : 0.3f,
                OutWeight = section.inputs.outWeight > 0f ? section.inputs.outWeight : 0.3f,
                FixedVelocity = section.inputs.propertyOverrides?.fixedVelocity ?? false,
                FixedVelocityKeyframes = ToKeyframeArray(section.inputs.keyframes?.fixedVelocity, allocator),
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
