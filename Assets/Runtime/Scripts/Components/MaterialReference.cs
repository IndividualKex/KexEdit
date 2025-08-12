using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class MaterialReference : IComponentData {
        public Material Value;

        public static implicit operator Material(MaterialReference reference) => reference.Value;
        public static implicit operator MaterialReference(Material material) => new() { Value = material };
    }
}
