using System;
using System.Collections.Generic;

namespace KexEdit.UI.NodeGraph {
    public class NodeGraphNodePool {
        private static readonly Dictionary<NodeType, int> s_PrewarmCounts = new() {
            { NodeType.ForceSection, 16 },
            { NodeType.GeometricSection, 16 },
            { NodeType.CurvedSection, 8 },
            { NodeType.CopyPathSection, 8 },
            { NodeType.Anchor, 4 },
            { NodeType.Bridge, 4 },
            { NodeType.Reverse, 4 },
            { NodeType.ReversePath, 4 },
            { NodeType.Mesh, 4 },
            { NodeType.Append, 4 },
        };

        private readonly Dictionary<NodeType, Stack<NodeGraphNode>> _pools = new();
        private readonly NodeGraphView _view;

        public NodeGraphNodePool(NodeGraphView view) {
            _view = view;

            foreach (NodeType type in Enum.GetValues(typeof(NodeType))) {
                _pools[type] = new Stack<NodeGraphNode>();

                for (int i = 0; i < s_PrewarmCounts[type]; i++) {
                    _pools[type].Push(new NodeGraphNode(_view, type));
                }
            }
        }

        public NodeGraphNode Get(NodeType type) {
            if (!_pools.TryGetValue(type, out var stack)) {
                stack = new Stack<NodeGraphNode>();
                _pools[type] = stack;
            }
            

            if (stack.Count > 0) {
                return stack.Pop();
            }

            UnityEngine.Debug.LogWarning($"NodeGraphNodePool: No more nodes of type {type} available");
            return new NodeGraphNode(_view, type);
        }

        public void Return(NodeGraphNode node) {
            var type = node.Type;
            var pool = _pools[type];
            pool.Push(node);
        }
    }
}
