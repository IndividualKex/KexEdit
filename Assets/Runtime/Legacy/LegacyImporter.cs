using KexEdit.Coaster;
using KexEdit.Legacy.Serialization;
using KexEdit.NodeGraph;
using KexEdit.Nodes.Anchor;
using KexEdit.Nodes.Bridge;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using CoreKeyframe = KexEdit.Core.Keyframe;
using CoreInterpolationType = KexEdit.Core.InterpolationType;
using CoasterAggregate = KexEdit.Coaster.Coaster;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class LegacyImporter {
        [BurstCompile]
        public static void Import(in SerializedGraph serializedGraph, Allocator allocator, out CoasterAggregate coaster) {
            coaster = CoasterAggregate.Create(allocator);

            var nodeIdRemap = new NativeHashMap<int, uint>(16, Allocator.Temp);
            var portIdRemap = new NativeHashMap<uint, uint>(64, Allocator.Temp);
            ImportGraph(in serializedGraph, ref coaster.Graph, allocator, ref nodeIdRemap, ref portIdRemap);
            ImportNodeData(in serializedGraph, ref coaster, in nodeIdRemap);
            ImportBridgeTargets(in serializedGraph, ref coaster, allocator, in nodeIdRemap);
            nodeIdRemap.Dispose();
            portIdRemap.Dispose();
        }

        [BurstCompile]
        private static uint ConvertNodeType(Legacy.NodeType legacyType) {
            return legacyType switch {
                Legacy.NodeType.ForceSection => (uint)Nodes.NodeType.Force,
                Legacy.NodeType.GeometricSection => (uint)Nodes.NodeType.Geometric,
                Legacy.NodeType.CurvedSection => (uint)Nodes.NodeType.Curved,
                Legacy.NodeType.CopyPathSection => (uint)Nodes.NodeType.CopyPath,
                Legacy.NodeType.Anchor => (uint)Nodes.NodeType.Anchor,
                Legacy.NodeType.Reverse => (uint)Nodes.NodeType.Reverse,
                Legacy.NodeType.ReversePath => (uint)Nodes.NodeType.ReversePath,
                Legacy.NodeType.Bridge => (uint)Nodes.NodeType.Bridge,
                _ => 0
            };
        }


        [BurstCompile]
        private static void ImportGraph(in SerializedGraph serializedGraph, ref Graph graph, Allocator allocator, ref NativeHashMap<int, uint> nodeIdRemap, ref NativeHashMap<uint, uint> portIdRemap) {
            uint maxNodeId = 0;
            uint maxPortId = 0;
            uint maxEdgeId = 0;

            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                maxNodeId = math.max(maxNodeId, node.Node.Id);

                for (int j = 0; j < node.InputPorts.Length; j++) {
                    maxPortId = math.max(maxPortId, node.InputPorts[j].Port.Id);
                }
                for (int j = 0; j < node.OutputPorts.Length; j++) {
                    maxPortId = math.max(maxPortId, node.OutputPorts[j].Port.Id);
                }
            }

            for (int i = 0; i < serializedGraph.Edges.Length; i++) {
                maxEdgeId = math.max(maxEdgeId, serializedGraph.Edges[i].Id);
            }

            uint nextNodeId = maxNodeId + 1;
            uint nextPortId = maxPortId + 1;
            graph.NextEdgeId = maxEdgeId + 1;

            var seenNodes = new NativeHashSet<uint>(serializedGraph.Nodes.Length, Allocator.Temp);

            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                uint nodeId = node.Node.Id;

                // Remap duplicate node IDs to new unique IDs
                if (!seenNodes.Add(nodeId)) {
                    nodeId = nextNodeId++;
                    nodeIdRemap[i] = nodeId;
                }

                uint nodeType = ConvertNodeType(node.Node.Type);
                float2 position = node.Node.Position;
                bool isBridge = node.Node.Type == Legacy.NodeType.Bridge;
                bool isAnchor = node.Node.Type == Legacy.NodeType.Anchor;

                int bridgeAnchorPortCount = 0;
                if (isBridge) {
                    for (int j = 0; j < node.InputPorts.Length; j++) {
                        if (node.InputPorts[j].Port.Type == Legacy.PortType.Anchor) {
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
                var coreNodeType = (Nodes.NodeType)nodeType;

                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    uint portId = port.Port.Id;

                    if (portIdRemap.ContainsKey(portId)) {
                        portId = nextPortId++;
                    }
                    portIdRemap[port.Port.Id] = portId;

                    Nodes.NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);

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
                    Nodes.NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);
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
                    Nodes.NodeSchema.InputSpec(coreNodeType, inputIndex, out var portSpec);
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

                    Nodes.NodeSchema.OutputSpec(coreNodeType, j, out var portSpec);

                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(portId);
                    graph.PortTypes.Add(portSpec.ToEncoded());
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(false);
                    graph.PortIndexMap[portId] = portIndex;
                }
            }

            seenNodes.Dispose();

            var seenEdges = new NativeHashSet<uint>(serializedGraph.Edges.Length, Allocator.Temp);

            for (int i = 0; i < serializedGraph.Edges.Length; i++) {
                var edge = serializedGraph.Edges[i];
                if (!seenEdges.Add(edge.Id)) continue;

                uint sourceId = edge.SourceId;
                uint targetId = edge.TargetId;

                if (portIdRemap.TryGetValue(sourceId, out uint remappedSource)) {
                    sourceId = remappedSource;
                }
                if (portIdRemap.TryGetValue(targetId, out uint remappedTarget)) {
                    targetId = remappedTarget;
                }

                // Skip edges that reference non-existent ports (from unimported node types)
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
        private static void ImportNodeData(in SerializedGraph serializedGraph, ref CoasterAggregate coaster, in NativeHashMap<int, uint> nodeIdRemap) {
            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                uint nodeId = nodeIdRemap.TryGetValue(i, out uint remappedId) ? remappedId : node.Node.Id;

                ImportKeyframes(in node, nodeId, ref coaster.Keyframes);
                ImportDuration(in node, nodeId, ref coaster);
                ImportSteering(in node, nodeId, ref coaster);
                ImportDriven(in node, nodeId, ref coaster);
                ImportFacing(in node, nodeId, ref coaster);
                ImportPriority(in node, nodeId, ref coaster);
                ImportRender(in node, nodeId, ref coaster);
                ImportPortValues(in node, nodeId, ref coaster);
                ImportAnchorData(in node, nodeId, ref coaster);
            }
        }


        [BurstCompile]
        private static void ImportKeyframes(in SerializedNode node, uint nodeId, ref Nodes.Storage.KeyframeStore keyframes) {
            ImportKeyframeArray(node.RollSpeedKeyframes, nodeId, Nodes.PropertyId.RollSpeed, ref keyframes);
            ImportKeyframeArray(node.NormalForceKeyframes, nodeId, Nodes.PropertyId.NormalForce, ref keyframes);
            ImportKeyframeArray(node.LateralForceKeyframes, nodeId, Nodes.PropertyId.LateralForce, ref keyframes);
            ImportKeyframeArray(node.PitchSpeedKeyframes, nodeId, Nodes.PropertyId.PitchSpeed, ref keyframes);
            ImportKeyframeArray(node.YawSpeedKeyframes, nodeId, Nodes.PropertyId.YawSpeed, ref keyframes);
            ImportKeyframeArray(node.FixedVelocityKeyframes, nodeId, Nodes.PropertyId.DrivenVelocity, ref keyframes);
            ImportKeyframeArray(node.HeartKeyframes, nodeId, Nodes.PropertyId.HeartOffset, ref keyframes);
            ImportKeyframeArray(node.FrictionKeyframes, nodeId, Nodes.PropertyId.Friction, ref keyframes);
            ImportKeyframeArray(node.ResistanceKeyframes, nodeId, Nodes.PropertyId.Resistance, ref keyframes);
            ImportKeyframeArray(node.TrackStyleKeyframes, nodeId, Nodes.PropertyId.TrackStyle, ref keyframes);
        }

        [BurstCompile]
        private static void ImportKeyframeArray<T>(
            in NativeArray<T> legacyKeyframes,
            uint nodeId,
            Nodes.PropertyId propertyId,
            ref Nodes.Storage.KeyframeStore keyframes
        ) where T : unmanaged {
            if (legacyKeyframes.Length == 0) return;

            var convertedKeyframes = new NativeArray<CoreKeyframe>(legacyKeyframes.Length, Allocator.Temp);
            unsafe {
                var legacyPtr = (Keyframe*)legacyKeyframes.GetUnsafePtr();
                for (int i = 0; i < legacyKeyframes.Length; i++) {
                    var legacy = legacyPtr[i];
                    convertedKeyframes[i] = new CoreKeyframe(
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
            }

            keyframes.Set(nodeId, propertyId, in convertedKeyframes);
            convertedKeyframes.Dispose();
        }

        [BurstCompile]
        private static void ImportDuration(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasDuration) != 0) {
                var legacyDuration = node.Duration;
                coaster.Scalars[CoasterAggregate.InputKey(nodeId, NodeMeta.Duration)] = legacyDuration.Value;
                if (legacyDuration.Type == Legacy.DurationType.Distance) {
                    coaster.Flags[CoasterAggregate.InputKey(nodeId, NodeMeta.DurationType)] = 1;
                }
            }
        }

        [BurstCompile]
        private static void ImportSteering(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            if (node.Steering) {
                coaster.Flags[CoasterAggregate.InputKey(nodeId, NodeMeta.Steering)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportDriven(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0 && node.PropertyOverrides.FixedVelocity) {
                coaster.Flags[CoasterAggregate.InputKey(nodeId, NodeMeta.Driven)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportFacing(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            int facing = node.Anchor.Facing;
            if (facing != 1) {
                coaster.Flags[CoasterAggregate.InputKey(nodeId, NodeMeta.Facing)] = facing;
            }
        }

        [BurstCompile]
        private static void ImportPriority(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            int priority = node.Node.Priority;
            if (priority != 0) {
                coaster.Scalars[CoasterAggregate.InputKey(nodeId, NodeMeta.Priority)] = priority;
            }
        }

        [BurstCompile]
        private static void ImportRender(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            bool render = node.Render;
            if (!render) {
                coaster.Flags[CoasterAggregate.InputKey(nodeId, NodeMeta.Render)] = 1;
            }
        }

        [BurstCompile]
        private static void ImportPortValues(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            for (int i = 0; i < node.InputPorts.Length; i++) {
                var port = node.InputPorts[i];
                var portType = port.Port.Type;
                var value = port.Value;
                ulong key = CoasterAggregate.InputKey(nodeId, i);

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
        private static void ImportAnchorData(in SerializedNode node, uint nodeId, ref CoasterAggregate coaster) {
            if (node.Node.Type != Legacy.NodeType.Anchor) return;

            coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Velocity)] = node.Anchor.Velocity;
            coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Heart)] = node.Anchor.HeartOffset;
            coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Friction)] = node.Anchor.Friction;
            coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Resistance)] = node.Anchor.Resistance;

            bool hasPosition = !math.all(node.Anchor.HeartPosition == float3.zero);
            bool hasDirection = !math.all(node.Anchor.Direction == float3.zero);

            if (hasPosition) {
                coaster.Vectors[CoasterAggregate.InputKey(nodeId, AnchorPorts.Position)] = node.Anchor.HeartPosition;
            }

            if (hasDirection) {
                var frame = new Core.Frame(
                    math.normalizesafe(node.Anchor.Direction, math.back()),
                    math.normalizesafe(node.Anchor.Normal, math.down()),
                    math.normalizesafe(node.Anchor.Lateral, math.right())
                );
                coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Roll)] = frame.Roll;
                coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Pitch)] = frame.Pitch;
                coaster.Scalars[CoasterAggregate.InputKey(nodeId, AnchorPorts.Yaw)] = frame.Yaw;
            }
        }

        [BurstCompile]
        private static void ImportBridgeTargets(in SerializedGraph serializedGraph, ref CoasterAggregate coaster, Allocator allocator, in NativeHashMap<int, uint> nodeIdRemap) {
            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                if (node.Node.Type != Legacy.NodeType.Bridge) continue;

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
                uint anchorNodeId = coaster.Graph.CreateNode(Nodes.NodeType.Anchor, anchorPos, out _, out var anchorOutputs, allocator);

                coaster.Vectors[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Position)] = anchor.HeartPosition;

                var frame = new Core.Frame(
                    math.normalizesafe(anchor.Direction, math.back()),
                    math.normalizesafe(anchor.Normal, math.down()),
                    math.normalizesafe(anchor.Lateral, math.right())
                );
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Roll)] = frame.Roll;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Pitch)] = frame.Pitch;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Yaw)] = frame.Yaw;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Velocity)] = anchor.Velocity;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Heart)] = anchor.HeartOffset;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Friction)] = anchor.Friction;
                coaster.Scalars[CoasterAggregate.InputKey(anchorNodeId, AnchorPorts.Resistance)] = anchor.Resistance;

                if (coaster.Graph.TryGetInput(nodeId, BridgePorts.Target, out uint bridgeTargetPortId)) {
                    coaster.Graph.AddEdge(anchorOutputs[0], bridgeTargetPortId);
                }

                anchorOutputs.Dispose();
            }
        }
    }
}
