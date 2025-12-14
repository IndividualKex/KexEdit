using System;
using System.Collections.Generic;
using KexEdit.Core;
using KexEdit.Nodes;
using Unity.Collections;
using Unity.Mathematics;
using Keyframe = KexEdit.Core.Keyframe;
using InterpolationType = KexEdit.Core.InterpolationType;

namespace Tests {
    public struct ForceTestData : IDisposable {
        public Point Anchor;
        public IterationConfig Config;
        public bool FixedVelocity;
        public NativeArray<Keyframe> RollSpeed;
        public NativeArray<Keyframe> NormalForce;
        public NativeArray<Keyframe> LateralForce;
        public NativeArray<Keyframe> FixedVelocityKeyframes;
        public NativeArray<Keyframe> HeartOffset;
        public NativeArray<Keyframe> Friction;
        public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;

        public void Dispose() {
            if (RollSpeed.IsCreated) RollSpeed.Dispose();
            if (NormalForce.IsCreated) NormalForce.Dispose();
            if (LateralForce.IsCreated) LateralForce.Dispose();
            if (FixedVelocityKeyframes.IsCreated) FixedVelocityKeyframes.Dispose();
            if (HeartOffset.IsCreated) HeartOffset.Dispose();
            if (Friction.IsCreated) Friction.Dispose();
            if (Resistance.IsCreated) Resistance.Dispose();
        }
    }

    public static class ForceTestBuilder {
        public static ForceTestData FromGold(GoldSection section, Allocator allocator = Allocator.TempJob) {
            var anchorData = section.inputs.anchor;

            Point anchor = ToPoint(anchorData);

            IterationConfig config = new(
                section.inputs.duration.value,
                ParseDurationType(section.inputs.duration.type)
            );

            return new ForceTestData {
                Anchor = anchor,
                Config = config,
                FixedVelocity = section.inputs.propertyOverrides?.fixedVelocity ?? false,
                RollSpeed = ToKeyframeArray(section.inputs.keyframes?.rollSpeed, allocator),
                NormalForce = ToKeyframeArray(section.inputs.keyframes?.normalForce, allocator),
                LateralForce = ToKeyframeArray(section.inputs.keyframes?.lateralForce, allocator),
                FixedVelocityKeyframes = ToKeyframeArray(section.inputs.keyframes?.fixedVelocity, allocator),
                HeartOffset = ToKeyframeArray(section.inputs.keyframes?.heart, allocator),
                Friction = ToKeyframeArray(section.inputs.keyframes?.friction, allocator),
                Resistance = ToKeyframeArray(section.inputs.keyframes?.resistance, allocator),
                AnchorHeart = anchorData.heart,
                AnchorFriction = anchorData.friction,
                AnchorResistance = anchorData.resistance,
            };
        }

        public static Point ToPoint(GoldPointData p) {
            return new Point(
                direction: new float3(p.direction.x, p.direction.y, p.direction.z),
                lateral: new float3(p.lateral.x, p.lateral.y, p.lateral.z),
                normal: new float3(p.normal.x, p.normal.y, p.normal.z),
                spinePosition: new float3(p.position.x, p.position.y, p.position.z),
                velocity: p.velocity,
                energy: p.energy,
                normalForce: p.normalForce,
                lateralForce: p.lateralForce,
                heartArc: p.totalLength,
                spineArc: p.totalHeartLength,
                spineAdvance: p.heartDistanceFromLast,
                frictionOrigin: p.frictionCompensation,
                rollSpeed: p.rollSpeed,
                heartOffset: p.heart,
                friction: p.friction,
                resistance: p.resistance
            );
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

        private static DurationType ParseDurationType(string type) {
            return type switch {
                "Time" => DurationType.Time,
                "Distance" => DurationType.Distance,
                _ => DurationType.Time
            };
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
