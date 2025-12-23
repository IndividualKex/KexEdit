using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI.NodeGraph {
    public class NodeGraphData : IComponentData {
        public Entity Coaster;
        public Dictionary<Entity, NodeData> Nodes = new();
        public Dictionary<Entity, EdgeData> Edges = new();
        public Vector2 Pan;
        public float Zoom = 1f;
        public bool HasSelectedNodes;
        public bool HasSelectedEdges;
    }
}
