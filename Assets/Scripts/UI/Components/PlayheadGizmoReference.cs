using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public struct PlayheadGizmoReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(PlayheadGizmoReference reference) => reference.Value;
        public static implicit operator PlayheadGizmoReference(Entity entity) => new() { Value = entity };
    }
}
