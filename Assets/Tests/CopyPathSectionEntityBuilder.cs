using KexEdit;
using Unity.Entities;

namespace Tests {
    public static class CopyPathSectionEntityBuilder {
        public static Entity Create(EntityManager em, GoldSection section) {
            var entity = em.CreateEntity(
                typeof(Anchor),
                typeof(CopyPathSectionTag),
                typeof(Dirty),
                typeof(PropertyOverrides)
            );

            em.AddBuffer<Point>(entity);
            em.AddBuffer<InputPortReference>(entity);
            em.AddBuffer<OutputPortReference>(entity);
            em.AddBuffer<FixedVelocityKeyframe>(entity);
            em.AddBuffer<HeartKeyframe>(entity);
            em.AddBuffer<FrictionKeyframe>(entity);
            em.AddBuffer<ResistanceKeyframe>(entity);

            em.SetComponentData(entity, new Anchor { Value = ForceSectionEntityBuilder.ToPointData(section.inputs.anchor) });
            em.SetComponentEnabled<Dirty>(entity, true);
            em.SetComponentData(entity, ToPropertyOverrides(section.inputs.propertyOverrides));

            PopulateKeyframes(em, entity, section.inputs.keyframes);

            var anchorPort = CreateMockAnchorPort(em);
            var pathPort = CreatePathPort(em, section);
            var startPort = CreateStartPort(em, section.inputs.start);
            var endPort = CreateEndPort(em, section.inputs.end);
            var outputPort = CreateMockOutputPort(em);

            var inputPorts = em.GetBuffer<InputPortReference>(entity);
            inputPorts.Add(anchorPort);
            inputPorts.Add(pathPort);
            inputPorts.Add(startPort);
            inputPorts.Add(endPort);

            var outputPorts = em.GetBuffer<OutputPortReference>(entity);
            outputPorts.Add(outputPort);

            return entity;
        }

        private static Entity CreateMockAnchorPort(EntityManager em) {
            var port = em.CreateEntity(typeof(AnchorPort), typeof(Dirty));
            em.SetComponentData(port, new AnchorPort { Value = PointData.Create() });
            em.SetComponentEnabled<Dirty>(port, false);
            return port;
        }

        private static Entity CreatePathPort(EntityManager em, GoldSection section) {
            var port = em.CreateEntity(typeof(Dirty));
            em.AddBuffer<PathPort>(port);
            em.SetComponentEnabled<Dirty>(port, false);

            var pathBuffer = em.GetBuffer<PathPort>(port);
            if (section.inputs.sourcePath != null) {
                foreach (var point in section.inputs.sourcePath) {
                    pathBuffer.Add(new PathPort { Value = ForceSectionEntityBuilder.ToPointData(point) });
                }
            }

            return port;
        }

        private static Entity CreateStartPort(EntityManager em, float start) {
            var port = em.CreateEntity(typeof(StartPort), typeof(Dirty));
            em.SetComponentData(port, new StartPort { Value = start });
            em.SetComponentEnabled<Dirty>(port, false);
            return port;
        }

        private static Entity CreateEndPort(EntityManager em, float end) {
            var port = em.CreateEntity(typeof(EndPort), typeof(Dirty));
            em.SetComponentData(port, new EndPort { Value = end });
            em.SetComponentEnabled<Dirty>(port, false);
            return port;
        }

        private static Entity CreateMockOutputPort(EntityManager em) {
            var port = em.CreateEntity(typeof(AnchorPort), typeof(Dirty));
            em.SetComponentData(port, new AnchorPort { Value = PointData.Create() });
            em.SetComponentEnabled<Dirty>(port, false);
            return port;
        }

        private static void PopulateKeyframes(EntityManager em, Entity entity, GoldKeyframes kf) {
            if (kf == null) return;

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
