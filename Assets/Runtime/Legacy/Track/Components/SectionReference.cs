using Unity.Entities;

namespace KexEdit.Legacy {
    public struct SectionReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(SectionReference reference) => reference.Value;
        public static implicit operator SectionReference(Entity entity) => new() { Value = entity };
    }
}