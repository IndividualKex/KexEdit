using UnityEngine.Rendering;
using Unity.Entities;
using System;

namespace KexEdit {
    public class MeshData : IComponentData, IDisposable {
        public Entity Entity;

        public AsyncGPUReadbackRequest? ComputeFence;
        public MeshBuffers CurrentBuffers;
        public MeshBuffers NextBuffers;

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
