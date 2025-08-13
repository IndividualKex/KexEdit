using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit {
    public class MeshBuffers : IDisposable {
        public ComputeBuffer PointsBuffer;
        public ComputeBuffer VisualizationDataBuffer;
        public ComputeBuffer SegmentBoundariesBuffer;
        public ComputeBuffer SegmentSelectionsBuffer;
        public List<DuplicationMeshBuffers> DuplicationBuffers = new();
        public List<ExtrusionMeshBuffers> ExtrusionBuffers = new();
        public List<CapMeshBuffers> StartCapBuffers = new();
        public List<CapMeshBuffers> EndCapBuffers = new();
        public List<ExtrusionGizmoBuffers> ExtrusionGizmoBuffers = new();
        public int Count;
        public int SegmentCount;

        public MeshBuffers(
            List<ExtrusionMeshSettingsData> extrusionMeshes,
            List<DuplicationMeshSettingsData> duplicationMeshes,
            List<CapMeshSettingsData> startCapMeshes,
            List<CapMeshSettingsData> endCapMeshes,
            List<ExtrusionGizmoSettings> extrusionGizmos
        ) {
            foreach (var mesh in extrusionMeshes) {
                var buffer = new ExtrusionMeshBuffers(mesh.Mesh, mesh.Material);
                ExtrusionBuffers.Add(buffer);
            }
            foreach (var mesh in duplicationMeshes) {
                var buffer = new DuplicationMeshBuffers(
                    mesh.Mesh,
                    mesh.Material,
                    mesh.Step,
                    mesh.Offset);
                DuplicationBuffers.Add(buffer);
            }
            foreach (var mesh in startCapMeshes) {
                var buffer = new CapMeshBuffers(mesh.Mesh, mesh.Material);
                StartCapBuffers.Add(buffer);
            }
            foreach (var mesh in endCapMeshes) {
                var buffer = new CapMeshBuffers(mesh.Mesh, mesh.Material);
                EndCapBuffers.Add(buffer);
            }
            foreach (var gizmo in extrusionGizmos) {
                var buffer = new ExtrusionGizmoBuffers(gizmo.Material, gizmo.Heart);
                ExtrusionGizmoBuffers.Add(buffer);
            }

            var defaultSegment = new NativeArray<int2>(1, Allocator.Temp);
            defaultSegment[0] = new int2(0, 0);
            Initialize(1, defaultSegment);
            defaultSegment.Dispose();
        }

        public void Initialize(int pointCount, NativeArray<int2> segmentBoundaries) {
            PointsBuffer?.Dispose();
            VisualizationDataBuffer?.Dispose();
            SegmentBoundariesBuffer?.Dispose();
            SegmentSelectionsBuffer?.Dispose();

            Count = pointCount;
            SegmentCount = segmentBoundaries.Length;
            PointsBuffer = new ComputeBuffer(pointCount, Marshal.SizeOf<TrackPoint>());
            VisualizationDataBuffer = new ComputeBuffer(pointCount, sizeof(float) * 4);
            SegmentBoundariesBuffer = new ComputeBuffer(SegmentCount, sizeof(int) * 2);
            SegmentSelectionsBuffer = new ComputeBuffer(SegmentCount, sizeof(float));

            foreach (var buffer in DuplicationBuffers) {
                buffer?.Initialize(pointCount, segmentBoundaries, VisualizationDataBuffer);
            }
            foreach (var buffer in ExtrusionBuffers) {
                buffer?.Initialize(pointCount, VisualizationDataBuffer);
            }
            foreach (var buffer in StartCapBuffers) {
                buffer?.Initialize(SegmentCount, VisualizationDataBuffer);
            }
            foreach (var buffer in EndCapBuffers) {
                buffer?.Initialize(SegmentCount, VisualizationDataBuffer);
            }
            foreach (var buffer in ExtrusionGizmoBuffers) {
                buffer?.Initialize(pointCount);
            }
        }

        public void Dispose() {
            PointsBuffer?.Dispose();
            VisualizationDataBuffer?.Dispose();
            SegmentBoundariesBuffer?.Dispose();
            SegmentSelectionsBuffer?.Dispose();

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

            foreach (var buffer in ExtrusionGizmoBuffers) {
                buffer?.Dispose();
            }
            ExtrusionGizmoBuffers.Clear();
        }
    }
}
