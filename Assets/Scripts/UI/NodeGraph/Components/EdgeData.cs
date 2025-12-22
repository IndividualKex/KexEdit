using KexEdit.Legacy;
using Unity.Entities;

namespace KexEdit.UI.NodeGraph {
    public class EdgeData {
        public Entity Entity;
        public Entity Source;
        public Entity Target;
        public InteractionState InteractionState;

        public bool Hovered => InteractionState.HasFlag(InteractionState.Hovered);
        public bool Selected => InteractionState.HasFlag(InteractionState.Selected);

        public static EdgeData Create(Entity connectionEntity, in Connection connection) {
            InteractionState interactionState = InteractionState.None;
            if (connection.Selected) interactionState |= InteractionState.Selected;

            return new EdgeData {
                Entity = connectionEntity,
                Source = connection.Source,
                Target = connection.Target,
                InteractionState = interactionState
            };
        }

        public void Update(in Connection connection) {
            if (connection.Selected) InteractionState |= InteractionState.Selected;
            else InteractionState &= ~InteractionState.Selected;
        }
    }
}
