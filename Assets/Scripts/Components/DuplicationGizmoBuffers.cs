using UnityEngine;
using System;

namespace KexEdit {
    public class DuplicationGizmoBuffers : IDisposable {
        public DuplicationGizmoSettings Settings;
        public ComputeBuffer DuplicationVerticesBuffer;
        public MaterialPropertyBlock MatProps;

        public DuplicationGizmoBuffers(
            MeshBuffers meshBuffers,
            DuplicationGizmoSettings settings
        ) {
            Settings = settings;

            DuplicationVerticesBuffer = new ComputeBuffer(meshBuffers.Count * 2, sizeof(float) * 3);

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Vertices", DuplicationVerticesBuffer);
        }

        public void Dispose() {
            DuplicationVerticesBuffer?.Dispose();
        }
    }
}
