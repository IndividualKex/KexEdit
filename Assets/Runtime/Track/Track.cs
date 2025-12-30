using System;
using KexEdit.Document;
using KexEdit.Graph;
using KexEdit.Graph.Typed;
using KexEdit.Sim;
using KexEdit.Sim.Nodes.Anchor;
using KexEdit.Sim.Nodes.Bridge;
using KexEdit.Sim.Nodes.CopyPath;
using KexEdit.Sim.Nodes.Curved;
using KexEdit.Sim.Nodes.Force;
using KexEdit.Sim.Nodes.Geometric;
using KexEdit.Sim.Nodes.Reverse;
using KexEdit.Sim.Nodes.ReversePath;
using KexEdit.Sim.Schema;
using KexEdit.Spline;
using KexEdit.Spline.Resampling;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Track {
    public static class NodeFlag {
        public const byte Reversed = 0x01;
    }

    public struct SectionLink {
        public int Index;
        public byte Flags;

        private const byte FLAG_AT_START = 0x01;
        private const byte FLAG_FLIP = 0x02;

        public static readonly SectionLink None = new SectionLink { Index = -1, Flags = 0 };
        public bool IsValid => Index >= 0;
        public bool AtStart => (Flags & FLAG_AT_START) != 0;
        public bool Flip => (Flags & FLAG_FLIP) != 0;

        public static SectionLink Create(int index, bool atStart, bool flip) {
            byte flags = 0;
            if (atStart) flags |= FLAG_AT_START;
            if (flip) flags |= FLAG_FLIP;
            return new SectionLink { Index = index, Flags = flags };
        }
    }

    public struct Section {
        public int StartIndex;
        public int EndIndex;
        public float ArcStart;
        public float ArcEnd;
        public byte Flags;
        public SectionLink Next;
        public SectionLink Prev;

        public int Length => EndIndex - StartIndex + 1;
        public bool IsValid => StartIndex >= 0;
        public int Facing => (Flags & NodeFlag.Reversed) != 0 ? -1 : 1;
    }

    [BurstCompile]
    public struct Track : IDisposable {
        public NativeList<Point> Points;
        public NativeArray<Section> Sections;
        public NativeHashMap<uint, int> NodeToSection;
        public NativeArray<int> TraversalOrder;

        public bool IsCreated => Points.IsCreated;
        public int SectionCount => Sections.IsCreated ? Sections.Length : 0;
        public int TraversalCount => TraversalOrder.IsCreated ? TraversalOrder.Length : 0;

        private const float DEFAULT_VELOCITY = 10f;
        private const float DEFAULT_HEART_OFFSET = 1.1f;
        private const float DEFAULT_FRICTION = 0.021f;
        private const float DEFAULT_RESISTANCE = 2e-5f;

        [BurstCompile]
        public static void Build(in Document.Document doc, Allocator allocator, out Track track) {
            int nodeCount = doc.Graph.NodeCount;

            if (nodeCount == 0) {
                track = new Track {
                    Points = new NativeList<Point>(0, allocator),
                    Sections = new NativeArray<Section>(0, allocator)
                };
                return;
            }

            var paths = new NativeHashMap<uint, NativeList<Point>>(nodeCount, Allocator.Temp);
            var outputAnchors = new NativeHashMap<uint, Point>(nodeCount, Allocator.Temp);
            var nodeFlags = new NativeHashMap<uint, byte>(nodeCount, Allocator.Temp);

            TopologicalSort(in doc.Graph, out var sortedNodes, Allocator.Temp);

            for (int i = 0; i < sortedNodes.Length; i++) {
                uint nodeId = sortedNodes[i];
                if (!doc.Graph.TryGetNodeType(nodeId, out NodeType nodeType)) continue;

                switch (nodeType) {
                    case NodeType.Anchor:
                        EvaluateAnchorNode(in doc, nodeId, ref outputAnchors);
                        break;
                    case NodeType.Force:
                        EvaluateForceNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                    case NodeType.Geometric:
                        EvaluateGeometricNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                    case NodeType.Curved:
                        EvaluateCurvedNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                    case NodeType.Bridge:
                        EvaluateBridgeNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                    case NodeType.CopyPath:
                        EvaluateCopyPathNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                    case NodeType.Reverse:
                        EvaluateReverseNode(in doc, nodeId, ref outputAnchors);
                        break;
                    case NodeType.ReversePath:
                        EvaluateReversePathNode(in doc, nodeId, ref paths, ref outputAnchors, Allocator.Temp);
                        break;
                }
            }

            CollectAllSections(in doc, in sortedNodes, in paths, out var allSectionNodes, Allocator.Temp);
            PopulateNodeFlags(in doc, in paths, ref nodeFlags);

            track = new Track {
                Points = new NativeList<Point>(256, allocator),
                Sections = new NativeArray<Section>(allSectionNodes.Length, allocator),
                NodeToSection = new NativeHashMap<uint, int>(allSectionNodes.Length, allocator),
                TraversalOrder = default
            };

            for (int i = 0; i < allSectionNodes.Length; i++) {
                uint nodeId = allSectionNodes[i];
                byte flags = nodeFlags.TryGetValue(nodeId, out byte f) ? f : (byte)0;

                track.NodeToSection[nodeId] = i;

                if (!paths.TryGetValue(nodeId, out var path) || path.Length < 2) {
                    track.Sections[i] = new Section {
                        StartIndex = -1,
                        EndIndex = -1,
                        ArcStart = 0f,
                        ArcEnd = 0f,
                        Flags = flags,
                        Next = SectionLink.None,
                        Prev = SectionLink.None
                    };
                    continue;
                }

                int startIndex = track.Points.Length;
                for (int j = 0; j < path.Length; j++) {
                    track.Points.Add(path[j]);
                }
                int endIndex = track.Points.Length - 1;

                track.Sections[i] = new Section {
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    ArcStart = path[0].SpineArc,
                    ArcEnd = path[^1].SpineArc,
                    Flags = flags,
                    Next = SectionLink.None,
                    Prev = SectionLink.None
                };
            }

            BuildTraversalOrder(in doc, in allSectionNodes, out var traversalIndices, Allocator.Temp);
            track.TraversalOrder = new NativeArray<int>(traversalIndices.Length, allocator);
            for (int i = 0; i < traversalIndices.Length; i++) {
                track.TraversalOrder[i] = traversalIndices[i];
            }
            traversalIndices.Dispose();

            ComputeContinuations(in doc, in allSectionNodes, ref track);
            ComputeSpatialContinuations(ref track);

            var pathKeys = paths.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < pathKeys.Length; i++) {
                if (paths.TryGetValue(pathKeys[i], out var list) && list.IsCreated) {
                    list.Dispose();
                }
            }
            pathKeys.Dispose();
            paths.Dispose();
            outputAnchors.Dispose();
            nodeFlags.Dispose();
            allSectionNodes.Dispose();
            sortedNodes.Dispose();
        }

        [BurstCompile]
        private static void PopulateNodeFlags(in Document.Document doc, in NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, byte> nodeFlags) {
            var keys = paths.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++) {
                uint nodeId = keys[i];
                byte flags = 0;

                ulong facingKey = Document.Document.InputKey(nodeId, NodeMeta.Facing);
                if (doc.Flags.TryGetValue(facingKey, out int f) && f < 0) {
                    flags |= NodeFlag.Reversed;
                }

                nodeFlags[nodeId] = flags;
            }
            keys.Dispose();
        }

        [BurstCompile]
        private static void CollectAllSections(
            in Document.Document doc,
            in NativeList<uint> sortedNodes,
            in NativeHashMap<uint, NativeList<Point>> paths,
            out NativeList<uint> sectionNodes,
            Allocator allocator
        ) {
            sectionNodes = new NativeList<uint>(sortedNodes.Length, allocator);

            for (int i = 0; i < sortedNodes.Length; i++) {
                uint nodeId = sortedNodes[i];
                if (!paths.ContainsKey(nodeId)) continue;
                if (!doc.Graph.TryGetNodeType(nodeId, out NodeType nodeType)) continue;

                bool isSectionProducing = nodeType == NodeType.Force ||
                                          nodeType == NodeType.Geometric ||
                                          nodeType == NodeType.Curved ||
                                          nodeType == NodeType.CopyPath ||
                                          nodeType == NodeType.Bridge;
                if (!isSectionProducing) continue;

                sectionNodes.Add(nodeId);
            }
        }

        [BurstCompile]
        private static void BuildTraversalOrder(
            in Document.Document doc,
            in NativeList<uint> sectionNodes,
            out NativeList<int> traversalIndices,
            Allocator allocator
        ) {
            var candidates = new NativeList<int>(sectionNodes.Length, Allocator.Temp);

            for (int i = 0; i < sectionNodes.Length; i++) {
                uint nodeId = sectionNodes[i];
                ulong priorityKey = Document.Document.InputKey(nodeId, NodeMeta.Priority);
                float priority = doc.Scalars.TryGetValue(priorityKey, out float p) ? p : 0f;
                if (priority < 0f) continue;

                candidates.Add(i);
            }

            for (int i = 1; i < candidates.Length; i++) {
                int currentIdx = candidates[i];
                uint currentNode = sectionNodes[currentIdx];
                ulong currentKey = Document.Document.InputKey(currentNode, NodeMeta.Priority);
                float currentPriority = doc.Scalars.TryGetValue(currentKey, out float cp) ? cp : 0f;

                int j = i - 1;
                while (j >= 0) {
                    int otherIdx = candidates[j];
                    uint otherNode = sectionNodes[otherIdx];
                    ulong otherKey = Document.Document.InputKey(otherNode, NodeMeta.Priority);
                    float otherPriority = doc.Scalars.TryGetValue(otherKey, out float op) ? op : 0f;

                    if (currentPriority > otherPriority) {
                        candidates[j + 1] = otherIdx;
                        j--;
                    }
                    else {
                        break;
                    }
                }
                candidates[j + 1] = currentIdx;
            }

            traversalIndices = new NativeList<int>(candidates.Length, allocator);
            for (int i = 0; i < candidates.Length; i++) {
                traversalIndices.Add(candidates[i]);
            }

            candidates.Dispose();
        }

        [BurstCompile]
        private static void ComputeContinuations(
            in Document.Document doc,
            in NativeList<uint> sectionNodes,
            ref Track track
        ) {
            for (int i = 0; i < sectionNodes.Length; i++) {
                uint nodeId = sectionNodes[i];
                int nextSection = FindNextSection(in doc.Graph, nodeId, in track.NodeToSection);

                if (nextSection >= 0) {
                    // Graph connections: our END connects to their START (same direction, no flip)
                    var section = track.Sections[i];
                    section.Next = SectionLink.Create(nextSection, atStart: true, flip: false);
                    track.Sections[i] = section;

                    var nextSec = track.Sections[nextSection];
                    nextSec.Prev = SectionLink.Create(i, atStart: false, flip: false);
                    track.Sections[nextSection] = nextSec;
                }
            }
        }

        private const float SPATIAL_TOLERANCE = 0.01f;
        private const float DIRECTION_THRESHOLD = 0.9f;

        [BurstCompile]
        private static void ComputeSpatialContinuations(ref Track track) {
            for (int i = 0; i < track.Sections.Length; i++) {
                var section = track.Sections[i];
                if (!section.IsValid) continue;

                if (!section.Next.IsValid) {
                    FindSpatialMatch(ref track, i, true, out int idx, out bool atStart, out bool flip);
                    if (idx >= 0) {
                        section.Next = SectionLink.Create(idx, atStart, flip);
                        track.Sections[i] = section;
                    }
                }

                if (!section.Prev.IsValid) {
                    FindSpatialMatch(ref track, i, false, out int idx, out bool atStart, out bool flip);
                    if (idx >= 0) {
                        section.Prev = SectionLink.Create(idx, atStart, flip);
                        track.Sections[i] = section;
                    }
                }
            }
        }

        [BurstCompile]
        private static void FindSpatialMatch(ref Track track, int sectionIndex, bool isNext, out int matchIndex, out bool matchAtStart, out bool matchFlip) {
            matchIndex = -1;
            matchAtStart = false;
            matchFlip = false;

            var section = track.Sections[sectionIndex];

            // Get our geometric connection point and direction
            // For overhang: Next = past ArcEnd (EndIndex), Prev = before ArcStart (StartIndex)
            int ourPointIndex = isNext ? section.EndIndex : section.StartIndex;
            var ourPoint = track.Points[ourPointIndex];
            float3 ourPos = ourPoint.HeartPosition;
            float3 ourGeoDir = ourPoint.Frame.Direction;

            float bestDist = float.MaxValue;
            bool bestIsCosmetic = false;

            for (int i = 0; i < track.Sections.Length; i++) {
                if (i == sectionIndex) continue;

                var candidate = track.Sections[i];
                if (!candidate.IsValid) continue;

                bool isCosmetic = IsCosmeticSection(ref track, i);

                var candStart = track.Points[candidate.StartIndex];
                var candEnd = track.Points[candidate.EndIndex];

                // Check both endpoints of candidate for geometric alignment
                // For Next (overhang in +geoDir): need candidate geometry extending in +ourGeoDir
                // For Prev (overhang in -geoDir): need candidate geometry extending in -ourGeoDir
                // Same direction = no flip, Opposite direction = flip

                if (isNext) {
                    // Pattern 1: candStart near ourEnd, same direction → no flip
                    float distStart = math.distance(candStart.HeartPosition, ourPos);
                    if (distStart < SPATIAL_TOLERANCE) {
                        float dirDot = math.dot(ourGeoDir, candStart.Frame.Direction);
                        if (dirDot > DIRECTION_THRESHOLD && IsBetterMatch(distStart, isCosmetic, bestDist, bestIsCosmetic)) {
                            bestDist = distStart;
                            matchIndex = i;
                            matchAtStart = true;
                            matchFlip = false;
                            bestIsCosmetic = isCosmetic;
                        }
                    }

                    // Pattern 2: candEnd near ourEnd, opposite direction → flip
                    float distEnd = math.distance(candEnd.HeartPosition, ourPos);
                    if (distEnd < SPATIAL_TOLERANCE) {
                        float dirDot = math.dot(ourGeoDir, candEnd.Frame.Direction);
                        if (dirDot < -DIRECTION_THRESHOLD && IsBetterMatch(distEnd, isCosmetic, bestDist, bestIsCosmetic)) {
                            bestDist = distEnd;
                            matchIndex = i;
                            matchAtStart = false;
                            matchFlip = true;
                            bestIsCosmetic = isCosmetic;
                        }
                    }
                }
                else {
                    // Pattern 1: candEnd near ourStart, same direction → no flip
                    float distEnd = math.distance(candEnd.HeartPosition, ourPos);
                    if (distEnd < SPATIAL_TOLERANCE) {
                        float dirDot = math.dot(ourGeoDir, candEnd.Frame.Direction);
                        if (dirDot > DIRECTION_THRESHOLD && IsBetterMatch(distEnd, isCosmetic, bestDist, bestIsCosmetic)) {
                            bestDist = distEnd;
                            matchIndex = i;
                            matchAtStart = false;
                            matchFlip = false;
                            bestIsCosmetic = isCosmetic;
                        }
                    }

                    // Pattern 2: candStart near ourStart, opposite direction → flip
                    float distStart = math.distance(candStart.HeartPosition, ourPos);
                    if (distStart < SPATIAL_TOLERANCE) {
                        float dirDot = math.dot(ourGeoDir, candStart.Frame.Direction);
                        if (dirDot < -DIRECTION_THRESHOLD && IsBetterMatch(distStart, isCosmetic, bestDist, bestIsCosmetic)) {
                            bestDist = distStart;
                            matchIndex = i;
                            matchAtStart = true;
                            matchFlip = true;
                            bestIsCosmetic = isCosmetic;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private static bool IsCosmeticSection(ref Track track, int sectionIndex) {
            for (int j = 0; j < track.TraversalCount; j++) {
                if (track.TraversalOrder[j] == sectionIndex) return false;
            }
            return true;
        }

        [BurstCompile]
        private static bool IsBetterMatch(float dist, bool isCosmetic, float bestDist, bool bestIsCosmetic) {
            if (isCosmetic && !bestIsCosmetic) return true;
            if (!isCosmetic && bestIsCosmetic) return false;
            return dist < bestDist;
        }

        [BurstCompile]
        private static int FindNextSection(
            in KexEdit.Graph.Graph graph,
            uint nodeId,
            in NativeHashMap<uint, int> nodeToSection
        ) {
            if (!graph.TryGetOutputBySpec(nodeId, PortDataType.Anchor, 0, out uint outputPort)) return -1;

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeSources[i] != outputPort) continue;

                uint targetPort = graph.EdgeTargets[i];
                if (!graph.TryGetPortIndex(targetPort, out int portIndex)) continue;

                uint targetNode = graph.PortOwners[portIndex];
                if (!graph.TryGetNodeType(targetNode, out NodeType targetType)) continue;

                if (targetType == NodeType.Reverse || targetType == NodeType.ReversePath) {
                    continue;
                }

                if (nodeToSection.TryGetValue(targetNode, out int sectionIndex)) {
                    return sectionIndex;
                }

                if (targetType == NodeType.Anchor) {
                    int result = FindNextSection(in graph, targetNode, in nodeToSection);
                    if (result >= 0) return result;
                }
            }

            return -1;
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
            in KexEdit.Graph.Graph graph, in NativeHashMap<uint, Point> outputAnchors,
            uint nodeId, int index, out Point anchor
        ) {
            anchor = default;
            if (!graph.TryGetInput(nodeId, index, out uint portId)) return false;
            return TryGetAnchorFromPort(in graph, in outputAnchors, portId, out anchor);
        }

        [BurstCompile]
        private static bool TryGetAnchorFromPort(
            in KexEdit.Graph.Graph graph, in NativeHashMap<uint, Point> outputAnchors,
            uint portId, out Point anchor
        ) {
            anchor = default;
            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeTargets[i] != portId) continue;

                uint sourcePortId = graph.EdgeSources[i];
                if (!graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = graph.PortOwners[portIndex];
                if (outputAnchors.TryGetValue(sourceNodeId, out anchor)) {
                    return true;
                }
            }
            return false;
        }

        [BurstCompile]
        private static void EvaluateAnchorNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, Point> outputAnchors) {
            ulong posKey = Document.Document.InputKey(nodeId, AnchorPorts.Position);
            float3 position = doc.Vectors.TryGetValue(posKey, out var pos) ? pos : float3.zero;
            float roll = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Roll, 0f);
            float pitch = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Pitch, 0f);
            float yaw = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Yaw, 0f);
            float velocity = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Velocity, DEFAULT_VELOCITY);
            float heart = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Heart, DEFAULT_HEART_OFFSET);
            float friction = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Friction, DEFAULT_FRICTION);
            float resistance = GetScalar(in doc.Scalars, nodeId, AnchorPorts.Resistance, DEFAULT_RESISTANCE);

            Frame frame = Frame.FromEuler(pitch, yaw, roll);
            float centerY = frame.SpinePosition(position, heart * 0.9f).y;
            float energy = 0.5f * velocity * velocity + KexEdit.Sim.Sim.G * centerY;

            AnchorNode.Build(
                in position, pitch, yaw, roll,
                velocity, energy,
                heart, friction, resistance,
                out Point anchor
            );

            outputAnchors[nodeId] = anchor;
        }

        [BurstCompile]
        private static void EvaluateForceNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, ForcePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = GetScalar(in doc.Scalars, nodeId, NodeMeta.Duration, 1f);
            var durationType = GetFlag(in doc.Flags, nodeId, NodeMeta.DurationType) == 1
                ? DurationType.Distance : DurationType.Time;

            var config = new IterationConfig(duration, durationType);
            bool driven = GetFlag(in doc.Flags, nodeId, NodeMeta.Driven) == 1;

            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.NormalForce, out var normalForce);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.LateralForce, out var lateralForce);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            ForceNode.Build(
                in inputAnchor, in config, driven,
                in rollSpeed, in normalForce, in lateralForce,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateGeometricNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, GeometricPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float duration = GetScalar(in doc.Scalars, nodeId, NodeMeta.Duration, 1f);
            var durationType = GetFlag(in doc.Flags, nodeId, NodeMeta.DurationType) == 1
                ? DurationType.Distance : DurationType.Time;

            var config = new IterationConfig(duration, durationType);
            bool driven = GetFlag(in doc.Flags, nodeId, NodeMeta.Driven) == 1;
            bool steering = GetFlag(in doc.Flags, nodeId, NodeMeta.Steering) == 1;

            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.PitchSpeed, out var pitchSpeed);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.YawSpeed, out var yawSpeed);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            GeometricNode.Build(
                in inputAnchor, in config, driven, steering,
                in rollSpeed, in pitchSpeed, in yawSpeed,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateCurvedNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, CurvedPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            float radius = GetScalar(in doc.Scalars, nodeId, CurvedPorts.Radius, 10f);
            float arc = GetScalar(in doc.Scalars, nodeId, CurvedPorts.Arc, 90f);
            float axis = GetScalar(in doc.Scalars, nodeId, CurvedPorts.Axis, 0f);
            float leadIn = GetScalar(in doc.Scalars, nodeId, CurvedPorts.LeadIn, 0f);
            float leadOut = GetScalar(in doc.Scalars, nodeId, CurvedPorts.LeadOut, 0f);

            bool driven = GetFlag(in doc.Flags, nodeId, NodeMeta.Driven) == 1;

            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.RollSpeed, out var rollSpeed);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            CurvedNode.Build(
                in inputAnchor, radius, arc, axis, leadIn, leadOut, driven,
                in rollSpeed, in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateBridgeNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, BridgePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, BridgePorts.Target, out Point targetAnchor)) {
                return;
            }

            float inWeight = GetScalar(in doc.Scalars, nodeId, BridgePorts.InWeight, 0.5f);
            float outWeight = GetScalar(in doc.Scalars, nodeId, BridgePorts.OutWeight, 0.5f);
            bool driven = GetFlag(in doc.Flags, nodeId, NodeMeta.Driven) == 1;

            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            BridgeNode.Build(
                in inputAnchor, in targetAnchor, inWeight, outWeight, driven,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateCopyPathNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, CopyPathPorts.Anchor, out Point inputAnchor)) {
                return;
            }

            if (!TryGetPath(in doc.Graph, in paths, nodeId, CopyPathPorts.Path, out var sourcePath)) {
                return;
            }

            float start = GetScalar(in doc.Scalars, nodeId, CopyPathPorts.Start, -1f);
            float end = GetScalar(in doc.Scalars, nodeId, CopyPathPorts.End, -1f);
            bool driven = GetFlag(in doc.Flags, nodeId, NodeMeta.Driven) == 1;

            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.DrivenVelocity, out var drivenVelocity);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.HeartOffset, out var heartOffset);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Friction, out var friction);
            GetKeyframes(in doc.Keyframes, nodeId, PropertyId.Resistance, out var resistance);

            var path = new NativeList<Point>(256, allocator);

            CopyPathNode.Build(
                in inputAnchor, sourcePath.AsArray(), start, end, driven,
                in drivenVelocity, in heartOffset, in friction, in resistance,
                inputAnchor.HeartOffset, inputAnchor.Friction, inputAnchor.Resistance,
                ref path
            );

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static void EvaluateReverseNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, Point> outputAnchors) {
            if (!TryGetAnchor(in doc.Graph, in outputAnchors, nodeId, ReversePorts.Anchor, out Point inputAnchor)) {
                return;
            }

            ReverseNode.Build(in inputAnchor, out Point reversed);
            outputAnchors[nodeId] = reversed;
        }

        [BurstCompile]
        private static void EvaluateReversePathNode(in Document.Document doc, uint nodeId, ref NativeHashMap<uint, NativeList<Point>> paths, ref NativeHashMap<uint, Point> outputAnchors, Allocator allocator) {
            if (!TryGetPath(in doc.Graph, in paths, nodeId, ReversePathPorts.Path, out var sourcePath)) {
                return;
            }

            var path = new NativeList<Point>(sourcePath.Length, allocator);
            ReversePathNode.Build(sourcePath.AsArray(), ref path);

            if (path.Length > 0) {
                outputAnchors[nodeId] = path[^1];
            }
            paths[nodeId] = path;
        }

        [BurstCompile]
        private static bool TryGetPath(
            in KexEdit.Graph.Graph graph, in NativeHashMap<uint, NativeList<Point>> paths,
            uint nodeId, int index, out NativeList<Point> path
        ) {
            path = default;
            if (!graph.TryGetInput(nodeId, index, out uint portId)) return false;
            return GetPathFromPort(in graph, in paths, portId, out path);
        }

        [BurstCompile]
        private static bool GetPathFromPort(
            in KexEdit.Graph.Graph graph, in NativeHashMap<uint, NativeList<Point>> paths,
            uint portId, out NativeList<Point> path
        ) {
            path = default;
            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeTargets[i] != portId) continue;

                uint sourcePortId = graph.EdgeSources[i];
                if (!graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = graph.PortOwners[portIndex];
                if (paths.TryGetValue(sourceNodeId, out path)) {
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
            ulong key = Document.Document.InputKey(nodeId, inputIndex);
            return scalars.TryGetValue(key, out float value) ? value : defaultValue;
        }

        [BurstCompile]
        private static int GetFlag(
            in NativeHashMap<ulong, int> flags,
            uint nodeId, int propertyIndex
        ) {
            ulong key = Document.Document.InputKey(nodeId, propertyIndex);
            return flags.TryGetValue(key, out int value) ? value : 0;
        }

        [BurstCompile]
        private static void GetKeyframes(
            in KeyframeStore store,
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

        [BurstCompile]
        public void SamplePoint(int sectionIndex, float arc, out Point result) {
            if (sectionIndex < 0 || sectionIndex >= Sections.Length) {
                result = Point.Default;
                return;
            }

            var section = Sections[sectionIndex];
            if (!section.IsValid) {
                result = Point.Default;
                return;
            }

            InterpolatePoint(section, arc, out result);
        }

        [BurstCompile]
        public void SampleSplinePoint(int sectionIndex, float arc, out SplinePoint result) {
            SamplePoint(sectionIndex, arc, out Point p);
            SplineResampler.ToSplinePoint(in p, out result);
        }

        [BurstCompile]
        public void Extrapolate(int sectionIndex, float arc, bool fromEnd, out SplinePoint result) {
            if (sectionIndex < 0 || sectionIndex >= Sections.Length) {
                result = SplinePoint.Default;
                return;
            }

            var section = Sections[sectionIndex];
            if (!section.IsValid) {
                result = SplinePoint.Default;
                return;
            }

            if (fromEnd) {
                ExtrapolateFromEnd(section, arc, out result);
            }
            else {
                ExtrapolateFromStart(section, arc, out result);
            }
        }

        [BurstCompile]
        public float3 GetSectionStartPosition(int sectionIndex) {
            if (sectionIndex < 0 || sectionIndex >= Sections.Length) return float3.zero;
            var section = Sections[sectionIndex];
            if (!section.IsValid) return float3.zero;
            return Points[section.StartIndex].HeartPosition;
        }

        [BurstCompile]
        public float3 GetSectionEndPosition(int sectionIndex) {
            if (sectionIndex < 0 || sectionIndex >= Sections.Length) return float3.zero;
            var section = Sections[sectionIndex];
            if (!section.IsValid) return float3.zero;
            return Points[section.EndIndex].HeartPosition;
        }

        [BurstCompile]
        private void InterpolatePoint(Section section, float arc, out Point result) {
            if (section.StartIndex >= section.EndIndex) {
                result = Points[section.StartIndex];
                return;
            }

            if (arc <= Points[section.StartIndex].SpineArc) {
                result = Points[section.StartIndex];
                return;
            }
            if (arc >= Points[section.EndIndex].SpineArc) {
                result = Points[section.EndIndex];
                return;
            }

            int lo = section.StartIndex;
            int hi = section.EndIndex;
            while (lo < hi - 1) {
                int mid = (lo + hi) / 2;
                if (Points[mid].SpineArc <= arc) lo = mid;
                else hi = mid;
            }

            Point a = Points[lo];
            Point b = Points[lo + 1];
            float segLen = b.SpineArc - a.SpineArc;
            float t = segLen > 0f ? (arc - a.SpineArc) / segLen : 0f;

            Interpolate(in a, in b, t, out result);
        }

        [BurstCompile]
        private static void Interpolate(in Point p0, in Point p1, float t, out Point result) {
            result = new Point(
                heartPosition: math.lerp(p0.HeartPosition, p1.HeartPosition, t),
                direction: math.normalizesafe(math.lerp(p0.Direction, p1.Direction, t)),
                normal: math.normalizesafe(math.lerp(p0.Normal, p1.Normal, t)),
                lateral: math.normalizesafe(math.lerp(p0.Lateral, p1.Lateral, t)),
                velocity: math.lerp(p0.Velocity, p1.Velocity, t),
                energy: math.lerp(p0.Energy, p1.Energy, t),
                normalForce: math.lerp(p0.NormalForce, p1.NormalForce, t),
                lateralForce: math.lerp(p0.LateralForce, p1.LateralForce, t),
                heartArc: math.lerp(p0.HeartArc, p1.HeartArc, t),
                spineArc: math.lerp(p0.SpineArc, p1.SpineArc, t),
                heartAdvance: math.lerp(p0.HeartAdvance, p1.HeartAdvance, t),
                frictionOrigin: math.lerp(p0.FrictionOrigin, p1.FrictionOrigin, t),
                rollSpeed: math.lerp(p0.RollSpeed, p1.RollSpeed, t),
                heartOffset: math.lerp(p0.HeartOffset, p1.HeartOffset, t),
                friction: math.lerp(p0.Friction, p1.Friction, t),
                resistance: math.lerp(p0.Resistance, p1.Resistance, t)
            );
        }

        [BurstCompile]
        private void ExtrapolateFromStart(Section section, float arc, out SplinePoint result) {
            Point anchor = Points[section.StartIndex];
            SplineResampler.ToSplinePoint(in anchor, out var sp);
            float delta = arc - section.ArcStart;

            result = new SplinePoint(
                arc,
                sp.Position + sp.Direction * delta,
                sp.Direction,
                sp.Normal,
                sp.Lateral
            );
        }

        [BurstCompile]
        private void ExtrapolateFromEnd(Section section, float arc, out SplinePoint result) {
            Point anchor = Points[section.EndIndex];
            SplineResampler.ToSplinePoint(in anchor, out var sp);
            float delta = arc - section.ArcEnd;

            result = new SplinePoint(
                arc,
                sp.Position + sp.Direction * delta,
                sp.Direction,
                sp.Normal,
                sp.Lateral
            );
        }

        public void Dispose() {
            if (Points.IsCreated) Points.Dispose();
            if (Sections.IsCreated) Sections.Dispose();
            if (NodeToSection.IsCreated) NodeToSection.Dispose();
            if (TraversalOrder.IsCreated) TraversalOrder.Dispose();
        }
    }
}
