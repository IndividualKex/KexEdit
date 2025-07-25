using Unity.Entities;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

namespace KexEdit {
    public class TrackStyle : IComponentData, IDisposable {
        public List<DuplicationMeshSettings> DuplicationMeshes = new();
        public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
        public List<CapMeshSettings> StartCapMeshes = new();
        public List<CapMeshSettings> EndCapMeshes = new();
        public float Spacing;
        public float Threshold;
        public int Version;

        public AsyncGPUReadbackRequest? ComputeFence;
        public MeshBuffers CurrentBuffers;
        public MeshBuffers NextBuffers;

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
