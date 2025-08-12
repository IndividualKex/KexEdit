using Unity.Entities;
using UnityEngine.Rendering;
using System;

namespace KexEdit {
    public class TrackStyleBuffers : IComponentData, IDisposable {
        public AsyncGPUReadbackRequest? ComputeFence;
        public MeshBuffers CurrentBuffers;
        public MeshBuffers NextBuffers;

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
