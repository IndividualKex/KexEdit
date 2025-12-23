using System;
using KexEdit.Sim;
using KexEdit.Graph;
using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Anchor;
using KexEdit.Sim.Nodes.Bridge;
using KexEdit.Sim.Nodes.CopyPath;
using KexEdit.Sim.Nodes.Curved;
using KexEdit.Sim.Nodes.Force;
using KexEdit.Sim.Nodes.Geometric;
using KexEdit.Sim.Nodes.Reverse;
using KexEdit.Sim.Nodes.ReversePath;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.App.Coaster {
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
        private const float DEFAULT_FRICTION = 0.021f;
        private const float DEFAULT_RESISTANCE = 2e-5f;

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
        private static void TopologicalSort(in KexEdit.Graph.Graph graph, out NativeList<uint> sorted, Allocator allocator) {
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
        private static bool TryGetAnchor(
            in KexEdit.Graph.Graph graph, in EvaluationResult result,
            uint nodeId, int index, out Point anchor
        ) {
            anchor = default;
            if (!graph.TryGetInput(nodeId, index, out uint portId)) return false;
            return TryGetAnchorFromPort(in graph, in result, portId, out anchor);
        }

        [BurstCompile]
        private static bool TryGetAnchorFromPort(
            in KexEdit.Graph.Graph graph, in EvaluationResult result,
            uint portId, out Point anchor
        ) {
            anchor = default;
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
            ulong posKey = Coaster.InputKey(nodeId, AnchorPorts.Position);
            float3 position = coaster.Vectors.TryGetValue(posKey, out var pos) ? pos : float3.zero;
            float roll = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Roll, 0f);
            float pitch = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Pitch, 0f);
            float yaw = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Yaw, 0f);
            float velocity = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Velocity, DEFAULT_VELOCITY);
            float heart = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Heart, DEFAULT_HEART_OFFSET);
            float friction = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Friction, DEFAULT_FRICTION);
            float resistance = GetScalar(in coaster.Scalars, nodeId, AnchorPorts.Resistance, DEFAULT_RESISTANCE);

            Frame frame = Frame.FromEuler(pitch, yaw, roll);
            float centerY = frame.SpinePosition(position, heart * 0.9f).y;
            float energy = 0.5f * velocity * velocity + KexEdit.Sim.Sim.G * centerY;

            AnchorNode.Build(
                in position, pitch, yaw, roll,
                velocity, energy,
                heart, friction, resistance,
                out Point anchor
            );

            result.OutputAnchors[nodeId] = anchor;
        }

        [BurstCompile]
        private static void EvaluateForceNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, ForcePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = GetScalar(in coaster.Scalars, nodeId, NodeMeta.Duration, 1f);
            var durationType = GetFlag(in coaster.Flags, nodeId, NodeMeta.DurationType) == 1
                ? DurationType.Distance : DurationType.Time;

            var config = new IterationConfig(duration, (KexEdit.Sim.Schema.DurationType)durationType);
            bool driven = GetFlag(in coaster.Flags, nodeId, NodeMeta.Driven) == 1;

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
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, GeometricPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = GetScalar(in coaster.Scalars, nodeId, NodeMeta.Duration, 1f);
            var durationType = GetFlag(in coaster.Flags, nodeId, NodeMeta.DurationType) == 1
                ? DurationType.Distance : DurationType.Time;

            var config = new IterationConfig(duration, (KexEdit.Sim.Schema.DurationType)durationType);
            bool driven = GetFlag(in coaster.Flags, nodeId, NodeMeta.Driven) == 1;
            bool steering = GetFlag(in coaster.Flags, nodeId, NodeMeta.Steering) == 1;

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
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, CurvedPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float radius = GetScalar(in coaster.Scalars, nodeId, CurvedPorts.Radius, 10f);
            float arc = GetScalar(in coaster.Scalars, nodeId, CurvedPorts.Arc, 90f);
            float axis = GetScalar(in coaster.Scalars, nodeId, CurvedPorts.Axis, 0f);
            float leadIn = GetScalar(in coaster.Scalars, nodeId, CurvedPorts.LeadIn, 0f);
            float leadOut = GetScalar(in coaster.Scalars, nodeId, CurvedPorts.LeadOut, 0f);

            bool driven = GetFlag(in coaster.Flags, nodeId, NodeMeta.Driven) == 1;

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
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, BridgePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, BridgePorts.Target, out Point targetAnchor)) {
                return;
            }

            float inWeight = GetScalar(in coaster.Scalars, nodeId, BridgePorts.InWeight, 0.5f);
            float outWeight = GetScalar(in coaster.Scalars, nodeId, BridgePorts.OutWeight, 0.5f);
            bool driven = GetFlag(in coaster.Flags, nodeId, NodeMeta.Driven) == 1;

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
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, CopyPathPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            if (!TryGetPath(in coaster.Graph, in result, nodeId, CopyPathPorts.Path, out var sourcePath)) {
                return;
            }

            float start = GetScalar(in coaster.Scalars, nodeId, CopyPathPorts.Start, -1f);
            float end = GetScalar(in coaster.Scalars, nodeId, CopyPathPorts.End, -1f);
            bool driven = GetFlag(in coaster.Flags, nodeId, NodeMeta.Driven) == 1;

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
            if (!TryGetAnchor(in coaster.Graph, in result, nodeId, ReversePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            ReverseNode.Build(in inputAnchor, out Point reversed);
            result.OutputAnchors[nodeId] = reversed;
        }

        [BurstCompile]
        private static void EvaluateReversePathNode(in Coaster coaster, uint nodeId, ref EvaluationResult result, Allocator allocator) {
            if (!TryGetPath(in coaster.Graph, in result, nodeId, ReversePathPorts.Path, out var sourcePath)) {
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
        private static bool TryGetPath(
            in KexEdit.Graph.Graph graph, in EvaluationResult result,
            uint nodeId, int index, out NativeList<Point> path
        ) {
            path = default;
            if (!graph.TryGetInput(nodeId, index, out uint portId)) return false;
            return GetPathFromPort(in graph, in result, portId, out path);
        }

        [BurstCompile]
        private static bool GetPathFromPort(
            in KexEdit.Graph.Graph graph, in EvaluationResult result,
            uint portId, out NativeList<Point> path
        ) {
            path = default;
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
        private static float GetScalar(
            in NativeHashMap<ulong, float> scalars,
            uint nodeId, int inputIndex, float defaultValue
        ) {
            ulong key = Coaster.InputKey(nodeId, inputIndex);
            return scalars.TryGetValue(key, out float value) ? value : defaultValue;
        }

        [BurstCompile]
        private static int GetFlag(
            in NativeHashMap<ulong, int> flags,
            uint nodeId, int propertyIndex
        ) {
            ulong key = Coaster.InputKey(nodeId, propertyIndex);
            return flags.TryGetValue(key, out int value) ? value : 0;
        }

        [BurstCompile]
        private static void GetKeyframes(
            in KexEdit.Sim.Schema.Storage.KeyframeStore store,
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
