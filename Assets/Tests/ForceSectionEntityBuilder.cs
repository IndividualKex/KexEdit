using KexEdit.Legacy;
using System;
using System.Collections.Generic;
using KexEdit;
using Unity.Entities;
using Unity.Mathematics;

namespace Tests {
    public static class ForceSectionEntityBuilder {
        public static Entity Create(EntityManager em, GoldSection section) {
            var entity = em.CreateEntity(
                typeof(Anchor),
                typeof(Duration),
                typeof(Dirty),
                typeof(PropertyOverrides)
            );

            em.AddBuffer<CorePointBuffer>(entity);
            em.AddBuffer<InputPortReference>(entity);
            em.AddBuffer<OutputPortReference>(entity);
            em.AddBuffer<RollSpeedKeyframe>(entity);
            em.AddBuffer<NormalForceKeyframe>(entity);
            em.AddBuffer<LateralForceKeyframe>(entity);
            em.AddBuffer<FixedVelocityKeyframe>(entity);
            em.AddBuffer<HeartKeyframe>(entity);
            em.AddBuffer<FrictionKeyframe>(entity);
            em.AddBuffer<ResistanceKeyframe>(entity);

            em.SetComponentData(entity, new Anchor { Value = ToPointData(section.inputs.anchor) });
            em.SetComponentData(entity, new Duration {
                Type = ParseDurationType(section.inputs.duration.type),
                Value = section.inputs.duration.value
            });
            em.SetComponentEnabled<Dirty>(entity, true);
            em.SetComponentData(entity, ToPropertyOverrides(section.inputs.propertyOverrides));

            PopulateKeyframes(em, entity, section.inputs.keyframes);

            var outputPort = CreateMockOutputPort(em);
            var outputPorts = em.GetBuffer<OutputPortReference>(entity);
            outputPorts.Add(outputPort);

            return entity;
        }

        private static Entity CreateMockOutputPort(EntityManager em) {
            var port = em.CreateEntity(typeof(AnchorPort), typeof(Dirty));
            em.SetComponentData(port, new AnchorPort { Value = PointData.Create() });
            em.SetComponentEnabled<Dirty>(port, false);
            return port;
        }

        private static void PopulateKeyframes(EntityManager em, Entity entity, GoldKeyframes kf) {
            if (kf == null) return;

            if (kf.rollSpeed != null) {
                var buffer = em.GetBuffer<RollSpeedKeyframe>(entity);
                foreach (var k in kf.rollSpeed) buffer.Add(ToKeyframe(k));
            }
            if (kf.normalForce != null) {
                var buffer = em.GetBuffer<NormalForceKeyframe>(entity);
                foreach (var k in kf.normalForce) buffer.Add(ToKeyframe(k));
            }
            if (kf.lateralForce != null) {
                var buffer = em.GetBuffer<LateralForceKeyframe>(entity);
                foreach (var k in kf.lateralForce) buffer.Add(ToKeyframe(k));
            }
            if (kf.fixedVelocity != null) {
                var buffer = em.GetBuffer<FixedVelocityKeyframe>(entity);
                foreach (var k in kf.fixedVelocity) buffer.Add(ToKeyframe(k));
            }
            if (kf.heart != null) {
                var buffer = em.GetBuffer<HeartKeyframe>(entity);
                foreach (var k in kf.heart) buffer.Add(ToKeyframe(k));
            }
            if (kf.friction != null) {
                var buffer = em.GetBuffer<FrictionKeyframe>(entity);
                foreach (var k in kf.friction) buffer.Add(ToKeyframe(k));
            }
            if (kf.resistance != null) {
                var buffer = em.GetBuffer<ResistanceKeyframe>(entity);
                foreach (var k in kf.resistance) buffer.Add(ToKeyframe(k));
            }
        }

        public static PointData ToPointData(GoldPointData p) {
            return new PointData {
                HeartPosition = new float3(p.HeartPosition.x, p.HeartPosition.y, p.HeartPosition.z),
                Direction = new float3(p.direction.x, p.direction.y, p.direction.z),
                Lateral = new float3(p.lateral.x, p.lateral.y, p.lateral.z),
                Normal = new float3(p.normal.x, p.normal.y, p.normal.z),
                Roll = p.roll,
                Velocity = p.velocity,
                Energy = p.energy,
                NormalForce = p.normalForce,
                LateralForce = p.lateralForce,
                HeartAdvance = p.HeartAdvance,
                SpineAdvance = p.SpineAdvance,
                AngleFromLast = p.angleFromLast,
                PitchFromLast = p.pitchFromLast,
                YawFromLast = p.yawFromLast,
                RollSpeed = p.rollSpeed,
                HeartArc = p.HeartArc,
                SpineArc = p.SpineArc,
                FrictionOrigin = p.FrictionOrigin,
                HeartOffset = p.HeartOffset,
                Friction = p.friction,
                Resistance = p.resistance,
                Facing = p.facing
            };
        }

        private static Keyframe ToKeyframe(GoldKeyframe k) {
            return new Keyframe {
                Id = k.id,
                Time = k.time,
                Value = k.value,
                InInterpolation = ParseInterpolationType(k.inInterpolation),
                OutInterpolation = ParseInterpolationType(k.outInterpolation),
                HandleType = ParseHandleType(k.handleType),
                InTangent = k.inTangent,
                OutTangent = k.outTangent,
                InWeight = k.inWeight,
                OutWeight = k.outWeight,
                Flags = KeyframeFlags.None,
                Selected = false
            };
        }

        private static PropertyOverrides ToPropertyOverrides(GoldPropertyOverrides p) {
            if (p == null) return PropertyOverrides.Default;
            var result = new PropertyOverrides();
            result.FixedVelocity = p.fixedVelocity;
            result.Heart = p.heart;
            result.Friction = p.friction;
            result.Resistance = p.resistance;
            return result;
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

        private static HandleType ParseHandleType(string type) {
            return type switch {
                "Free" => HandleType.Free,
                "Aligned" => HandleType.Aligned,
                _ => HandleType.Aligned
            };
        }
    }
}
