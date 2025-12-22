using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct GraphTraversalSystem : ISystem {
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Node, CoasterReference, InputPortReference>()
                .Build(state.EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection>()
                .Build(state.EntityManager);

            state.RequireForUpdate<Coaster>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI.Query<RefRW<Coaster>>().WithEntityAccess()) {
                ref var coasterRef = ref coaster.ValueRW;
                var coasterNodes = new NativeList<Entity>(nodes.Length, Allocator.Temp);
                foreach (var nodeEntity in nodes) {
                    var nodeCoaster = SystemAPI.GetComponent<CoasterReference>(nodeEntity).Value;
                    if (nodeCoaster != entity) continue;
                    coasterNodes.Add(nodeEntity);
                }
                coasterRef.RootNode = FindGraphRoot(ref state, entity, coasterNodes);
                coasterNodes.Dispose();
            }
            nodes.Dispose();
        }

        private Entity FindGraphRoot(ref SystemState state, in Entity coaster, in NativeList<Entity> nodes) {
            if (nodes.Length == 0) return Entity.Null;

            using var connections = _connectionQuery.ToComponentDataArray<Connection>(Allocator.Temp);
            using var nodeMap = new NativeHashMap<Entity, Entity>(nodes.Length, Allocator.Temp);
            using var nodePriorityMap = new NativeHashMap<Entity, int>(nodes.Length, Allocator.Temp);
            using var incomingConnections = new NativeHashSet<Entity>(nodes.Length, Allocator.Temp);

            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetComponent<Node>(nodeEntity);
                if (node.Type == NodeType.Mesh || node.Type == NodeType.Append) continue;
                var inputPorts = SystemAPI.GetBuffer<InputPortReference>(nodeEntity);
                foreach (var inputPort in inputPorts) {
                    nodeMap.Add(inputPort, nodeEntity);
                }
                nodePriorityMap.Add(nodeEntity, node.Priority);
            }

            var connectionMap = new NativeHashMap<Entity, Entity>(connections.Length, Allocator.Temp);
            var connectionPriorities = new NativeHashMap<Entity, int>(connections.Length, Allocator.Temp);

            foreach (var connection in connections) {
                if (!nodeMap.TryGetValue(connection.Target, out var targetNode) ||
                    !nodePriorityMap.TryGetValue(targetNode, out var priority)) continue;

                if (!connectionPriorities.TryGetValue(connection.Source, out var currentPriority) ||
                    priority > currentPriority) {
                    connectionMap[connection.Source] = connection.Target;
                    connectionPriorities[connection.Source] = priority;
                }
            }

            foreach (var kvp in connectionMap) {
                if (nodeMap.TryGetValue(kvp.Value, out var targetNode)) {
                    incomingConnections.Add(targetNode);
                }
            }

            Entity bestRoot = Entity.Null;
            int highestPriority = int.MinValue;

            var potentialRoots = new NativeList<Entity>(nodes.Length, Allocator.Temp);

            foreach (var nodeEntity in nodes) {
                if (incomingConnections.Contains(nodeEntity)) continue;

                if (nodePriorityMap.TryGetValue(nodeEntity, out var priority)) {
                    if (priority > highestPriority) {
                        highestPriority = priority;
                        potentialRoots.Clear();
                        potentialRoots.Add(nodeEntity);
                    }
                    else if (priority == highestPriority) {
                        potentialRoots.Add(nodeEntity);
                    }
                }
            }

            if (potentialRoots.Length == 1) {
                bestRoot = potentialRoots[0];
            }
            else if (potentialRoots.Length > 1) {
                int longestPathLength = 0;

                foreach (var rootCandidate in potentialRoots) {
                    var tempGraph = new NativeHashMap<Entity, Entity>(nodes.Length, Allocator.Temp);
                    TraverseGraph(ref state, rootCandidate, ref tempGraph, nodeMap, connectionMap);
                    int pathLength = tempGraph.Count;

                    if (pathLength > longestPathLength) {
                        longestPathLength = pathLength;
                        bestRoot = rootCandidate;
                    }

                    tempGraph.Dispose();
                }
            }

            potentialRoots.Dispose();

            if (bestRoot != Entity.Null) {
                var rawGraph = new NativeHashMap<Entity, Entity>(nodes.Length, Allocator.Temp);
                TraverseGraph(ref state, bestRoot, ref rawGraph, nodeMap, connectionMap);

                var pointLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(true);
                using var processedGraph = PostProcessGraph(rawGraph, pointLookup);

                using var reverseGraph = new NativeHashMap<Entity, Entity>(processedGraph.Count, Allocator.Temp);
                foreach (var kvp in processedGraph) {
                    reverseGraph.TryAdd(kvp.Value, kvp.Key);
                }

                foreach (var nodeEntity in nodes) {
                    ref var nodeRef = ref SystemAPI.GetComponentRW<Node>(nodeEntity).ValueRW;
                    nodeRef.Next = processedGraph.TryGetValue(nodeEntity, out var nextNode) ? nextNode : Entity.Null;
                    nodeRef.Previous = reverseGraph.TryGetValue(nodeEntity, out var prevNode) ? prevNode : Entity.Null;
                }

                if (!pointLookup.TryGetBuffer(bestRoot, out var rootPoints) || rootPoints.Length < 2) {
                    bestRoot = FindValidRoot(in rawGraph, in pointLookup, bestRoot);
                }

                rawGraph.Dispose();
            }
            else {
                foreach (var nodeEntity in nodes) {
                    ref var nodeRef = ref SystemAPI.GetComponentRW<Node>(nodeEntity).ValueRW;
                    nodeRef.Next = Entity.Null;
                    nodeRef.Previous = Entity.Null;
                }
            }

            connectionMap.Dispose();
            connectionPriorities.Dispose();

            return bestRoot;
        }

        private void TraverseGraph(
            ref SystemState state,
            Entity node,
            ref NativeHashMap<Entity, Entity> graph,
            in NativeHashMap<Entity, Entity> nodeMap,
            in NativeHashMap<Entity, Entity> connectionMap
        ) {

            if (!SystemAPI.HasBuffer<OutputPortReference>(node)) return;
            var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(node);
            if (outputPortBuffer.Length == 0) return;

            if (connectionMap.TryGetValue(outputPortBuffer[0].Value, out var targetPort)) {
                if (nodeMap.TryGetValue(targetPort, out var nextNode)) {
                    graph.Add(node, nextNode);
                    TraverseGraph(ref state, nextNode, ref graph, nodeMap, connectionMap);
                }
            }
        }

        private Entity FindNextValidNode(
            Entity start,
            in NativeHashMap<Entity, Entity> rawGraph,
            in BufferLookup<CorePointBuffer> pointLookup
        ) {
            Entity current = start;
            while (current != Entity.Null) {
                if (pointLookup.TryGetBuffer(current, out var points) && points.Length >= 2) {
                    return current;
                }
                if (!rawGraph.TryGetValue(current, out current)) {
                    break;
                }
            }
            return Entity.Null;
        }

        private NativeHashMap<Entity, Entity> PostProcessGraph(
            in NativeHashMap<Entity, Entity> rawGraph,
            in BufferLookup<CorePointBuffer> pointLookup
        ) {
            var graph = new NativeHashMap<Entity, Entity>(rawGraph.Count, Allocator.Temp);

            foreach (var kvp in rawGraph) {
                Entity source = kvp.Key;
                if (!pointLookup.TryGetBuffer(source, out var sourcePoints) ||
                    sourcePoints.Length < 2) {
                    continue;
                }

                Entity target = FindNextValidNode(kvp.Value, rawGraph, pointLookup);
                if (target != Entity.Null) {
                    graph.Add(source, target);
                }
            }

            return graph;
        }

        private Entity FindValidRoot(
            in NativeHashMap<Entity, Entity> rawGraph,
            in BufferLookup<CorePointBuffer> pointLookup,
            Entity startRoot
        ) {
            return FindNextValidNode(startRoot, rawGraph, pointLookup);
        }
    }
}
