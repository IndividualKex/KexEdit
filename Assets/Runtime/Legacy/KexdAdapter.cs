using KexEdit.Coaster;
using KexEdit.Core;
using KexEdit.Legacy.Serialization;
using KexEdit.Nodes;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoasterAggregate = KexEdit.Coaster.Coaster;
using CoreDuration = KexEdit.Coaster.Duration;
using CoreDurationType = KexEdit.Coaster.DurationType;
using CoreKeyframe = KexEdit.Core.Keyframe;
using CoreInterpolationType = KexEdit.Core.InterpolationType;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class KexdAdapter {
        public static void ImportToEcs(
            in CoasterAggregate coaster,
            in KexEdit.Persistence.UIMetadataChunk uiMetadata,
            Entity coasterEntity,
            EntityManager entityManager,
            bool restoreUIState
        ) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                BuildSerializedNode(
                    in coaster,
                    in uiMetadata,
                    i,
                    Allocator.Temp,
                    out var serializedNode
                );

                SerializationSystem.Instance.DeserializeNode(
                    serializedNode,
                    coasterEntity,
                    ref ecb,
                    restoreUIState
                );

                serializedNode.Dispose();
            }

            ecb.Playback(entityManager);
            ecb.Dispose();

            BuildConnections(coaster, coasterEntity, entityManager);
        }

        [BurstCompile]
        private static void BuildSerializedNode(
            in CoasterAggregate coaster,
            in KexEdit.Persistence.UIMetadataChunk uiMetadata,
            int nodeIndex,
            Allocator allocator,
            out SerializedNode result
        ) {
            uint nodeId = coaster.Graph.NodeIds[nodeIndex];
            uint coreNodeType = coaster.Graph.NodeTypes[nodeIndex];

            float2 position = float2.zero;
            if (uiMetadata.Positions.IsCreated) {
                uiMetadata.Positions.TryGetValue(nodeId, out position);
            }

            var legacyType = CoreToLegacyNodeType((Nodes.NodeType)coreNodeType);

            var node = new Node {
                Id = nodeId,
                Type = legacyType,
                Position = position,
                Selected = false,
                Priority = 0
            };

            BuildInputPorts(in coaster, (Nodes.NodeType)coreNodeType, nodeId, nodeIndex, allocator, out var inputPorts);
            BuildOutputPorts(in coaster, (Nodes.NodeType)coreNodeType, nodeId, nodeIndex, allocator, out var outputPorts);

            BuildAnchor(in coaster, nodeId, out var anchor);
            ExtractDuration(in coaster, nodeId, out var duration);
            bool steering = coaster.Steering.Contains(nodeId);

            ExtractRollSpeedKeyframes(in coaster, nodeId, allocator, out var rollSpeedKf);
            ExtractNormalForceKeyframes(in coaster, nodeId, allocator, out var normalForceKf);
            ExtractLateralForceKeyframes(in coaster, nodeId, allocator, out var lateralForceKf);
            ExtractPitchSpeedKeyframes(in coaster, nodeId, allocator, out var pitchSpeedKf);
            ExtractYawSpeedKeyframes(in coaster, nodeId, allocator, out var yawSpeedKf);
            ExtractFixedVelocityKeyframes(in coaster, nodeId, allocator, out var fixedVelocityKf);
            ExtractHeartKeyframes(in coaster, nodeId, allocator, out var heartKf);
            ExtractFrictionKeyframes(in coaster, nodeId, allocator, out var frictionKf);
            ExtractResistanceKeyframes(in coaster, nodeId, allocator, out var resistanceKf);
            ExtractTrackStyleKeyframes(in coaster, nodeId, allocator, out var trackStyleKf);

            var fieldFlags = BuildFieldFlags(
                legacyType,
                duration.Type != DurationType.Time,
                steering,
                hasPropertyOverrides: false,
                hasCurveData: false,
                hasMeshFilePath: false
            );

            result = new SerializedNode {
                Node = node,
                Anchor = anchor,
                FieldFlags = fieldFlags,
                BooleanFlags = BuildBooleanFlags(steering),
                Duration = duration,
                InputPorts = inputPorts,
                OutputPorts = outputPorts,
                RollSpeedKeyframes = rollSpeedKf,
                NormalForceKeyframes = normalForceKf,
                LateralForceKeyframes = lateralForceKf,
                PitchSpeedKeyframes = pitchSpeedKf,
                YawSpeedKeyframes = yawSpeedKf,
                FixedVelocityKeyframes = fixedVelocityKf,
                HeartKeyframes = heartKf,
                FrictionKeyframes = frictionKf,
                ResistanceKeyframes = resistanceKf,
                TrackStyleKeyframes = trackStyleKf,
                PropertyOverrides = default,
                SelectedProperties = default,
                CurveData = default,
                MeshFilePath = default
            };
        }

        [BurstCompile]
        private static Legacy.NodeType CoreToLegacyNodeType(Nodes.NodeType type) {
            return type switch {
                Nodes.NodeType.Force => Legacy.NodeType.ForceSection,
                Nodes.NodeType.Geometric => Legacy.NodeType.GeometricSection,
                Nodes.NodeType.Curved => Legacy.NodeType.CurvedSection,
                Nodes.NodeType.CopyPath => Legacy.NodeType.CopyPathSection,
                Nodes.NodeType.Anchor => Legacy.NodeType.Anchor,
                Nodes.NodeType.Reverse => Legacy.NodeType.Reverse,
                Nodes.NodeType.ReversePath => Legacy.NodeType.ReversePath,
                Nodes.NodeType.Bridge => Legacy.NodeType.Bridge,
                _ => Legacy.NodeType.Anchor
            };
        }

        [BurstCompile]
        private static void BuildInputPorts(
            in CoasterAggregate coaster,
            Nodes.NodeType coreType,
            uint nodeId,
            int nodeIndex,
            Allocator allocator,
            out NativeArray<SerializedPort> result
        ) {
            int inputCount = NodeSchema.InputCount(coreType);
            result = new NativeArray<SerializedPort>(inputCount, allocator);

            for (int i = 0; i < inputCount; i++) {
                var portType = GetLegacyPortType(coreType, i, true);

                uint portId = FindPortId(in coaster, nodeId, true, i);

                var port = new Port {
                    Id = portId,
                    Type = portType,
                    IsInput = true
                };

                ExtractPortValue(in coaster, nodeId, portId, portType, out var value);

                result[i] = new SerializedPort {
                    Port = port,
                    Value = value
                };
            }
        }

        [BurstCompile]
        private static void BuildOutputPorts(
            in CoasterAggregate coaster,
            Nodes.NodeType coreType,
            uint nodeId,
            int nodeIndex,
            Allocator allocator,
            out NativeArray<SerializedPort> result
        ) {
            int outputCount = NodeSchema.OutputCount(coreType);
            result = new NativeArray<SerializedPort>(outputCount, allocator);

            for (int i = 0; i < outputCount; i++) {
                var portType = GetLegacyPortType(coreType, i, false);

                uint portId = FindPortId(in coaster, nodeId, false, i);

                var port = new Port {
                    Id = portId,
                    Type = portType,
                    IsInput = false
                };

                ExtractPortValue(in coaster, nodeId, portId, portType, out var value);

                result[i] = new SerializedPort {
                    Port = port,
                    Value = value
                };
            }
        }

        [BurstCompile]
        private static uint FindPortId(in CoasterAggregate coaster, uint nodeId, bool isInput, int portIndex) {
            int currentIndex = 0;
            for (int i = 0; i < coaster.Graph.PortIds.Length; i++) {
                if (coaster.Graph.PortOwners[i] != nodeId) continue;
                if (coaster.Graph.PortIsInput[i] != isInput) continue;

                if (currentIndex == portIndex) {
                    return coaster.Graph.PortIds[i];
                }
                currentIndex++;
            }
            return 0;
        }

        [BurstCompile]
        private static Legacy.PortType GetLegacyPortType(Nodes.NodeType coreType, int portIndex, bool isInput) {
            if (!isInput) {
                return portIndex switch {
                    0 => Legacy.PortType.Anchor,
                    1 => Legacy.PortType.Path,
                    _ => Legacy.PortType.Anchor
                };
            }

            return (coreType, portIndex) switch {
                (Nodes.NodeType.Anchor, 0) => Legacy.PortType.Position,
                (Nodes.NodeType.Anchor, 1) => Legacy.PortType.Rotation,
                (Nodes.NodeType.Anchor, 2) => Legacy.PortType.Velocity,
                (Nodes.NodeType.Anchor, 3) => Legacy.PortType.Heart,
                (Nodes.NodeType.Anchor, 4) => Legacy.PortType.Friction,
                (Nodes.NodeType.Anchor, 5) => Legacy.PortType.Resistance,

                (Nodes.NodeType.Force, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.Force, 1) => Legacy.PortType.Duration,

                (Nodes.NodeType.Geometric, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.Geometric, 1) => Legacy.PortType.Duration,

                (Nodes.NodeType.Curved, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.Curved, 1) => Legacy.PortType.Radius,
                (Nodes.NodeType.Curved, 2) => Legacy.PortType.Arc,
                (Nodes.NodeType.Curved, 3) => Legacy.PortType.Axis,
                (Nodes.NodeType.Curved, 4) => Legacy.PortType.LeadIn,
                (Nodes.NodeType.Curved, 5) => Legacy.PortType.LeadOut,

                (Nodes.NodeType.CopyPath, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.CopyPath, 1) => Legacy.PortType.Path,
                (Nodes.NodeType.CopyPath, 2) => Legacy.PortType.Start,
                (Nodes.NodeType.CopyPath, 3) => Legacy.PortType.End,

                (Nodes.NodeType.Bridge, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.Bridge, 1) => Legacy.PortType.Anchor,
                (Nodes.NodeType.Bridge, 2) => Legacy.PortType.OutWeight,
                (Nodes.NodeType.Bridge, 3) => Legacy.PortType.InWeight,

                (Nodes.NodeType.Reverse, 0) => Legacy.PortType.Anchor,
                (Nodes.NodeType.ReversePath, 0) => Legacy.PortType.Path,

                _ => Legacy.PortType.Anchor
            };
        }

        [BurstCompile]
        private static void ExtractPortValue(
            in CoasterAggregate coaster,
            uint nodeId,
            uint portId,
            Legacy.PortType portType,
            out PointData result
        ) {
            switch (portType) {
                case Legacy.PortType.Anchor:
                    BuildAnchorPortValue(in coaster, nodeId, out result);
                    return;

                case Legacy.PortType.Position:
                    var pos = coaster.Vectors.TryGetValue(nodeId, out var p) ? p : float3.zero;
                    result = new PointData { HeartPosition = pos };
                    return;

                case Legacy.PortType.Rotation:
                    var rot = coaster.GetRotation(nodeId);
                    result = new PointData {
                        Roll = rot.x,
                        HeartPosition = new float3(rot.y, rot.z, 0)
                    };
                    return;

                case Legacy.PortType.Duration:
                    if (coaster.Durations.TryGetValue(nodeId, out var dur)) {
                        result = new PointData { Roll = dur.Value };
                        return;
                    }
                    result = default;
                    return;

                case Legacy.PortType.Friction:
                    if (coaster.Scalars.TryGetValue(portId, out float frictionPhysics)) {
                        result = new PointData { Roll = frictionPhysics * Constants.FRICTION_PHYSICS_TO_UI_SCALE };
                        return;
                    }
                    result = default;
                    return;

                case Legacy.PortType.Resistance:
                    if (coaster.Scalars.TryGetValue(portId, out float resistancePhysics)) {
                        result = new PointData { Roll = resistancePhysics * Constants.RESISTANCE_PHYSICS_TO_UI_SCALE };
                        return;
                    }
                    result = default;
                    return;

                case Legacy.PortType.Velocity:
                case Legacy.PortType.Heart:
                case Legacy.PortType.Radius:
                case Legacy.PortType.Arc:
                case Legacy.PortType.Axis:
                case Legacy.PortType.LeadIn:
                case Legacy.PortType.LeadOut:
                case Legacy.PortType.Start:
                case Legacy.PortType.End:
                case Legacy.PortType.OutWeight:
                case Legacy.PortType.InWeight:
                    if (coaster.Scalars.TryGetValue(portId, out float value)) {
                        result = new PointData { Roll = value };
                        return;
                    }
                    result = default;
                    return;

                default:
                    result = default;
                    return;
            }
        }

        [BurstCompile]
        private static void BuildAnchorPortValue(in CoasterAggregate coaster, uint nodeId, out PointData result) {
            var position = coaster.Vectors.TryGetValue(nodeId, out var p) ? p : float3.zero;
            var rotation = coaster.GetRotation(nodeId);
            var frame = Frame.FromEuler(rotation.y, rotation.z, rotation.x);

            result = new PointData {
                HeartPosition = position,
                Direction = frame.Direction,
                Normal = frame.Normal,
                Lateral = frame.Lateral,
                Velocity = 0f,
                Roll = rotation.x,
                Energy = 0f
            };
        }

        [BurstCompile]
        private static void BuildAnchor(in CoasterAggregate coaster, uint nodeId, out PointData result) {
            BuildAnchorPortValue(in coaster, nodeId, out result);
        }

        [BurstCompile]
        private static void ExtractDuration(in CoasterAggregate coaster, uint nodeId, out Legacy.Duration result) {
            if (coaster.Durations.TryGetValue(nodeId, out var dur)) {
                result = new Legacy.Duration {
                    Value = dur.Value,
                    Type = dur.Type == CoreDurationType.Time
                        ? Legacy.DurationType.Time
                        : Legacy.DurationType.Distance
                };
                return;
            }
            result = new Legacy.Duration {
                Value = 1f,
                Type = Legacy.DurationType.Time
            };
        }

        [BurstCompile]
        private static NodeFlags BuildBooleanFlags(bool steering) {
            var flags = NodeFlags.Render;
            if (steering) {
                flags |= NodeFlags.Steering;
            }
            return flags;
        }

        [BurstCompile]
        private static NodeFieldFlags BuildFieldFlags(
            Legacy.NodeType nodeType,
            bool hasDistanceDuration,
            bool hasSteering,
            bool hasPropertyOverrides,
            bool hasCurveData,
            bool hasMeshFilePath
        ) {
            var flags = NodeFieldFlags.None;

            if (hasDistanceDuration) {
                flags |= NodeFieldFlags.HasDuration;
            }
            if (hasSteering) {
                flags |= NodeFieldFlags.HasSteering;
            }
            if (hasPropertyOverrides) {
                flags |= NodeFieldFlags.HasPropertyOverrides;
            }
            if (hasCurveData) {
                flags |= NodeFieldFlags.HasCurveData;
            }
            if (hasMeshFilePath) {
                flags |= NodeFieldFlags.HasMeshFilePath;
            }

            return flags;
        }

        [BurstCompile]
        private static void ExtractRollSpeedKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<RollSpeedKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.RollSpeed, out var kfs)) {
                result = new NativeArray<RollSpeedKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<RollSpeedKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractNormalForceKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<NormalForceKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.NormalForce, out var kfs)) {
                result = new NativeArray<NormalForceKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<NormalForceKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractLateralForceKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<LateralForceKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.LateralForce, out var kfs)) {
                result = new NativeArray<LateralForceKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<LateralForceKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractPitchSpeedKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<PitchSpeedKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.PitchSpeed, out var kfs)) {
                result = new NativeArray<PitchSpeedKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<PitchSpeedKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractYawSpeedKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<YawSpeedKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.YawSpeed, out var kfs)) {
                result = new NativeArray<YawSpeedKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<YawSpeedKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractFixedVelocityKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<FixedVelocityKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.DrivenVelocity, out var kfs)) {
                result = new NativeArray<FixedVelocityKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<FixedVelocityKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractHeartKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<HeartKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.HeartOffset, out var kfs)) {
                result = new NativeArray<HeartKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<HeartKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractFrictionKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<FrictionKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.Friction, out var kfs)) {
                result = new NativeArray<FrictionKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<FrictionKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractResistanceKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<ResistanceKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.Resistance, out var kfs)) {
                result = new NativeArray<ResistanceKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<ResistanceKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        private static void ExtractTrackStyleKeyframes(
            in CoasterAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<TrackStyleKeyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.TrackStyle, out var kfs)) {
                result = new NativeArray<TrackStyleKeyframe>(0, allocator);
                return;
            }

            result = new NativeArray<TrackStyleKeyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = new TrackStyleKeyframe {
                    Value = new Legacy.Keyframe {
                        Id = legacyKf.Id,
                        Time = legacyKf.Time,
                        Value = (uint)legacyKf.Value,
                        InInterpolation = legacyKf.InInterpolation,
                        OutInterpolation = legacyKf.OutInterpolation,
                        HandleType = legacyKf.HandleType,
                        Flags = legacyKf.Flags,
                        InTangent = legacyKf.InTangent,
                        OutTangent = legacyKf.OutTangent,
                        InWeight = legacyKf.InWeight,
                        OutWeight = legacyKf.OutWeight,
                        Selected = legacyKf.Selected
                    }
                };
            }
        }

        [BurstCompile]
        private static Legacy.InterpolationType ConvertInterpolationType(CoreInterpolationType coreType) {
            return coreType switch {
                CoreInterpolationType.Constant => Legacy.InterpolationType.Constant,
                CoreInterpolationType.Linear => Legacy.InterpolationType.Linear,
                CoreInterpolationType.Bezier => Legacy.InterpolationType.Bezier,
                _ => Legacy.InterpolationType.Linear
            };
        }

        [BurstCompile]
        private static void CoreToLegacyKeyframe(in CoreKeyframe coreKf, out Legacy.Keyframe result) {
            result = new Legacy.Keyframe {
                Id = 0,
                Time = coreKf.Time,
                Value = coreKf.Value,
                InInterpolation = ConvertInterpolationType(coreKf.InInterpolation),
                OutInterpolation = ConvertInterpolationType(coreKf.OutInterpolation),
                HandleType = Legacy.HandleType.Aligned,
                Flags = Legacy.KeyframeFlags.None,
                InTangent = coreKf.InTangent,
                OutTangent = coreKf.OutTangent,
                InWeight = coreKf.InWeight,
                OutWeight = coreKf.OutWeight,
                Selected = false
            };
        }

        private static void BuildConnections(CoasterAggregate coaster, Entity coasterEntity, EntityManager entityManager) {
            var portQuery = entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);

            var portMap = new NativeHashMap<uint, Entity>(ports.Length, Allocator.Temp);
            foreach (var port in ports) {
                uint id = entityManager.GetComponentData<Port>(port).Id;
                portMap[id] = port;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < coaster.Graph.EdgeIds.Length; i++) {
                uint edgeId = coaster.Graph.EdgeIds[i];
                uint sourcePortId = coaster.Graph.EdgeSources[i];
                uint targetPortId = coaster.Graph.EdgeTargets[i];

                if (!portMap.TryGetValue(sourcePortId, out var source)) continue;
                if (!portMap.TryGetValue(targetPortId, out var target)) continue;

                var connection = ecb.CreateEntity();
                ecb.AddComponent<Dirty>(connection);
                ecb.AddComponent<CoasterReference>(connection, coasterEntity);
                ecb.AddComponent(connection, new Connection {
                    Id = edgeId,
                    Source = source,
                    Target = target,
                    Selected = false
                });
                ecb.SetName(connection, "Connection");
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            portMap.Dispose();
        }
    }
}
