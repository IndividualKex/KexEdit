using System;
using KexEdit.Document;
using KexEdit.Graph;
using KexEdit.Graph.Typed;
using KexEdit.Legacy.Serialization;
using KexEdit.Sim.Nodes.Anchor;
using KexEdit.Sim.Nodes.Bridge;
using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using DocumentAggregate = KexEdit.Document.Document;
using CoreInterpolationType = KexEdit.Sim.InterpolationType;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using SchemaNodeType = KexEdit.Sim.Schema.NodeType;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class LegacyImporter {

        private struct LegacyNode : IDisposable {
            public Node Node;
            public PointData Anchor;
            public NodeFieldFlags FieldFlags;
            public NodeFlags BooleanFlags;
            public PropertyOverrides PropertyOverrides;
            public SelectedProperties SelectedProperties;
            public CurveData CurveData;
            public Duration Duration;
            public FixedString512Bytes MeshFilePath;
            public NativeArray<SerializedPort> InputPorts;
            public NativeArray<SerializedPort> OutputPorts;

            public bool Render {
                get => (BooleanFlags & NodeFlags.Render) != 0;
                set => BooleanFlags = value ? BooleanFlags | NodeFlags.Render : BooleanFlags & ~NodeFlags.Render;
            }

            public bool Selected {
                get => (BooleanFlags & NodeFlags.Selected) != 0;
                set => BooleanFlags = value ? BooleanFlags | NodeFlags.Selected : BooleanFlags & ~NodeFlags.Selected;
            }

            public bool Steering {
                get => (BooleanFlags & NodeFlags.Steering) != 0;
                set => BooleanFlags = value ? BooleanFlags | NodeFlags.Steering : BooleanFlags & ~NodeFlags.Steering;
            }

            public void Dispose() {
                if (InputPorts.IsCreated) InputPorts.Dispose();
                if (OutputPorts.IsCreated) OutputPorts.Dispose();
            }
        }

        [BurstCompile]
        public static void Import(ref NativeArray<byte> kexData, Allocator allocator, out DocumentAggregate coaster, out SerializedUIState uiState) {
            coaster = DocumentAggregate.Create(allocator);
            ImportInternal(ref kexData, allocator, ref coaster, out uiState);
        }

        [BurstCompile]
        private static void ImportInternal(ref NativeArray<byte> kexData, Allocator allocator, ref DocumentAggregate coaster, out SerializedUIState uiState) {
            var reader = new BinaryReader(kexData);

            int version = ReadHeader(ref reader, out uiState);

            int nodeCount = reader.Read<int>();
            var nodes = new NativeArray<LegacyNode>(nodeCount, Allocator.Temp);
            var nodeIds = new NativeArray<uint>(nodeCount, Allocator.Temp);
            uint nodeIdCounter = 1;

            for (int i = 0; i < nodeCount; i++) {
                ReadNode(ref reader, version, ref nodeIdCounter, out var node);
                nodes[i] = node;
                nodeIds[i] = node.Node.Id;
                ReadKeyframesToStore(ref reader, version, nodeIds[i], ref coaster.Keyframes);
            }

            reader.ReadArray(out NativeArray<SerializedEdge> edges, Allocator.Temp);

            var nodeIdRemap = new NativeHashMap<int, uint>(16, Allocator.Temp);
            var portIdRemap = new NativeHashMap<uint, uint>(64, Allocator.Temp);

            ImportGraphFromNodes(in nodes, in edges, ref coaster.Graph, allocator, ref nodeIdRemap, ref portIdRemap);

            for (int i = 0; i < nodeCount; i++) {
                var node = nodes[i];
                uint nodeId = nodeIdRemap.TryGetValue(i, out uint remappedId) ? remappedId : node.Node.Id;
                if (nodeId != nodeIds[i]) {
                    RemapKeyframesInStore(nodeIds[i], nodeId, ref coaster.Keyframes);
                }
                ImportNodeDataFromLegacyNode(in node, nodeId, ref coaster);
            }

            ImportBridgeTargetsFromNodes(in nodes, ref coaster, allocator, in nodeIdRemap);
            ImportInferredAnchorsFromNodes(in nodes, ref coaster, allocator, in nodeIdRemap);

            nodeIdRemap.Dispose();
            portIdRemap.Dispose();
            edges.Dispose();
            nodeIds.Dispose();
            for (int i = 0; i < nodeCount; i++) {
                var node = nodes[i];
                node.Dispose();
            }
            nodes.Dispose();
        }

        [BurstCompile]
        private static void RemapKeyframesInStore(uint oldNodeId, uint newNodeId, ref KexEdit.Sim.Schema.KeyframeStore keyframes) {
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.RollSpeed, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.NormalForce, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.LateralForce, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.PitchSpeed, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.YawSpeed, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.DrivenVelocity, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.HeartOffset, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.Friction, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.Resistance, ref keyframes);
            RemapKeyframesForProperty(oldNodeId, newNodeId, PropertyId.TrackStyle, ref keyframes);
        }

        [BurstCompile]
        private static void RemapKeyframesForProperty(uint oldNodeId, uint newNodeId, PropertyId propertyId, ref KexEdit.Sim.Schema.KeyframeStore keyframes) {
            if (!keyframes.TryGet(oldNodeId, propertyId, out var kfs)) return;
            var copy = new NativeArray<CoreKeyframe>(kfs.Length, Allocator.Temp);
            for (int i = 0; i < kfs.Length; i++) {
                copy[i] = kfs[i];
            }
            keyframes.Remove(oldNodeId, propertyId);
            keyframes.Set(newNodeId, propertyId, in copy);
            copy.Dispose();
        }

        [BurstCompile]
        private static int ReadHeader(ref BinaryReader reader, out SerializedUIState uiState) {
            int version = reader.Read<int>();

            if (version >= SerializationVersion.UI_STATE_SERIALIZATION) {
                uiState.TimelineOffset = reader.Read<float>();
                uiState.TimelineZoom = reader.Read<float>();
                uiState.NodeGraphPanX = reader.Read<float>();
                uiState.NodeGraphPanY = reader.Read<float>();
                uiState.NodeGraphZoom = reader.Read<float>();
                uiState.CameraTargetPositionX = reader.Read<float>();
                uiState.CameraTargetPositionY = reader.Read<float>();
                uiState.CameraTargetPositionZ = reader.Read<float>();
                uiState.CameraTargetDistance = reader.Read<float>();
                uiState.CameraTargetPitch = reader.Read<float>();
                uiState.CameraTargetYaw = reader.Read<float>();
                uiState.CameraSpeedMultiplier = reader.Read<float>();
                uiState.CameraPositionX = uiState.CameraTargetPositionX;
                uiState.CameraPositionY = uiState.CameraTargetPositionY;
                uiState.CameraPositionZ = uiState.CameraTargetPositionZ;
                uiState.CameraDistance = uiState.CameraTargetDistance;
                uiState.CameraPitch = uiState.CameraTargetPitch;
                uiState.CameraYaw = uiState.CameraTargetYaw;
            }
            else {
                float3 defaultPosition = new(6f, 6f, 6f);
                float3 defaultEuler = new(30f, -135f, 0f);
                uiState = new SerializedUIState {
                    TimelineOffset = 0f,
                    TimelineZoom = 1f,
                    NodeGraphPanX = 0f,
                    NodeGraphPanY = 0f,
                    NodeGraphZoom = 1f,
                    CameraPositionX = defaultPosition.x,
                    CameraPositionY = defaultPosition.y,
                    CameraPositionZ = defaultPosition.z,
                    CameraTargetPositionX = defaultPosition.x,
                    CameraTargetPositionY = defaultPosition.y,
                    CameraTargetPositionZ = defaultPosition.z,
                    CameraDistance = math.length(defaultPosition),
                    CameraTargetDistance = math.length(defaultPosition),
                    CameraPitch = defaultEuler.x,
                    CameraTargetPitch = defaultEuler.x,
                    CameraYaw = defaultEuler.y,
                    CameraTargetYaw = defaultEuler.y,
                    CameraSpeedMultiplier = 1f
                };
            }

            return version;
        }

        [BurstCompile]
        private static void ReadNode(ref BinaryReader reader, int version, ref uint nodeIdCounter, out LegacyNode node) {
            node = default;

            if (version < SerializationVersion.NODE_ID) {
                var nodeV1 = reader.Read<NodeV1>();
                node.Node = nodeV1.ToCurrentNode(nodeIdCounter++);
            }
            else {
                node.Node = reader.Read<Node>();
            }

            node.Anchor = reader.Read<PointData>();

            uint flags = reader.Read<uint>();
            node.FieldFlags = (NodeFieldFlags)flags;

            if ((node.FieldFlags & (NodeFieldFlags.HasRender | NodeFieldFlags.HasSelected | NodeFieldFlags.HasSteering)) != 0) {
                node.BooleanFlags = (NodeFlags)reader.Read<byte>();
            }

            node.PropertyOverrides = (node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0 ? reader.Read<PropertyOverrides>() : default;
            node.SelectedProperties = (node.FieldFlags & NodeFieldFlags.HasSelectedProperties) != 0 ? reader.Read<SelectedProperties>() : default;
            node.CurveData = (node.FieldFlags & NodeFieldFlags.HasCurveData) != 0 ? reader.Read<CurveData>() : default;
            node.Duration = (node.FieldFlags & NodeFieldFlags.HasDuration) != 0 ? reader.Read<Duration>() : default;
            node.MeshFilePath = (node.FieldFlags & NodeFieldFlags.HasMeshFilePath) != 0 ? reader.Read<FixedString512Bytes>() : default;

            reader.ReadArray(out node.InputPorts, Allocator.Temp);

            if (version < SerializationVersion.BRIDGE_WEIGHT_PORTS && node.Node.Type == NodeType.Bridge) {
                MigrateBridgeWeightPorts(ref node);
            }

            reader.ReadArray(out node.OutputPorts, Allocator.Temp);

            if (version < SerializationVersion.COPY_PATH_TRIM_PORTS && node.Node.Type == NodeType.CopyPathSection) {
                MigrateCopyPathTrimPorts(ref node);
            }
        }

        [BurstCompile]
        private static void MigrateBridgeWeightPorts(ref LegacyNode node) {
            bool hasInWeight = false;
            bool hasOutWeight = false;
            for (int i = 0; i < node.InputPorts.Length; i++) {
                var t = node.InputPorts[i].Port.Type;
                if (t == PortType.InWeight) hasInWeight = true;
                else if (t == PortType.OutWeight) hasOutWeight = true;
            }
            if (hasInWeight && hasOutWeight) return;

            int extra = (hasInWeight ? 0 : 1) + (hasOutWeight ? 0 : 1);
            var oldPorts = node.InputPorts;
            node.InputPorts = new NativeArray<SerializedPort>(oldPorts.Length + extra, Allocator.Temp);
            for (int i = 0; i < oldPorts.Length; i++) node.InputPorts[i] = oldPorts[i];

            uint idLocal = 1;
            int idx = oldPorts.Length;
            if (!hasOutWeight) {
                node.InputPorts[idx++] = new SerializedPort {
                    Port = Port.Create(PortType.OutWeight, true, idLocal++),
                    Value = new PointData { Roll = 0.3f }
                };
            }
            if (!hasInWeight) {
                node.InputPorts[idx++] = new SerializedPort {
                    Port = Port.Create(PortType.InWeight, true, idLocal++),
                    Value = new PointData { Roll = 0.3f }
                };
            }
            oldPorts.Dispose();
        }

        [BurstCompile]
        private static void MigrateCopyPathTrimPorts(ref LegacyNode node) {
            var oldPorts = node.InputPorts;
            node.InputPorts = new NativeArray<SerializedPort>(oldPorts.Length + 2, Allocator.Temp);

            for (int i = 0; i < oldPorts.Length; i++) {
                node.InputPorts[i] = oldPorts[i];
            }

            uint id = 1;
            node.InputPorts[oldPorts.Length] = new SerializedPort {
                Port = Port.Create(PortType.Start, true, id++),
                Value = new PointData { Roll = 0f }
            };
            node.InputPorts[oldPorts.Length + 1] = new SerializedPort {
                Port = Port.Create(PortType.End, true, id++),
                Value = new PointData { Roll = -1f }
            };

            oldPorts.Dispose();
        }

        [BurstCompile]
        private static void ReadKeyframesToStore(ref BinaryReader reader, int version, uint nodeId, ref KexEdit.Sim.Schema.KeyframeStore keyframes) {
            if (version < SerializationVersion.PRECISION_MIGRATION) {
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.RollSpeed, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.NormalForce, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.LateralForce, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.PitchSpeed, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.YawSpeed, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.DrivenVelocity, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.HeartOffset, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.Friction, ref keyframes);
                ReadLegacyKeyframeArray(ref reader, nodeId, PropertyId.Resistance, ref keyframes);
            }
            else {
                ReadKeyframeArray(ref reader, nodeId, PropertyId.RollSpeed, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.NormalForce, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.LateralForce, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.PitchSpeed, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.YawSpeed, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.DrivenVelocity, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.HeartOffset, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.Friction, ref keyframes);
                ReadKeyframeArray(ref reader, nodeId, PropertyId.Resistance, ref keyframes);
                if (version >= SerializationVersion.TRACK_STYLE_PROPERTY) {
                    ReadKeyframeArray(ref reader, nodeId, PropertyId.TrackStyle, ref keyframes);
                }
            }
        }

        [BurstCompile]
        private static void ReadKeyframeArray(ref BinaryReader reader, uint nodeId, PropertyId propertyId, ref KexEdit.Sim.Schema.KeyframeStore keyframes) {
            reader.ReadArray(out NativeArray<Keyframe> legacyKeyframes, Allocator.Temp);
            if (legacyKeyframes.Length == 0) {
                legacyKeyframes.Dispose();
                return;
            }

            var converted = new NativeArray<CoreKeyframe>(legacyKeyframes.Length, Allocator.Temp);
            for (int i = 0; i < legacyKeyframes.Length; i++) {
                var legacy = legacyKeyframes[i];
                converted[i] = new CoreKeyframe(
                    time: legacy.Time,
                    value: legacy.Value,
                    inInterpolation: (CoreInterpolationType)legacy.InInterpolation,
                    outInterpolation: (CoreInterpolationType)legacy.OutInterpolation,
                    inTangent: legacy.InTangent,
                    outTangent: legacy.OutTangent,
                    inWeight: legacy.InWeight,
                    outWeight: legacy.OutWeight
                );
            }

            keyframes.Set(nodeId, propertyId, in converted);
            converted.Dispose();
            legacyKeyframes.Dispose();
        }

        [BurstCompile]
        private static void ReadLegacyKeyframeArray(ref BinaryReader reader, uint nodeId, PropertyId propertyId, ref KexEdit.Sim.Schema.KeyframeStore keyframes) {
            reader.ReadArray(out NativeArray<KeyframeV1> legacy, Allocator.Temp);
            if (legacy.Length == 0) {
                legacy.Dispose();
                return;
            }

            var converted = new NativeArray<CoreKeyframe>(legacy.Length, Allocator.Temp);
            for (int i = 0; i < legacy.Length; i++) {
                var kf = legacy[i].ToCurrentKeyframe();
                converted[i] = new CoreKeyframe(
                    time: kf.Time,
                    value: kf.Value,
                    inInterpolation: (CoreInterpolationType)kf.InInterpolation,
                    outInterpolation: (CoreInterpolationType)kf.OutInterpolation,
                    inTangent: kf.InTangent,
                    outTangent: kf.OutTangent,
                    inWeight: kf.InWeight,
                    outWeight: kf.OutWeight
                );
            }

            keyframes.Set(nodeId, propertyId, in converted);
            converted.Dispose();
            legacy.Dispose();
        }

        [BurstCompile]
        private static void ImportGraphFromNodes(
            in NativeArray<LegacyNode> nodes,
            in NativeArray<SerializedEdge> edges,
            ref KexEdit.Graph.Graph graph,
            Allocator allocator,
            ref NativeHashMap<int, uint> nodeIdRemap,
            ref NativeHashMap<uint, uint> portIdRemap
        ) {
            uint maxNodeId = 0;
            uint maxPortId = 0;
            uint maxEdgeId = 0;

            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                maxNodeId = math.max(maxNodeId, node.Node.Id);
                for (int j = 0; j < node.InputPorts.Length; j++) {
                    maxPortId = math.max(maxPortId, node.InputPorts[j].Port.Id);
                }
                for (int j = 0; j < node.OutputPorts.Length; j++) {
                    maxPortId = math.max(maxPortId, node.OutputPorts[j].Port.Id);
                }
            }

            for (int i = 0; i < edges.Length; i++) {
                maxEdgeId = math.max(maxEdgeId, edges[i].Id);
            }

            uint nextNodeId = maxNodeId + 1;
            uint nextPortId = maxPortId + 1;
            graph.NextEdgeId = maxEdgeId + 1;

            var seenNodes = new NativeHashSet<uint>(nodes.Length, Allocator.Temp);

            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                uint nodeId = node.Node.Id;

                if (!seenNodes.Add(nodeId)) {
                    nodeId = nextNodeId++;
                    nodeIdRemap[i] = nodeId;
                }

                uint nodeType = ConvertNodeType(node.Node.Type);
                float2 position = node.Node.Position;
                bool isBridge = node.Node.Type == NodeType.Bridge;
                bool isAnchor = node.Node.Type == NodeType.Anchor;

                int bridgeAnchorPortCount = 0;
                if (isBridge) {
                    for (int j = 0; j < node.InputPorts.Length; j++) {
                        if (node.InputPorts[j].Port.Type == PortType.Anchor) {
                            bridgeAnchorPortCount++;
                        }
                    }
                }
                bool bridgeNeedsNewTargetPort = isBridge && bridgeAnchorPortCount < 2;
                int extraInputs = (bridgeNeedsNewTargetPort ? 1 : 0) + (isAnchor ? 1 : 0);

                int nodeIndex = graph.NodeIds.Length;
                graph.NodeIds.Add(nodeId);
                graph.NodeTypes.Add(nodeType);
                graph.NodePositions.Add(position);
                graph.NodeInputCount.Add(node.InputPorts.Length + extraInputs);
                graph.NodeOutputCount.Add(node.OutputPorts.Length);
                graph.NodeIndexMap[nodeId] = nodeIndex;

                int inputIndex = 0;
                var coreNodeType = (SchemaNodeType)nodeType;

                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    uint portId = port.Port.Id;

                    if (portIdRemap.ContainsKey(portId)) {
                        portId = nextPortId++;
                    }
                    portIdRemap[port.Port.Id] = portId;

                    NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);

                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(portId);
                    graph.PortTypes.Add(portSpec.ToEncoded());
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(true);
                    graph.PortIndexMap[portId] = portIndex;
                    inputIndex++;
                }

                if (bridgeNeedsNewTargetPort) {
                    uint targetPortId = nextPortId++;
                    NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);
                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(targetPortId);
                    graph.PortTypes.Add(portSpec.ToEncoded());
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(true);
                    graph.PortIndexMap[targetPortId] = portIndex;
                    inputIndex++;
                }

                if (isAnchor) {
                    uint velocityPortId = nextPortId++;
                    NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);
                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(velocityPortId);
                    graph.PortTypes.Add(portSpec.ToEncoded());
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(true);
                    graph.PortIndexMap[velocityPortId] = portIndex;
                }

                for (int j = 0; j < node.OutputPorts.Length; j++) {
                    var port = node.OutputPorts[j];
                    uint portId = port.Port.Id;

                    if (portIdRemap.ContainsKey(portId)) {
                        portId = nextPortId++;
                    }
                    portIdRemap[port.Port.Id] = portId;

                    NodeSchema.OutputSpec(coreNodeType, j, out var portSpec);

                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(portId);
                    graph.PortTypes.Add(portSpec.ToEncoded());
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(false);
                    graph.PortIndexMap[portId] = portIndex;
                }
            }

            seenNodes.Dispose();

            var seenEdges = new NativeHashSet<uint>(edges.Length, Allocator.Temp);

            for (int i = 0; i < edges.Length; i++) {
                var edge = edges[i];
                if (!seenEdges.Add(edge.Id)) continue;

                uint sourceId = edge.SourceId;
                uint targetId = edge.TargetId;

                if (portIdRemap.TryGetValue(sourceId, out uint remappedSource)) {
                    sourceId = remappedSource;
                }
                if (portIdRemap.TryGetValue(targetId, out uint remappedTarget)) {
                    targetId = remappedTarget;
                }

                if (!graph.PortIndexMap.ContainsKey(sourceId) || !graph.PortIndexMap.ContainsKey(targetId)) {
                    continue;
                }

                int edgeIndex = graph.EdgeIds.Length;
                graph.EdgeIds.Add(edge.Id);
                graph.EdgeSources.Add(sourceId);
                graph.EdgeTargets.Add(targetId);
                graph.EdgeIndexMap[edge.Id] = edgeIndex;
            }

            seenEdges.Dispose();

            graph.NextNodeId = nextNodeId;
            graph.NextPortId = nextPortId;
        }

        [BurstCompile]
        private static void ImportNodeDataFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            ImportDurationFromLegacyNode(in node, nodeId, ref coaster);
            ImportSteeringFromLegacyNode(in node, nodeId, ref coaster);
            ImportPropertyOverridesFromLegacyNode(in node, nodeId, ref coaster);
            ImportFacingFromLegacyNode(in node, nodeId, ref coaster);
            ImportPriorityFromLegacyNode(in node, nodeId, ref coaster);
            ImportRenderFromLegacyNode(in node, nodeId, ref coaster);
            ImportPortValuesFromLegacyNode(in node, nodeId, ref coaster);
            ImportAnchorDataFromLegacyNode(in node, nodeId, ref coaster);
        }

        [BurstCompile]
        private static void ImportDurationFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasDuration) != 0) {
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Duration)] = node.Duration.Value;
                if (node.Duration.Type == DurationType.Distance) {
                    coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.DurationType)] = 1;
                }
            }
        }

        [BurstCompile]
        private static void ImportSteeringFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            if (node.Steering) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Steering)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportPropertyOverridesFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) == 0) return;

            if (node.PropertyOverrides.FixedVelocity) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Driven)] = 1;
            }
            if (node.PropertyOverrides.Heart) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideHeart)] = 1;
            }
            if (node.PropertyOverrides.Friction) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideFriction)] = 1;
            }
            if (node.PropertyOverrides.Resistance) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideResistance)] = 1;
            }
            if (node.PropertyOverrides.TrackStyle) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideTrackStyle)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportFacingFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            int facing = node.Anchor.Facing;
            if (facing != 1) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Facing)] = facing;
            }
        }

        [BurstCompile]
        private static void ImportPriorityFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            int priority = node.Node.Priority;
            if (priority != 0) {
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Priority)] = priority;
            }
        }

        [BurstCompile]
        private static void ImportRenderFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            if (!node.Render) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Render)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportPortValuesFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            for (int i = 0; i < node.InputPorts.Length; i++) {
                var port = node.InputPorts[i];
                var portType = port.Port.Type;
                var value = port.Value;
                ulong key = DocumentAggregate.InputKey(nodeId, i);

                switch (portType) {
                    case PortType.Anchor:
                    case PortType.Path:
                        break;
                    case PortType.Position:
                        coaster.Vectors[key] = new float3(value.Roll, value.Velocity, value.Energy);
                        break;
                    case PortType.Rotation:
                        break;
                    case PortType.Roll:
                    case PortType.Pitch:
                    case PortType.Yaw:
                        coaster.Scalars[key] = math.radians(value.Roll);
                        break;
                    case PortType.Axis:
                    case PortType.Radius:
                    case PortType.Arc:
                    case PortType.LeadIn:
                    case PortType.LeadOut:
                    case PortType.InWeight:
                    case PortType.OutWeight:
                    case PortType.Start:
                    case PortType.End:
                    case PortType.Velocity:
                    case PortType.Heart:
                    case PortType.Friction:
                    case PortType.Resistance:
                        coaster.Scalars[key] = value.Roll;
                        break;
                }
            }
        }

        [BurstCompile]
        private static void ImportAnchorDataFromLegacyNode(in LegacyNode node, uint nodeId, ref DocumentAggregate coaster) {
            if (node.Node.Type != NodeType.Anchor) return;

            coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Velocity)] = node.Anchor.Velocity;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Heart)] = node.Anchor.HeartOffset;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Friction)] = node.Anchor.Friction;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Resistance)] = node.Anchor.Resistance;

            bool hasPosition = !math.all(node.Anchor.HeartPosition == float3.zero);
            bool hasDirection = !math.all(node.Anchor.Direction == float3.zero);

            if (hasPosition) {
                coaster.Vectors[DocumentAggregate.InputKey(nodeId, AnchorPorts.Position)] = node.Anchor.HeartPosition;
            }

            if (hasDirection) {
                var frame = new KexEdit.Sim.Frame(
                    math.normalizesafe(node.Anchor.Direction, math.back()),
                    math.normalizesafe(node.Anchor.Normal, math.down()),
                    math.normalizesafe(node.Anchor.Lateral, math.right())
                );
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Roll)] = frame.Roll;
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Pitch)] = frame.Pitch;
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, AnchorPorts.Yaw)] = frame.Yaw;
            }
        }

        [BurstCompile]
        private static void ImportBridgeTargetsFromNodes(
            in NativeArray<LegacyNode> nodes,
            ref DocumentAggregate coaster,
            Allocator allocator,
            in NativeHashMap<int, uint> nodeIdRemap
        ) {
            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                if (node.Node.Type != NodeType.Bridge) continue;

                uint nodeId = nodeIdRemap.TryGetValue(i, out uint remappedId) ? remappedId : node.Node.Id;

                bool hasTargetConnection = false;
                if (coaster.Graph.TryGetInput(nodeId, BridgePorts.Target, out uint targetPortId)) {
                    for (int j = 0; j < coaster.Graph.EdgeIds.Length; j++) {
                        if (coaster.Graph.EdgeTargets[j] == targetPortId) {
                            hasTargetConnection = true;
                            break;
                        }
                    }
                }

                if (hasTargetConnection) continue;

                var anchor = node.Anchor;
                bool hasPosition = !math.all(anchor.HeartPosition == float3.zero);
                bool hasDirection = !math.all(anchor.Direction == float3.zero);
                if (!hasPosition && !hasDirection) continue;

                float2 anchorPos = node.Node.Position + new float2(-100f, 50f);
                uint anchorNodeId = coaster.Graph.CreateNode(SchemaNodeType.Anchor, anchorPos, out _, out var anchorOutputs, allocator);

                coaster.Vectors[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Position)] = anchor.HeartPosition;

                var frame = new KexEdit.Sim.Frame(
                    math.normalizesafe(anchor.Direction, math.back()),
                    math.normalizesafe(anchor.Normal, math.down()),
                    math.normalizesafe(anchor.Lateral, math.right())
                );
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Roll)] = frame.Roll;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Pitch)] = frame.Pitch;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Yaw)] = frame.Yaw;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Velocity)] = anchor.Velocity;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Heart)] = anchor.HeartOffset;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Friction)] = anchor.Friction;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Resistance)] = anchor.Resistance;

                if (coaster.Graph.TryGetInput(nodeId, BridgePorts.Target, out uint bridgeTargetPortId)) {
                    coaster.Graph.AddEdge(anchorOutputs[0], bridgeTargetPortId);
                }

                anchorOutputs.Dispose();
            }
        }

        [BurstCompile]
        private static void ImportInferredAnchorsFromNodes(
            in NativeArray<LegacyNode> nodes,
            ref DocumentAggregate coaster,
            Allocator allocator,
            in NativeHashMap<int, uint> nodeIdRemap
        ) {
            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                var nodeType = node.Node.Type;

                bool isSectionNode = nodeType == NodeType.GeometricSection ||
                                     nodeType == NodeType.ForceSection ||
                                     nodeType == NodeType.CurvedSection ||
                                     nodeType == NodeType.CopyPathSection;
                if (!isSectionNode) continue;

                uint nodeId = nodeIdRemap.TryGetValue(i, out uint remappedId) ? remappedId : node.Node.Id;

                bool hasAnchorConnection = false;
                if (coaster.Graph.TryGetInput(nodeId, 0, out uint anchorPortId)) {
                    for (int j = 0; j < coaster.Graph.EdgeIds.Length; j++) {
                        if (coaster.Graph.EdgeTargets[j] == anchorPortId) {
                            hasAnchorConnection = true;
                            break;
                        }
                    }
                }

                if (hasAnchorConnection) continue;

                var anchor = node.Anchor;
                bool hasPosition = !math.all(anchor.HeartPosition == float3.zero);
                bool hasDirection = !math.all(anchor.Direction == float3.zero);
                if (!hasPosition && !hasDirection) continue;

                float2 anchorPos = node.Node.Position + new float2(-100f, -50f);
                uint anchorNodeId = coaster.Graph.CreateNode(SchemaNodeType.Anchor, anchorPos, out _, out var anchorOutputs, allocator);

                if (hasPosition) {
                    coaster.Vectors[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Position)] = anchor.HeartPosition;
                }

                if (hasDirection) {
                    var frame = new KexEdit.Sim.Frame(
                        math.normalizesafe(anchor.Direction, math.back()),
                        math.normalizesafe(anchor.Normal, math.down()),
                        math.normalizesafe(anchor.Lateral, math.right())
                    );
                    coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Roll)] = frame.Roll;
                    coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Pitch)] = frame.Pitch;
                    coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Yaw)] = frame.Yaw;
                }

                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Velocity)] = anchor.Velocity;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Heart)] = anchor.HeartOffset;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Friction)] = anchor.Friction;
                coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Resistance)] = anchor.Resistance;

                if (coaster.Graph.TryGetInput(nodeId, 0, out uint sectionAnchorPortId)) {
                    coaster.Graph.AddEdge(anchorOutputs[0], sectionAnchorPortId);
                }

                anchorOutputs.Dispose();
            }
        }

        [BurstCompile]
        private static uint ConvertNodeType(NodeType legacyType) {
            return legacyType switch {
                NodeType.ForceSection => (uint)SchemaNodeType.Force,
                NodeType.GeometricSection => (uint)SchemaNodeType.Geometric,
                NodeType.CurvedSection => (uint)SchemaNodeType.Curved,
                NodeType.CopyPathSection => (uint)SchemaNodeType.CopyPath,
                NodeType.Anchor => (uint)SchemaNodeType.Anchor,
                NodeType.Reverse => (uint)SchemaNodeType.Reverse,
                NodeType.ReversePath => (uint)SchemaNodeType.ReversePath,
                NodeType.Bridge => (uint)SchemaNodeType.Bridge,
                _ => 0
            };
        }
    }
}
