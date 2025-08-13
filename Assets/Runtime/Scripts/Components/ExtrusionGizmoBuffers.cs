using UnityEngine;
using System;

namespace KexEdit {
    public class ExtrusionGizmoBuffers : IDisposable {
        public Material Material;
        public float Heart;

        public ComputeBuffer ExtrusionVerticesBuffer;
        public MaterialPropertyBlock MatProps;

        public ExtrusionGizmoBuffers(Material material, float heart) {
            Material = material;
            Heart = heart;

            MatProps = new MaterialPropertyBlock();
        }

        public void Initialize(int count) {
            ExtrusionVerticesBuffer?.Dispose();
            ExtrusionVerticesBuffer = new ComputeBuffer(count * 2, sizeof(float) * 3);
            MatProps.SetBuffer("_Vertices", ExtrusionVerticesBuffer);
        }

        public void Dispose() {
            ExtrusionVerticesBuffer?.Dispose();
        }
    }
}
