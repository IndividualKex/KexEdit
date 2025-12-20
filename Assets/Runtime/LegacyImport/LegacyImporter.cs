using KexEdit.Coaster;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Point = KexEdit.Core.Point;
using Keyframe = KexEdit.Core.Keyframe;
using InterpolationType = KexEdit.Core.InterpolationType;
using Duration = KexEdit.Coaster.Duration;
using DurationType = KexEdit.Coaster.DurationType;

namespace KexEdit.LegacyImport {
    [BurstCompile]
    public static class LegacyImporter {
        [BurstCompile]
        public static void Import(in SerializedGraph serializedGraph, Allocator allocator, out Coaster.Coaster coaster) {
            coaster = Coaster.Coaster.Create(allocator);

            ImportGraph(in serializedGraph, ref coaster.Graph, allocator);
            ImportNodeData(in serializedGraph, ref coaster);
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
        private static void ImportGraph(in SerializedGraph serializedGraph, ref Graph graph, Allocator allocator) {
            uint maxNodeId = 0;
            uint maxPortId = 0;
            uint maxEdgeId = 0;

            var seenNodes = new NativeHashSet<uint>(serializedGraph.Nodes.Length, Allocator.Temp);

            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                if (!seenNodes.Add(node.Node.Id)) continue;

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

            graph.NextNodeId = maxNodeId + 1;
            uint nextPortId = maxPortId + 1;
            graph.NextEdgeId = maxEdgeId + 1;

            seenNodes.Clear();
            var portIdRemap = new NativeHashMap<uint, uint>(serializedGraph.Nodes.Length * 4, Allocator.Temp);

            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                uint nodeId = node.Node.Id;

                if (!seenNodes.Add(nodeId)) continue;

                uint nodeType = ConvertNodeType(node.Node.Type);
                float2 position = node.Node.Position;

                int nodeIndex = graph.NodeIds.Length;
                graph.NodeIds.Add(nodeId);
                graph.NodeTypes.Add(nodeType);
                graph.NodePositions.Add(position);
                graph.NodeInputCount.Add(node.InputPorts.Length);
                graph.NodeOutputCount.Add(node.OutputPorts.Length);
                graph.NodeIndexMap[nodeId] = nodeIndex;

                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    uint portId = port.Port.Id;

                    if (portIdRemap.ContainsKey(portId)) {
                        portId = nextPortId++;
                    }
                    portIdRemap[port.Port.Id] = portId;

                    uint portType = (uint)port.Port.Type;

                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(portId);
                    graph.PortTypes.Add(portType);
                    graph.PortOwners.Add(nodeId);
                    graph.PortIsInput.Add(true);
                    graph.PortIndexMap[portId] = portIndex;
                }

                for (int j = 0; j < node.OutputPorts.Length; j++) {
                    var port = node.OutputPorts[j];
                    uint portId = port.Port.Id;

                    if (portIdRemap.ContainsKey(portId)) {
                        portId = nextPortId++;
                    }
                    portIdRemap[port.Port.Id] = portId;

                    uint portType = (uint)port.Port.Type;

                    int portIndex = graph.PortIds.Length;
                    graph.PortIds.Add(portId);
                    graph.PortTypes.Add(portType);
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

                int edgeIndex = graph.EdgeIds.Length;
                graph.EdgeIds.Add(edge.Id);
                graph.EdgeSources.Add(sourceId);
                graph.EdgeTargets.Add(targetId);
                graph.EdgeIndexMap[edge.Id] = edgeIndex;
            }

            portIdRemap.Dispose();
            seenEdges.Dispose();

            graph.NextPortId = nextPortId;
        }

