using KexEdit.Document;
using KexEdit.Sim;
using KexEdit.Legacy.Serialization;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Anchor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DocumentAggregate = KexEdit.Document.Document;
using CoreInterpolationType = KexEdit.Sim.InterpolationType;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using SchemaNodeType = KexEdit.Sim.Schema.NodeType;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class EcsAdapter {
        public static void ImportToEcs(
            in DocumentAggregate coaster,
            in KexEdit.Persistence.UIStateChunk uiState,
            Entity coasterEntity,
            EntityManager entityManager,
            bool restoreUIState
        ) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                BuildSerializedNode(
                    in coaster,
                    in uiState,
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

            BuildConnections(coaster, coasterEntity, entityManager, in uiState);
        }

        [BurstCompile]
        public static void BuildSerializedNode(
            in DocumentAggregate coaster,
            in KexEdit.Persistence.UIStateChunk uiState,
            int nodeIndex,
            Allocator allocator,
            out SerializedNode result
        ) {
            uint nodeId = coaster.Graph.NodeIds[nodeIndex];
            uint coreNodeType = coaster.Graph.NodeTypes[nodeIndex];

            float2 position = float2.zero;
            if (uiState.NodePositions.IsCreated && uiState.NodePositions.TryGetValue(nodeId, out position)) {
            }
            else if (nodeIndex < coaster.Graph.NodePositions.Length) {
                position = coaster.Graph.NodePositions[nodeIndex];
            }

            var legacyType = CoreToLegacyNodeType((SchemaNodeType)coreNodeType);

            ulong priorityKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Priority);
            int priority = coaster.Scalars.TryGetValue(priorityKey, out float pf) ? (int)pf : 0;

            var node = new Node {
                Id = nodeId,
                Type = legacyType,
                Position = position,
                Selected = uiState.SelectedNodeIds.IsCreated && uiState.SelectedNodeIds.Contains(nodeId),
                Priority = priority
            };

            BuildInputPorts(in coaster, (SchemaNodeType)coreNodeType, nodeId, nodeIndex, allocator, out var inputPorts);
            BuildOutputPorts(in coaster, (SchemaNodeType)coreNodeType, nodeId, nodeIndex, allocator, out var outputPorts);

            BuildAnchor(in coaster, nodeId, out var anchor);
            ExtractDuration(in coaster, nodeId, out var duration);
            bool steering = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.Steering), out int s) && s == 1;
            bool render = !coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.Render), out int r) || r == 0;

            ExtractKeyframesForNode(
                in coaster,
                (SchemaNodeType)coreNodeType,
                nodeId,
                allocator,
                out var rollSpeedKf,
                out var normalForceKf,
                out var lateralForceKf,
                out var pitchSpeedKf,
                out var yawSpeedKf,
                out var fixedVelocityKf,
                out var heartKf,
                out var frictionKf,
                out var resistanceKf,
                out var trackStyleKf
            );

            ExtractPropertyOverrides(in coaster, nodeId, out var propertyOverrides);
            bool hasPropertyOverrides = propertyOverrides.Flags != PropertyOverrideFlags.None;

            var fieldFlags = BuildFieldFlags(
                legacyType,
                duration.Type != DurationType.Time,
                steering,
                render,
                hasPropertyOverrides,
                hasCurveData: false,
                hasMeshFilePath: false
            );

            result = new SerializedNode {
                Node = node,
                Anchor = anchor,
                FieldFlags = fieldFlags,
                BooleanFlags = BuildBooleanFlags(steering, render),
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
                PropertyOverrides = propertyOverrides,
                SelectedProperties = default,
                CurveData = default,
                MeshFilePath = default
            };
        }

        [BurstCompile]
        public static void ExtractKeyframesForNode(
            in DocumentAggregate coaster,
            SchemaNodeType nodeType,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> rollSpeed,
            out NativeArray<Keyframe> normalForce,
            out NativeArray<Keyframe> lateralForce,
            out NativeArray<Keyframe> pitchSpeed,
            out NativeArray<Keyframe> yawSpeed,
            out NativeArray<Keyframe> fixedVelocity,
            out NativeArray<Keyframe> heart,
            out NativeArray<Keyframe> friction,
            out NativeArray<Keyframe> resistance,
            out NativeArray<Keyframe> trackStyle
        ) {
            bool HasProperty(PropertyId propertyId) {
                int propertyCount = NodeSchema.PropertyCount(nodeType);
                for (int i = 0; i < propertyCount; i++) {
                    if (NodeSchema.Property(nodeType, i) == propertyId) {
                        return true;
                    }
                }
                return false;
            }

            if (HasProperty(PropertyId.RollSpeed)) {
                ExtractRollSpeedKeyframes(in coaster, nodeId, allocator, out rollSpeed);
            }
            else {
                rollSpeed = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.NormalForce)) {
                ExtractNormalForceKeyframes(in coaster, nodeId, allocator, out normalForce);
            }
            else {
                normalForce = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.LateralForce)) {
                ExtractLateralForceKeyframes(in coaster, nodeId, allocator, out lateralForce);
            }
            else {
                lateralForce = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.PitchSpeed)) {
                ExtractPitchSpeedKeyframes(in coaster, nodeId, allocator, out pitchSpeed);
            }
            else {
                pitchSpeed = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.YawSpeed)) {
                ExtractYawSpeedKeyframes(in coaster, nodeId, allocator, out yawSpeed);
            }
            else {
                yawSpeed = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.DrivenVelocity)) {
                ExtractFixedVelocityKeyframes(in coaster, nodeId, allocator, out fixedVelocity);
            }
            else {
                fixedVelocity = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.HeartOffset)) {
                ExtractHeartKeyframes(in coaster, nodeId, allocator, out heart);
            }
            else {
                heart = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.Friction)) {
                ExtractFrictionKeyframes(in coaster, nodeId, allocator, out friction);
            }
            else {
                friction = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.Resistance)) {
                ExtractResistanceKeyframes(in coaster, nodeId, allocator, out resistance);
            }
            else {
                resistance = new NativeArray<Keyframe>(0, allocator);
            }

            if (HasProperty(PropertyId.TrackStyle)) {
                ExtractTrackStyleKeyframes(in coaster, nodeId, allocator, out trackStyle);
            }
            else {
                trackStyle = new NativeArray<Keyframe>(0, allocator);
            }
        }

        [BurstCompile]
        private static void ExtractPropertyOverrides(in DocumentAggregate coaster, uint nodeId, out PropertyOverrides result) {
            bool driven = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.Driven), out int d) && d == 1;
            bool heart = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideHeart), out int h) && h == 1;
            bool friction = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideFriction), out int f) && f == 1;
            bool resistance = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideResistance), out int r) && r == 1;
            bool trackStyle = coaster.Flags.TryGetValue(DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideTrackStyle), out int t) && t == 1;

            result = new PropertyOverrides();
            result.FixedVelocity = driven;
            result.Heart = heart;
            result.Friction = friction;
            result.Resistance = resistance;
            result.TrackStyle = trackStyle;
        }

        [BurstCompile]
        private static NodeType CoreToLegacyNodeType(SchemaNodeType type) {
            return type switch {
                SchemaNodeType.Force => NodeType.ForceSection,
                SchemaNodeType.Geometric => NodeType.GeometricSection,
                SchemaNodeType.Curved => NodeType.CurvedSection,
                SchemaNodeType.CopyPath => NodeType.CopyPathSection,
                SchemaNodeType.Anchor => NodeType.Anchor,
                SchemaNodeType.Reverse => NodeType.Reverse,
                SchemaNodeType.ReversePath => NodeType.ReversePath,
                SchemaNodeType.Bridge => NodeType.Bridge,
                _ => NodeType.Anchor
            };
        }

        [BurstCompile]
        private static void BuildInputPorts(
            in DocumentAggregate coaster,
            SchemaNodeType coreType,
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

                ExtractInputPortValue(in coaster, nodeId, i, portType, out var value);

                result[i] = new SerializedPort {
                    Port = port,
                    Value = value
                };
            }
        }

        [BurstCompile]
        private static void BuildOutputPorts(
            in DocumentAggregate coaster,
            SchemaNodeType coreType,
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

                result[i] = new SerializedPort {
                    Port = port,
                    Value = default
                };
            }
        }

        [BurstCompile]
        private static uint FindPortId(in DocumentAggregate coaster, uint nodeId, bool isInput, int portIndex) {
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
        private static PortType GetLegacyPortType(SchemaNodeType coreType, int portIndex, bool isInput) {
            if (!isInput) {
                return portIndex switch {
                    0 => PortType.Anchor,
                    1 => PortType.Path,
                    _ => PortType.Anchor
                };
            }

            return (coreType, portIndex) switch {
                (SchemaNodeType.Anchor, 0) => PortType.Position,
                (SchemaNodeType.Anchor, 1) => PortType.Roll,
                (SchemaNodeType.Anchor, 2) => PortType.Pitch,
                (SchemaNodeType.Anchor, 3) => PortType.Yaw,
                (SchemaNodeType.Anchor, 4) => PortType.Velocity,
                (SchemaNodeType.Anchor, 5) => PortType.Heart,
                (SchemaNodeType.Anchor, 6) => PortType.Friction,
                (SchemaNodeType.Anchor, 7) => PortType.Resistance,

                (SchemaNodeType.Force, 0) => PortType.Anchor,
                (SchemaNodeType.Force, 1) => PortType.Duration,

                (SchemaNodeType.Geometric, 0) => PortType.Anchor,
                (SchemaNodeType.Geometric, 1) => PortType.Duration,

                (SchemaNodeType.Curved, 0) => PortType.Anchor,
                (SchemaNodeType.Curved, 1) => PortType.Radius,
                (SchemaNodeType.Curved, 2) => PortType.Arc,
                (SchemaNodeType.Curved, 3) => PortType.Axis,
                (SchemaNodeType.Curved, 4) => PortType.LeadIn,
                (SchemaNodeType.Curved, 5) => PortType.LeadOut,

                (SchemaNodeType.CopyPath, 0) => PortType.Anchor,
                (SchemaNodeType.CopyPath, 1) => PortType.Path,
                (SchemaNodeType.CopyPath, 2) => PortType.Start,
                (SchemaNodeType.CopyPath, 3) => PortType.End,

                (SchemaNodeType.Bridge, 0) => PortType.Anchor,
                (SchemaNodeType.Bridge, 1) => PortType.Anchor,
                (SchemaNodeType.Bridge, 2) => PortType.OutWeight,
                (SchemaNodeType.Bridge, 3) => PortType.InWeight,

                (SchemaNodeType.Reverse, 0) => PortType.Anchor,
                (SchemaNodeType.ReversePath, 0) => PortType.Path,

                _ => PortType.Anchor
            };
        }

        [BurstCompile]
        private static void ExtractInputPortValue(
            in DocumentAggregate coaster,
            uint nodeId,
            int inputIndex,
            PortType portType,
            out PointData result
        ) {
            ulong key = DocumentAggregate.InputKey(nodeId, inputIndex);

            switch (portType) {
                case PortType.Anchor:
                case PortType.Path:
                    result = default;
                    return;

                case PortType.Position:
                    var pos = coaster.Vectors.TryGetValue(key, out var p) ? p : float3.zero;
                    result = new PointData { HeartPosition = pos };
                    return;

                case PortType.Roll:
                case PortType.Pitch:
                case PortType.Yaw:
                    if (coaster.Scalars.TryGetValue(key, out float rotValue)) {
                        result = new PointData { Roll = math.degrees(rotValue) };
                        return;
                    }
                    result = default;
                    return;

                case PortType.Duration:
                    ulong durKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Duration);
                    if (coaster.Scalars.TryGetValue(durKey, out float durValue)) {
                        result = new PointData { Roll = durValue };
                        return;
                    }
                    result = default;
                    return;

                case PortType.Friction:
                    if (coaster.Scalars.TryGetValue(key, out float frictionPhysics)) {
                        result = new PointData { Roll = frictionPhysics * Constants.FRICTION_PHYSICS_TO_UI_SCALE };
                        return;
                    }
                    result = default;
                    return;

                case PortType.Resistance:
                    if (coaster.Scalars.TryGetValue(key, out float resistancePhysics)) {
                        result = new PointData { Roll = resistancePhysics * Constants.RESISTANCE_PHYSICS_TO_UI_SCALE };
                        return;
                    }
                    result = default;
                    return;

                case PortType.Velocity:
                case PortType.Heart:
                case PortType.Radius:
                case PortType.Arc:
                case PortType.Axis:
                case PortType.LeadIn:
                case PortType.LeadOut:
                case PortType.Start:
                case PortType.End:
                case PortType.OutWeight:
                case PortType.InWeight:
                    if (coaster.Scalars.TryGetValue(key, out float value)) {
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
        private static void BuildAnchorPortValue(in DocumentAggregate coaster, uint nodeId, out PointData result) {
            ulong posKey = DocumentAggregate.InputKey(nodeId, AnchorPorts.Position);
            var position = coaster.Vectors.TryGetValue(posKey, out var p) ? p : float3.zero;

            coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Roll), out float roll);
            coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Pitch), out float pitch);
            coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Yaw), out float yaw);

            var frame = Frame.FromEuler(pitch, yaw, roll);

            result = new PointData {
                HeartPosition = position,
                Direction = frame.Direction,
                Normal = frame.Normal,
                Lateral = frame.Lateral,
                Velocity = 0f,
                Roll = roll,
                Energy = 0f
            };
        }

        [BurstCompile]
        private static void BuildAnchor(in DocumentAggregate coaster, uint nodeId, out PointData result) {
            BuildAnchorPortValue(in coaster, nodeId, out result);
            ulong facingKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Facing);
            result.Facing = coaster.Flags.TryGetValue(facingKey, out int facing) ? facing : 1;
        }

        [BurstCompile]
        private static void ExtractDuration(in DocumentAggregate coaster, uint nodeId, out Duration result) {
            ulong durKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Duration);
            ulong durTypeKey = DocumentAggregate.InputKey(nodeId, NodeMeta.DurationType);

            float value = coaster.Scalars.TryGetValue(durKey, out float v) ? v : 1f;
            bool isDistance = coaster.Flags.TryGetValue(durTypeKey, out int t) && t == 1;

            result = new Duration {
                Value = value,
                Type = isDistance ? DurationType.Distance : DurationType.Time
            };
        }

        [BurstCompile]
        private static NodeFlags BuildBooleanFlags(bool steering, bool render) {
            var flags = NodeFlags.None;
            if (render) {
                flags |= NodeFlags.Render;
            }
            if (steering) {
                flags |= NodeFlags.Steering;
            }
            return flags;
        }

        [BurstCompile]
        private static NodeFieldFlags BuildFieldFlags(
            NodeType nodeType,
            bool hasDistanceDuration,
            bool hasSteering,
            bool hasRender,
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
            if (hasRender) {
                flags |= NodeFieldFlags.HasRender;
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
        public static void ExtractRollSpeedKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.RollSpeed, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.RollSpeed, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractNormalForceKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.NormalForce, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.NormalForce, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractLateralForceKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.LateralForce, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.LateralForce, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractPitchSpeedKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.PitchSpeed, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.PitchSpeed, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractYawSpeedKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.YawSpeed, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.YawSpeed, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractFixedVelocityKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.DrivenVelocity, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.DrivenVelocity, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractHeartKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.HeartOffset, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.HeartOffset, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractFrictionKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.Friction, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.Friction, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractResistanceKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.Resistance, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                legacyKf.Id = math.hash(new uint3(nodeId, (uint)PropertyId.Resistance, (uint)i));
                result[i] = legacyKf;
            }
        }

        [BurstCompile]
        public static void ExtractTrackStyleKeyframes(
            in DocumentAggregate coaster,
            uint nodeId,
            Allocator allocator,
            out NativeArray<Keyframe> result
        ) {
            if (!coaster.Keyframes.TryGet(nodeId, PropertyId.TrackStyle, out var kfs)) {
                result = new NativeArray<Keyframe>(0, allocator);
                return;
            }

            result = new NativeArray<Keyframe>(kfs.Length, allocator);
            for (int i = 0; i < kfs.Length; i++) {
                var coreKf = kfs[i];
                CoreToLegacyKeyframe(in coreKf, out var legacyKf);
                result[i] = new Keyframe {
                    Id = math.hash(new uint3(nodeId, (uint)PropertyId.TrackStyle, (uint)i)),
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
                };
            }
        }

        [BurstCompile]
        private static InterpolationType ConvertInterpolationType(CoreInterpolationType coreType) {
            return coreType switch {
                CoreInterpolationType.Constant => InterpolationType.Constant,
                CoreInterpolationType.Linear => InterpolationType.Linear,
                CoreInterpolationType.Bezier => InterpolationType.Bezier,
                _ => InterpolationType.Linear
            };
        }

        [BurstCompile]
        private static void CoreToLegacyKeyframe(in CoreKeyframe coreKf, out Keyframe result) {
            result = new Keyframe {
                Id = 0,
                Time = coreKf.Time,
                Value = coreKf.Value,
                InInterpolation = ConvertInterpolationType(coreKf.InInterpolation),
                OutInterpolation = ConvertInterpolationType(coreKf.OutInterpolation),
                HandleType = HandleType.Aligned,
                Flags = KeyframeFlags.None,
                InTangent = coreKf.InTangent,
                OutTangent = coreKf.OutTangent,
                InWeight = coreKf.InWeight,
                OutWeight = coreKf.OutWeight,
                Selected = false
            };
        }

        private static void BuildConnections(DocumentAggregate coaster, Entity coasterEntity, EntityManager entityManager, in KexEdit.Persistence.UIStateChunk uiState) {
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
                    Selected = uiState.SelectedConnectionIds.IsCreated && uiState.SelectedConnectionIds.Contains(edgeId)
                });
                ecb.SetName(connection, "Connection");
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            portMap.Dispose();
        }
    }
}
