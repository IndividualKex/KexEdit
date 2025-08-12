using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class MeshReference : IComponentData {
        public Mesh Value;

        public static implicit operator Mesh(MeshReference reference) => reference.Value;
        public static implicit operator MeshReference(Mesh mesh) => new() { Value = mesh };
    }
}