        [BurstCompile]
        private static void ImportNodeData(in SerializedGraph serializedGraph, ref Coaster.Coaster coaster) {
            var seenNodes = new NativeHashSet<uint>(serializedGraph.Nodes.Length, Allocator.Temp);

            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                uint nodeId = node.Node.Id;

                if (!seenNodes.Add(nodeId)) continue;

                ImportAnchor(in node, nodeId, ref coaster);
                ImportKeyframes(in node, nodeId, ref coaster.Keyframes);
                ImportDuration(in node, nodeId, ref coaster);
                ImportSteering(in node, nodeId, ref coaster);
                ImportDriven(in node, nodeId, ref coaster);
                ImportPortValues(in node, nodeId, ref coaster);
            }

            seenNodes.Dispose();
        }

        [BurstCompile]
        private static void ImportAnchor(in SerializedNode node, uint nodeId, ref Coaster.Coaster coaster) {
            var legacyAnchor = node.Anchor;
            ConvertPointDataToPoint(in legacyAnchor, out var anchor);
            coaster.Anchors[nodeId] = anchor;
        }

        [BurstCompile]
        private static void ConvertPointDataToPoint(in PointData pointData, out Point point) {
            point = new Point(
                spinePosition: pointData.Position,
                direction: pointData.Direction,
                normal: pointData.Normal,
                lateral: pointData.Lateral,
                velocity: pointData.Velocity,
                energy: pointData.Energy,
                normalForce: pointData.NormalForce,
                lateralForce: pointData.LateralForce,
                heartArc: pointData.TotalHeartLength,
                spineArc: pointData.TotalLength,
                spineAdvance: pointData.DistanceFromLast,
                frictionOrigin: pointData.FrictionCompensation,
                rollSpeed: pointData.RollSpeed,
                heartOffset: pointData.Heart,
                friction: pointData.Friction,
                resistance: pointData.Resistance
            );
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

            var convertedKeyframes = new NativeArray<Keyframe>(legacyKeyframes.Length, Allocator.Temp);
            unsafe {
                var legacyPtr = (Legacy.Keyframe*)legacyKeyframes.GetUnsafePtr();
                for (int i = 0; i < legacyKeyframes.Length; i++) {
                    var legacy = legacyPtr[i];
                    convertedKeyframes[i] = new Keyframe(
                        time: legacy.Time,
                        value: legacy.Value,
                        inInterpolation: (InterpolationType)legacy.InInterpolation,
                        outInterpolation: (InterpolationType)legacy.OutInterpolation,
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
        private static void ImportDuration(in SerializedNode node, uint nodeId, ref Coaster.Coaster coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasDuration) != 0) {
                var legacyDuration = node.Duration;
                var duration = new Duration(legacyDuration.Value, (DurationType)legacyDuration.Type);
                coaster.Durations[nodeId] = duration;
            }
        }

        [BurstCompile]
        private static void ImportSteering(in SerializedNode node, uint nodeId, ref Coaster.Coaster coaster) {
            if (node.Steering) {
                coaster.Steering.Add(nodeId);
            }
        }

        [BurstCompile]
        private static void ImportDriven(in SerializedNode node, uint nodeId, ref Coaster.Coaster coaster) {
            if ((node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0 && node.PropertyOverrides.FixedVelocity) {
                coaster.Driven.Add(nodeId);
            }
        }

        [BurstCompile]
        private static void ImportPortValues(in SerializedNode node, uint nodeId, ref Coaster.Coaster coaster) {
            for (int i = 0; i < node.InputPorts.Length; i++) {
                var port = node.InputPorts[i];
                var portType = port.Port.Type;
                var portId = port.Port.Id;
                var value = port.Value;

                switch (portType) {
                    case PortType.Position:
                        coaster.Vectors[nodeId] = new float3(value.Roll, value.Velocity, value.Energy);
                        break;
                    case PortType.Rotation:
                        coaster.SetRotation(nodeId, new float3(value.Roll, value.Velocity, value.Energy));
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
                    case PortType.Roll:
                    case PortType.Pitch:
                    case PortType.Yaw:
                    case PortType.Velocity:
                    case PortType.Heart:
                    case PortType.Friction:
                    case PortType.Resistance:
                        coaster.Scalars[portId] = value.Roll;
                        break;
                }
            }
        }
    }
}
