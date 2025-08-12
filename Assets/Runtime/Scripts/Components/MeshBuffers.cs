using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace KexEdit {
    public class MeshBuffers : IDisposable {
        public ComputeBuffer PointsBuffer;
        public ComputeBuffer VisualizationDataBuffer;
        public List<DuplicationMeshBuffers> DuplicationBuffers = new();
        public List<ExtrusionMeshBuffers> ExtrusionBuffers = new();
        public List<CapMeshBuffers> StartCapBuffers = new();
        public List<CapMeshBuffers> EndCapBuffers = new();
        public List<DuplicationGizmoBuffers> DuplicationGizmoBuffers = new();
        public List<ExtrusionGizmoBuffers> ExtrusionGizmoBuffers = new();
        public int Count;

        public MeshBuffers(int count) {
            Count = count;

            PointsBuffer = new ComputeBuffer(count, Marshal.SizeOf<TrackPoint>());
            VisualizationDataBuffer = new ComputeBuffer(count, sizeof(float) * 4);
        }

        public void AddDuplicationMesh(DuplicationMeshSettings settings, Mesh mesh, Material material) {
            var buffer = new DuplicationMeshBuffers(this, settings, mesh, material);
            DuplicationBuffers.Add(buffer);
        }

        public void AddExtrusionMesh(Mesh mesh, Material material) {
            var buffer = new ExtrusionMeshBuffers(this, mesh, material);
            ExtrusionBuffers.Add(buffer);
        }

        public void AddStartCapMesh(Mesh mesh, Material material) {
            var buffer = new CapMeshBuffers(this, mesh, material, 1);
            StartCapBuffers.Add(buffer);
        }

        public void AddEndCapMesh(Mesh mesh, Material material) {
            var buffer = new CapMeshBuffers(this, mesh, material, 1);
            EndCapBuffers.Add(buffer);
        }

        public void AddDuplicationGizmo(DuplicationGizmoSettings settings) {
            var buffer = new DuplicationGizmoBuffers(this, settings);
            DuplicationGizmoBuffers.Add(buffer);
        }

        public void AddExtrusionGizmo(ExtrusionGizmoSettings settings) {
            var buffer = new ExtrusionGizmoBuffers(this, settings);
            ExtrusionGizmoBuffers.Add(buffer);
        }

        public void Dispose() {
            PointsBuffer?.Dispose();
            VisualizationDataBuffer?.Dispose();

            foreach (var buffer in DuplicationBuffers) {
                buffer?.Dispose();
            }
            DuplicationBuffers.Clear();

            foreach (var buffer in ExtrusionBuffers) {
                buffer?.Dispose();
            }
            ExtrusionBuffers.Clear();

            foreach (var buffer in StartCapBuffers) {
                buffer?.Dispose();
            }
            StartCapBuffers.Clear();

            foreach (var buffer in EndCapBuffers) {
                buffer?.Dispose();
            }
            EndCapBuffers.Clear();

            foreach (var buffer in DuplicationGizmoBuffers) {
                buffer?.Dispose();
            }
            DuplicationGizmoBuffers.Clear();

            foreach (var buffer in ExtrusionGizmoBuffers) {
                buffer?.Dispose();
            }
            ExtrusionGizmoBuffers.Clear();
        }
    }
}
