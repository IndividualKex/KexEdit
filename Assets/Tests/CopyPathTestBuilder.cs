using System;
using System.Collections.Generic;
using KexEdit.Sim;
using Unity.Collections;
using Keyframe = KexEdit.Sim.Keyframe;
using InterpolationType = KexEdit.Sim.InterpolationType;

namespace Tests {
    public struct CopyPathTestData : IDisposable {
        public Point Anchor;
        public NativeArray<Point> SourcePath;
        public float Start;
        public float End;
        public bool FixedVelocity;
        public NativeArray<Keyframe> FixedVelocityKeyframes;
        public NativeArray<Keyframe> HeartOffset;
        public NativeArray<Keyframe> Friction;
        public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;

        public void Dispose() {
            if (SourcePath.IsCreated) SourcePath.Dispose();
            if (FixedVelocityKeyframes.IsCreated) FixedVelocityKeyframes.Dispose();
            if (HeartOffset.IsCreated) HeartOffset.Dispose();
            if (Friction.IsCreated) Friction.Dispose();
            if (Resistance.IsCreated) Resistance.Dispose();
        }
    }

    public static class CopyPathTestBuilder {
        public static CopyPathTestData FromGold(GoldSection section, Allocator allocator = Allocator.TempJob) {
            var anchorData = section.inputs.anchor;

            Point anchor = ForceTestBuilder.ToPoint(anchorData);

            return new CopyPathTestData {
                Anchor = anchor,
                SourcePath = ToPointArray(section.inputs.sourcePath, allocator),
                Start = section.inputs.start,
                End = section.inputs.end,
                FixedVelocity = section.inputs.propertyOverrides?.driven ?? false,
                FixedVelocityKeyframes = ToKeyframeArray(section.inputs.keyframes?.drivenVelocity, allocator),
                HeartOffset = ToKeyframeArray(section.inputs.keyframes?.heart, allocator),
                Friction = ToKeyframeArray(section.inputs.keyframes?.friction, allocator),
                Resistance = ToKeyframeArray(section.inputs.keyframes?.resistance, allocator),
                AnchorHeart = anchorData.heartOffset,
                AnchorFriction = anchorData.friction,
                AnchorResistance = anchorData.resistance,
            };
        }

        private static NativeArray<Point> ToPointArray(List<GoldPointData> points, Allocator allocator) {
            if (points == null || points.Count == 0) {
                return new NativeArray<Point>(0, allocator);
            }

            var result = new NativeArray<Point>(points.Count, allocator);
            for (int i = 0; i < points.Count; i++) {
                result[i] = ForceTestBuilder.ToPoint(points[i]);
            }
            return result;
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
