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
        public List<DuplicationGizmoBuffers> DuplicationGizmoBuffers = new();
        public List<ExtrusionGizmoBuffers> ExtrusionGizmoBuffers = new();
        public int Count;

        public MeshBuffers(
            int count,
            List<DuplicationMeshSettings> duplicationMeshes,
            List<ExtrusionMeshSettings> extrusionMeshes,
            List<DuplicationGizmoSettings> duplicationGizmos,
            List<ExtrusionGizmoSettings> extrusionGizmos
        ) {
            Count = count;

            PointsBuffer = new ComputeBuffer(count, Marshal.SizeOf<TrackPoint>());
            VisualizationDataBuffer = new ComputeBuffer(count, sizeof(float) * 4);

            foreach (var settings in duplicationMeshes) {
                var buffer = new DuplicationMeshBuffers(this, settings);
                DuplicationBuffers.Add(buffer);
            }

            foreach (var settings in extrusionMeshes) {
                var buffer = new ExtrusionMeshBuffers(this, settings);
                ExtrusionBuffers.Add(buffer);
            }

            foreach (var settings in duplicationGizmos) {
                var buffer = new DuplicationGizmoBuffers(this, settings);
                DuplicationGizmoBuffers.Add(buffer);
            }

            foreach (var settings in extrusionGizmos) {
                var buffer = new ExtrusionGizmoBuffers(this, settings);
                ExtrusionGizmoBuffers.Add(buffer);
            }
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
