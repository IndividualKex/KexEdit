using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(TrackStyleRenderSystem))]
    public partial class TrackStyleBuildSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<GizmoSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var gizmoSettings = SystemAPI.ManagedAPI.GetSingleton<GizmoSettings>();

            foreach (var (buffers, styleEntity) in SystemAPI.Query<TrackStyleBuffers>().WithEntityAccess()) {
                using var points = new NativeList<TrackPoint>(0, Allocator.Temp);
                using var segmentBoundaries = new NativeList<int2>(0, Allocator.Temp);
                using var segmentSelections = new NativeList<float>(0, Allocator.Temp);

                foreach (var (segment, segmentEntity) in SystemAPI.Query<Segment>().WithAll<TrackPoint>().WithEntityAccess()) {
                    if (segment.Style != styleEntity) continue;

                    var trackPoints = SystemAPI.GetBuffer<TrackPoint>(segmentEntity);
                    if (trackPoints.Length > 1) {
                        int startIndex = points.Length;
                        points.AddRange(trackPoints.AsNativeArray());
                        int endIndex = points.Length - 1;
                        segmentBoundaries.Add(new int2(startIndex, endIndex));

                        float selectionValue = SystemAPI.HasComponent<SelectedBlend>(segmentEntity)
                            ? SystemAPI.GetComponent<SelectedBlend>(segmentEntity)
                            : 0f;
                        segmentSelections.Add(selectionValue);
                    }
                }

                if (points.Length == 0 || segmentBoundaries.Length == 0) continue;

                if (buffers.ComputeFence == null) {
                    Build(globalSettings, gizmoSettings, points.AsArray(), segmentBoundaries.AsArray(), segmentSelections.AsArray(), buffers);
                }

                if (buffers.ComputeFence != null && buffers.ComputeFence.Value.done) {
                    (buffers.CurrentBuffers, buffers.NextBuffers) = (buffers.NextBuffers, buffers.CurrentBuffers);
                    buffers.ComputeFence = null;
                }
            }
        }

        private void Build(
            GlobalSettings globalSettings,
            GizmoSettings gizmoSettings,
            NativeArray<TrackPoint> points,
            NativeArray<int2> segmentBoundaries,
            NativeArray<float> segmentSelections,
            TrackStyleBuffers buffers
        ) {
            if (points.Length == 0) return;

            if (buffers.NextBuffers.PointsBuffer.count != points.Length ||
                buffers.NextBuffers.SegmentCount != segmentBoundaries.Length) {
                buffers.NextBuffers.Initialize(points.Length, segmentBoundaries);
            }

            buffers.NextBuffers.PointsBuffer.SetData(points, 0, 0, points.Length);
            buffers.NextBuffers.SegmentBoundariesBuffer.SetData(segmentBoundaries, 0, 0, segmentBoundaries.Length);
            buffers.NextBuffers.SegmentSelectionsBuffer.SetData(segmentSelections, 0, 0, segmentSelections.Length);

            var compute = globalSettings.Compute;

            int visualizationKernel = compute.FindKernel("VisualizationKernel");

            compute.SetBuffer(visualizationKernel, "_Points", buffers.NextBuffers.PointsBuffer);
            compute.SetFloat("_Count", points.Length);
            compute.SetBuffer(visualizationKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
            compute.SetFloat("_SegmentCount", segmentBoundaries.Length);
            compute.SetBuffer(visualizationKernel, "_SegmentSelections", buffers.NextBuffers.SegmentSelectionsBuffer);

            compute.SetBuffer(visualizationKernel, "_VisualizationData", buffers.NextBuffers.VisualizationDataBuffer);

            compute.GetKernelThreadGroupSizes(visualizationKernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

            compute.Dispatch(visualizationKernel, threadGroups, 1, 1);

            int duplicationKernel = compute.FindKernel("DuplicationKernel");

            for (int i = 0; i < buffers.NextBuffers.DuplicationBuffers.Count; i++) {
                compute.SetBuffer(duplicationKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);
                compute.SetBuffer(duplicationKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
                compute.SetFloat("_SegmentCount", segmentBoundaries.Length);

                compute.SetBuffer(duplicationKernel, "_Matrices", buffers.NextBuffers.DuplicationBuffers[i].MatricesBuffer);
                compute.SetBuffer(duplicationKernel, "_DuplicationVisualizationIndices", buffers.NextBuffers.DuplicationBuffers[i].VisualizationIndicesBuffer);
                compute.SetFloat("_Step", buffers.NextBuffers.DuplicationBuffers[i].Step);
                compute.SetFloat("_Offset", buffers.NextBuffers.DuplicationBuffers[i].Offset);

                compute.GetKernelThreadGroupSizes(duplicationKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationKernel, threadGroups, 1, 1);
            }

            int extrusionKernel = compute.FindKernel("ExtrusionKernel");

            for (int i = 0; i < buffers.NextBuffers.ExtrusionBuffers.Count; i++) {
                compute.SetBuffer(extrusionKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);
                compute.SetBuffer(extrusionKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
                compute.SetFloat("_SegmentCount", segmentBoundaries.Length);

                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionVertices",
                    buffers.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionNormals",
                    buffers.NextBuffers.ExtrusionBuffers[i].CrossSectionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionTriangulation",
                    buffers.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer
                );

                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVertices",
                    buffers.NextBuffers.ExtrusionBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionNormals",
                    buffers.NextBuffers.ExtrusionBuffers[i].ExtrusionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionIndices",
                    buffers.NextBuffers.ExtrusionBuffers[i].ExtrusionIndicesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVisualizationIndices",
                    buffers.NextBuffers.ExtrusionBuffers[i].VisualizationIndicesBuffer
                );

                compute.SetFloat("_CrossSectionVerticesLength", buffers.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer.count);
                compute.SetFloat("_CrossSectionTriangulationLength", buffers.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer.count);

                compute.GetKernelThreadGroupSizes(extrusionKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionKernel, threadGroups, 1, 1);
            }

            int extrusionGizmoKernel = compute.FindKernel("ExtrusionGizmoKernel");

            for (int i = 0; i < buffers.NextBuffers.ExtrusionGizmoBuffers.Count; i++) {
                compute.SetBuffer(extrusionGizmoKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);
                compute.SetBuffer(extrusionGizmoKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
                compute.SetFloat("_SegmentCount", segmentBoundaries.Length);

                compute.SetBuffer(
                    extrusionGizmoKernel,
                    "_ExtrusionGizmoVertices",
                    buffers.NextBuffers.ExtrusionGizmoBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetFloat("_ExtrusionGizmoHeart", buffers.NextBuffers.ExtrusionGizmoBuffers[i].Heart);

                compute.GetKernelThreadGroupSizes(extrusionGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionGizmoKernel, threadGroups, 1, 1);
            }

            int startCapKernel = compute.FindKernel("StartCapKernel");

            for (int i = 0; i < buffers.NextBuffers.StartCapBuffers.Count; i++) {
                compute.SetBuffer(startCapKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);
                compute.SetBuffer(startCapKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
                compute.SetFloat("_SegmentCount", segmentBoundaries.Length);

                compute.SetBuffer(startCapKernel, "_Matrices", buffers.NextBuffers.StartCapBuffers[i].MatricesBuffer);
                compute.SetBuffer(startCapKernel, "_CapVisualizationIndices", buffers.NextBuffers.StartCapBuffers[i].VisualizationIndicesBuffer);

                compute.GetKernelThreadGroupSizes(startCapKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(segmentBoundaries.Length / (float)threadGroupSize);

                compute.Dispatch(startCapKernel, threadGroups, 1, 1);
            }

            int endCapKernel = compute.FindKernel("EndCapKernel");

            for (int i = 0; i < buffers.NextBuffers.EndCapBuffers.Count; i++) {
                compute.SetBuffer(endCapKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);
                compute.SetBuffer(endCapKernel, "_SegmentBoundaries", buffers.NextBuffers.SegmentBoundariesBuffer);
                compute.SetFloat("_SegmentCount", segmentBoundaries.Length);

                compute.SetBuffer(endCapKernel, "_Matrices", buffers.NextBuffers.EndCapBuffers[i].MatricesBuffer);
                compute.SetBuffer(endCapKernel, "_CapVisualizationIndices", buffers.NextBuffers.EndCapBuffers[i].VisualizationIndicesBuffer);

                compute.GetKernelThreadGroupSizes(endCapKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(segmentBoundaries.Length / (float)threadGroupSize);

                compute.Dispatch(endCapKernel, threadGroups, 1, 1);
            }

            buffers.ComputeFence = AsyncGPUReadback.Request(buffers.NextBuffers.PointsBuffer);
        }
    }
}
