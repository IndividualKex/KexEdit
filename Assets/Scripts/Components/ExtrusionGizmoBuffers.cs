using UnityEngine;
using System;

namespace KexEdit {
    public class ExtrusionGizmoBuffers : IDisposable {
        public ExtrusionGizmoSettings Settings;
        public ComputeBuffer ExtrusionVerticesBuffer;
        public MaterialPropertyBlock MatProps;

        public ExtrusionGizmoBuffers(
            MeshBuffers meshBuffers,
            ExtrusionGizmoSettings settings
        ) {
            Settings = settings;

            ExtrusionVerticesBuffer = new ComputeBuffer(meshBuffers.Count, sizeof(float) * 3);

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Vertices", ExtrusionVerticesBuffer);
        }

        public void Dispose() {
            ExtrusionVerticesBuffer?.Dispose();
        }
    }
}
