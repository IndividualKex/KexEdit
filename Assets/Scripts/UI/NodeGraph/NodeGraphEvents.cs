using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI.NodeGraph {
    public static class NodeGraphEvents {
        public static void Send<T>(this VisualElement element) where T : NodeGraphEvent<T>, new() {
            using var e = EventBase<T>.GetPooled() as T;
            e.target = element;
            element.panel.visualTree.SendEvent(e);
        }

        public static T GetPooled<T>(this VisualElement element) where T : NodeGraphEvent<T>, new() {
            var e = EventBase<T>.GetPooled() as T;
            e.target = element;
            return e;
        }

        public static void Send<T>(this VisualElement element, T e) where T : NodeGraphEvent<T>, new() {
            using (e) {
                element.panel.visualTree.SendEvent(e);
            }
        }
    }

    public class NodeGraphEvent<T> : EventBase<T> where T : NodeGraphEvent<T>, new() {
        public NodeGraphEvent() {
            LocalInit();
        }

        protected override void Init() {
            base.Init();
            LocalInit();
        }

        protected virtual void LocalInit() {
            bubbles = true;
            tricklesDown = true;
        }
    }

    public class ViewRightClickEvent : NodeGraphEvent<ViewRightClickEvent> {
        public Vector2 MousePosition;
        public Vector2 ContentPosition;
    }

    public class NodeClickEvent : NodeGraphEvent<NodeClickEvent> {
        public Entity Node;
        public bool ShiftKey;
    }

    public class NodeRightClickEvent : NodeGraphEvent<NodeRightClickEvent> {
        public Entity Node;
        public Vector2 MousePosition;
    }

    public class EdgeClickEvent : NodeGraphEvent<EdgeClickEvent> {
        public Entity Edge;
        public bool ShiftKey;
    }

    public class EdgeRightClickEvent : NodeGraphEvent<EdgeRightClickEvent> {
        public Vector2 MousePosition;
    }

    public class StartDragNodesEvent : NodeGraphEvent<StartDragNodesEvent> { }

    public class DragNodesEvent : NodeGraphEvent<DragNodesEvent> {
        public NodeGraphNode Node;
        public Vector2 Delta;
    }

    public class EndDragNodesEvent : NodeGraphEvent<EndDragNodesEvent> { }

    public class PortChangeEvent : NodeGraphEvent<PortChangeEvent> {
        public PortData Port;
    }

    public class DragOutputPortEvent : NodeGraphEvent<DragOutputPortEvent> {
        public PortData Port;
        public Vector2 MousePosition;
    }

    public class AnchorPromoteEvent : NodeGraphEvent<AnchorPromoteEvent> {
        public PortData Port;
    }

    public class AddConnectionEvent : NodeGraphEvent<AddConnectionEvent> {
        public PortData Source;
        public PortData Target;
    }

    public class SelectionEvent : NodeGraphEvent<SelectionEvent> {
        public List<Entity> Nodes;
        public List<Entity> Edges;
    }

    public class ClearSelectionEvent : NodeGraphEvent<ClearSelectionEvent> { }

    public class DurationTypeChangeEvent : NodeGraphEvent<DurationTypeChangeEvent> {
        public Entity Node;
        public DurationType DurationType;
    }

    public class RenderToggleChangeEvent : NodeGraphEvent<RenderToggleChangeEvent> {
        public Entity Node;
        public bool Render;
    }

    public class PriorityChangeEvent : NodeGraphEvent<PriorityChangeEvent> {
        public Entity Node;
        public int Priority;
    }

    public class NodeGraphPanChangeEvent : NodeGraphEvent<NodeGraphPanChangeEvent> {
        public float2 Pan;
    }

    public class NodeGraphZoomChangeEvent : NodeGraphEvent<NodeGraphZoomChangeEvent> {
        public float Zoom;
    }
}
