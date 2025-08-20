using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.UI.NodeGraph {
    public class NodeData {
        public Entity Entity;
        public NodeType Type;
        public List<Entity> OrderedInputs = new();
        public List<Entity> OrderedOutputs = new();
        public Dictionary<Entity, PortData> Inputs = new();
        public Dictionary<Entity, PortData> Outputs = new();

        public float2 Position;
        public float2 DragStartPosition;
        public InteractionState InteractionState;
        public DurationType DurationType;
        public int Priority;
        public bool Render;
        public bool Steering;

        public bool Hovered => InteractionState.HasFlag(InteractionState.Hovered);
        public bool Selected => InteractionState.HasFlag(InteractionState.Selected);

        public static NodeData Create(NodeAspect node, DurationType durationType, bool render, bool steering = true) {
            InteractionState interactionState = InteractionState.None;
            if (node.Selected) interactionState |= InteractionState.Selected;

            return new NodeData {
                Entity = node.Self,
                Type = node.Type,
                Position = node.Position,
                DragStartPosition = node.Position,
                InteractionState = interactionState,
                DurationType = durationType,
                Priority = node.Priority,
                Render = render,
                Steering = steering,
            };
        }

        public void Update(NodeAspect node, DurationType durationType, bool render, bool steering = true) {
            Position = node.Position;
            if (node.Selected) InteractionState |= InteractionState.Selected;
            else InteractionState &= ~InteractionState.Selected;
            DurationType = durationType;
            Priority = node.Priority;
            Render = render;
            Steering = steering;
        }
    }
}
