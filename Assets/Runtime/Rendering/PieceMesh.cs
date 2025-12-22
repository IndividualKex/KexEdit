using UnityEngine;

namespace KexEdit.Rendering {
    public readonly struct PieceMesh {
        public readonly Mesh Mesh;
        public readonly float NominalLength;

        public PieceMesh(Mesh mesh, float nominalLength) {
            Mesh = mesh;
            NominalLength = nominalLength;
        }

        public int VertexCount => Mesh.vertexCount;
    }
}
