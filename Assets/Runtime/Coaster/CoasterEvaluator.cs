using System;
using KexEdit.Core;
using KexEdit.NodeGraph;
using KexEdit.Nodes;
using KexEdit.Nodes.Anchor;
using KexEdit.Nodes.Bridge;
using KexEdit.Nodes.CopyPath;
using KexEdit.Nodes.Curved;
using KexEdit.Nodes.Force;
using KexEdit.Nodes.Geometric;
using KexEdit.Nodes.Reverse;
using KexEdit.Nodes.ReversePath;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Coaster {
    [BurstCompile]
    public struct EvaluationResult : IDisposable {
        public NativeHashMap<uint, NativeList<Point>> Paths;
        public NativeHashMap<uint, Point> OutputAnchors;

        [BurstCompile]
        public static void Create(out EvaluationResult result, Allocator allocator, int capacity = 16) {
            result = new EvaluationResult {
                Paths = new NativeHashMap<uint, NativeList<Point>>(capacity, allocator),
                OutputAnchors = new NativeHashMap<uint, Point>(capacity, allocator)
            };
        }

        public void Dispose() {
            if (Paths.IsCreated) {
                var keys = Paths.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++) {
                    if (Paths.TryGetValue(keys[i], out var list) && list.IsCreated) {
                        list.Dispose();
                    }
                }
                keys.Dispose();
                Paths.Dispose();
            }
            if (OutputAnchors.IsCreated) OutputAnchors.Dispose();
        }
    }

    [BurstCompile]
    public static class CoasterEvaluator {
        private const float DEFAULT_VELOCITY = 10f;
        private const float DEFAULT_HEART_OFFSET = 1.1f;
        private const float DEFAULT_FRICTION = 0.015f;
        private const float DEFAULT_RESISTANCE = 0f;

        [BurstCompile]
        public static void Evaluate(in Coaster coaster, out EvaluationResult result, Allocator allocator) {
            int nodeCount = coaster.Graph.NodeCount;
            EvaluationResult.Create(out result, allocator, math.max(nodeCount, 16));

            if (nodeCount == 0) return;

            TopologicalSort(in coaster.Graph, out var sortedNodes, Allocator.Temp);

            for (int i = 0; i < sortedNodes.Length; i++) {
                uint nodeId = sortedNodes[i];
                if (!coaster.Graph.TryGetNodeType(nodeId, out NodeType nodeType)) continue;

                switch (nodeType) {
                    case NodeType.Anchor:
                        EvaluateAnchorNode(in coaster, nodeId, ref result);
                        break;
                    case NodeType.Force:
                        EvaluateForceNode(in coaster, nodeId, ref result, allocator);
                        break;
                    case NodeType.Geometric:
                        EvaluateGeometricNode(in coaster, nodeId, ref result, allocator);
                        break;
                    case NodeType.Curved:
                        EvaluateCurvedNode(in coaster, nodeId, ref result, allocator);
                        break;
                    case NodeType.Bridge:
                        EvaluateBridgeNode(in coaster, nodeId, ref result, allocator);
                        break;
                    case NodeType.CopyPath:
                        EvaluateCopyPathNode(in coaster, nodeId, ref result, allocator);
                        break;
                    case NodeType.Reverse:
                        EvaluateReverseNode(in coaster, nodeId, ref result);
                        break;
                    case NodeType.ReversePath:
                        EvaluateReversePathNode(in coaster, nodeId, ref result, allocator);
                        break;
                }
            }

            sortedNodes.Dispose();
        }

        [BurstCompile]
        private static void TopologicalSort(in Graph graph, out NativeList<uint> sorted, Allocator allocator) {
            sorted = new NativeList<uint>(graph.NodeCount, allocator);

            graph.FindSourceNodes(out var sources, Allocator.Temp);
            var queue = new NativeList<uint>(math.max(sources.Length, 4), Allocator.Temp);
            for (int i = 0; i < sources.Length; i++) {
                queue.Add(sources[i]);
            }
            sources.Dispose();

            var visited = new NativeHashSet<uint>(math.max(graph.NodeCount, 4), Allocator.Temp);

            while (queue.Length > 0) {
                uint nodeId = queue[0];
                queue.RemoveAt(0);

                if (!visited.Add(nodeId)) continue;
                sorted.Add(nodeId);

                graph.GetSuccessorNodes(nodeId, out var successors, Allocator.Temp);
                for (int i = 0; i < successors.Length; i++) {
                    uint succ = successors[i];
                    if (visited.Contains(succ)) continue;

                    bool allPredVisited = true;
                    graph.GetPredecessorNodes(succ, out var preds, Allocator.Temp);
                    for (int j = 0; j < preds.Length; j++) {
                        if (!visited.Contains(preds[j])) {
                            allPredVisited = false;
                            break;
                        }
                    }
                    preds.Dispose();

                    if (allPredVisited) {
                        queue.Add(succ);
                    }
                }
                successors.Dispose();
            }

            visited.Dispose();
            queue.Dispose();
        }

        [BurstCompile]
        private static bool TryGetInputAnchor(
            in Graph graph, in EvaluationResult result,
            uint nodeId, PortId inputPort, out Point anchor
        ) {
            anchor = default;
            if (!graph.TryGetInputPort(nodeId, inputPort, out uint portId)) return false;

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeTargets[i] != portId) continue;

                uint sourcePortId = graph.EdgeSources[i];
                if (!graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = graph.PortOwners[portIndex];
                if (result.OutputAnchors.TryGetValue(sourceNodeId, out anchor)) {
                    return true;
                }
            }
            return false;
        }

        [BurstCompile]
        private static void EvaluateAnchorNode(in Coaster coaster, uint nodeId, ref EvaluationResult result) {
            if (coaster.Anchors.TryGetValue(nodeId, out var storedAnchor)) {
                result.OutputAnchors[nodeId] = storedAnchor;
                return;
            }

            float3 position = coaster.Vectors.TryGetValue(nodeId, out var pos) ? pos : float3.zero;
            float3 rotation = coaster.GetRotation(nodeId);

            float velocity = DEFAULT_VELOCITY;
            float energy = 0.5f * velocity * velocity + Sim.G * position.y;

            AnchorNode.Build(
                in position, rotation.x, rotation.y, rotation.z,
                velocity, energy,
                DEFAULT_HEART_OFFSET, DEFAULT_FRICTION, DEFAULT_RESISTANCE,
                out Point anchor
            );

            result.OutputAnchors[nodeId] = anchor;
        }

        [BurstCompile]
        private static void EvaluateForceNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = 1f;
            var durationType = DurationType.Time;
            if (coaster.Durations.TryGetValue(nodeId, out var dur)) {
                duration = dur.Value;
                durationType = dur.Type;
            }

            var config = new IterationConfig(duration, (KexEdit.Nodes.DurationType)durationType);
            bool driven = coaster.Driven.Contains(nodeId);

            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.NormalForce, out var normalForce);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.LateralForce, out var lateralForce);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            ForceNode.Build(
                in inputAnchor, in config, driven,
                in rollSpeed, in normalForce, in lateralForce,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateGeometricNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = 1f;
            var durationType = DurationType.Time;
            if (coaster.Durations.TryGetValue(nodeId, out var dur)) {
                duration = dur.Value;
                durationType = dur.Type;
            }

            var config = new IterationConfig(duration, (KexEdit.Nodes.DurationType)durationType);
            bool driven = coaster.Driven.Contains(nodeId);
            bool steering = coaster.Steering.Contains(nodeId);

            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.PitchSpeed, out var pitchSpeed);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.YawSpeed, out var yawSpeed);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            GeometricNode.Build(
                in inputAnchor, in config, driven, steering,
                in rollSpeed, in pitchSpeed, in yawSpeed,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateCurvedNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            float radius = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.Radius, 10f);
            float arc = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.Arc, 90f);
            float axis = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.Axis, 0f);
            float leadIn = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.LeadIn, 0f);
            float leadOut = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.LeadOut, 0f);

            bool driven = coaster.Driven.Contains(nodeId);

            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            CurvedNode.Build(
                in inputAnchor, radius, arc, axis, leadIn, leadOut, driven,
                in rollSpeed, in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateBridgeNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            Point targetAnchor;
            if (coaster.Anchors.TryGetValue(nodeId, out var stored)) {
                targetAnchor = stored;
            }
            else {
                return;
            }

            float inWeight = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.InWeight, 0.5f);
            float outWeight = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.OutWeight, 0.5f);
            bool driven = coaster.Driven.Contains(nodeId);

            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            BridgeNode.Build(
                in inputAnchor, in targetAnchor, inWeight, outWeight, driven,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateCopyPathNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            if (!TryGetInputPath(in coaster.Graph, in result, nodeId, PortId.Path, out var sourcePath)) {
                return;
            }

            float start = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.Start, -1f);
            float end = TryGetInputScalar(in coaster.Graph, in coaster.Scalars, nodeId, PortId.End, -1f);
            bool driven = coaster.Driven.Contains(nodeId);

            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in coaster.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            CopyPathNode.Build(
                in inputAnchor, sourcePath.AsArray(), start, end, driven,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateReverseNode(in Coaster coaster, uint nodeId, ref EvaluationResult result) {
            if (!TryGetInputAnchor(in coaster.Graph, in result, nodeId, PortId.Anchor, out Point inputAnchor)) {
                return;
            }

            ReverseNode.Build(in inputAnchor, out Point reversed);
            result.OutputAnchors[nodeId] = reversed;
        }

        [BurstCompile]
        private static void EvaluateReversePathNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetInputPath(in coaster.Graph, in result, nodeId, PortId.Path, out var sourcePath)) {
                return;
            }

            var path = new NativeList<Point>(sourcePath.Length, allocator);
            ReversePathNode.Build(sourcePath.AsArray(), ref path);

            if (path.Length > 0) {
                result.OutputAnchors[nodeId] = path[^1];
            }
            result.Paths[nodeId] = path;
        }

        [BurstCompile]
        private static bool TryGetInputPath(
            in Graph graph, in EvaluationResult result,
            uint nodeId, PortId inputPort, out NativeList<Point> path
        ) {
            path = default;
            if (!graph.TryGetInputPort(nodeId, inputPort, out uint portId)) return false;

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeTargets[i] != portId) continue;

                uint sourcePortId = graph.EdgeSources[i];
                if (!graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = graph.PortOwners[portIndex];
                if (result.Paths.TryGetValue(sourceNodeId, out path)) {
                    return path.IsCreated && path.Length > 0;
                }
            }
            return false;
        }

        [BurstCompile]
        private static float TryGetInputScalar(
            in Graph graph, in NativeHashMap<uint, float> scalars,
            uint nodeId, PortId inputPort, float defaultValue
        ) {
            if (!graph.TryGetInputPort(nodeId, inputPort, out uint portId)) return defaultValue;

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeTargets[i] != portId) continue;

                uint sourcePortId = graph.EdgeSources[i];
                if (scalars.TryGetValue(sourcePortId, out float value)) {
                    return value;
                }
            }

            if (scalars.TryGetValue(portId, out float inlineValue)) {
                return inlineValue;
            }

            return defaultValue;
        }

        [BurstCompile]
        private static void GetKeyframes(
            in KexEdit.Nodes.Storage.KeyframeStore store,
            uint nodeId, PropertyId propertyId,
            out NativeArray<Keyframe> keyframes
        ) {
            if (store.TryGet(nodeId, propertyId, out var slice)) {
                keyframes = new NativeArray<Keyframe>(slice.Length, Allocator.Temp);
                for (int i = 0; i < slice.Length; i++) {
                    keyframes[i] = slice[i];
                }
            }
            else {
                keyframes = new NativeArray<Keyframe>(0, Allocator.Temp);
            }
        }
    }
}
