using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MeshSystem : SystemBase {
        private Bounds _bounds;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            RequireForUpdate<GlobalMeshData>();
            RequireForUpdate<MeshData>();
        }

        protected override void OnUpdate() {
            var globalData = SystemAPI.ManagedAPI.GetSingleton<GlobalMeshData>();

            foreach (var data in SystemAPI.Query<MeshData>()) {
                if (!SystemAPI.HasBuffer<TrackPoint>(data.Entity)) continue;

                var points = SystemAPI.GetBuffer<TrackPoint>(data.Entity);
                if (points.Length == 0) continue;

                data.CurrentBuffers ??= new MeshBuffers(
                    1,
                    globalData.DuplicationMeshes,
                    globalData.ExtrusionMeshes,
                    globalData.DuplicationGizmos,
                    globalData.ExtrusionGizmos
                );

                if (data.ComputeFence == null) {
                    Build(globalData, data);
                }

                if (data.ComputeFence != null && data.ComputeFence.Value.done) {
                    if (data.NextBuffers != null) {
                        (data.CurrentBuffers, data.NextBuffers) = (data.NextBuffers, data.CurrentBuffers);
                    }
                    data.ComputeFence = null;
                }

                foreach (var buffer in data.CurrentBuffers.DuplicationBuffers) {
                    var rp = new RenderParams(buffer.Settings.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Settings.Mesh,
                        buffer.DuplicationBuffer
                    );
                }

                foreach (var buffer in data.CurrentBuffers.ExtrusionBuffers) {
                    var rp = new RenderParams(buffer.Settings.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderPrimitives(
                        rp,
                        MeshTopology.Triangles,
                        buffer.ExtrusionIndicesBuffer.count
                    );
                }

                foreach (var buffer in data.CurrentBuffers.DuplicationGizmoBuffers) {
                    var rp = new RenderParams(buffer.Settings.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderPrimitives(
                        rp,
                        MeshTopology.Lines,
                        buffer.DuplicationVerticesBuffer.count
                    );
                }

                foreach (var buffer in data.CurrentBuffers.ExtrusionGizmoBuffers) {
                    var rp = new RenderParams(buffer.Settings.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderPrimitives(
                        rp,
                        MeshTopology.LineStrip,
                        buffer.ExtrusionVerticesBuffer.count
                    );
                }
            }
        }

        private void Build(GlobalMeshData globalData, MeshData data) {
            var points = SystemAPI.GetBuffer<TrackPoint>(data.Entity);

            float selected = 0f;
            if (SystemAPI.HasComponent<SelectedBlend>(data.Entity)) {
                selected = SystemAPI.GetComponent<SelectedBlend>(data.Entity).Value;
            }

            if (points.Length == 0) return;

            if (data.NextBuffers == null || data.NextBuffers.PointsBuffer.count != points.Length) {
                data.NextBuffers?.Dispose();
                data.NextBuffers = new MeshBuffers(
                    points.Length,
                    globalData.DuplicationMeshes,
                    globalData.ExtrusionMeshes,
                    globalData.DuplicationGizmos,
                    globalData.ExtrusionGizmos
                );
            }

            data.NextBuffers.PointsBuffer.SetData(points.AsNativeArray(), 0, 0, points.Length);

            var compute = globalData.Compute;

            // Visualization
            int visualizationKernel = compute.FindKernel("VisualizationKernel");

            compute.SetBuffer(visualizationKernel, "_Points", data.NextBuffers.PointsBuffer);
            compute.SetFloat("_Count", points.Length);

            compute.SetBuffer(visualizationKernel, "_VisualizationData", data.NextBuffers.VisualizationDataBuffer);
            compute.SetFloat("_Selected", selected);

            compute.GetKernelThreadGroupSizes(visualizationKernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

            compute.Dispatch(visualizationKernel, threadGroups, 1, 1);

            // Duplication
            int duplicationKernel = compute.FindKernel("DuplicationKernel");

            for (int i = 0; i < data.NextBuffers.DuplicationBuffers.Count; i++) {
                compute.SetBuffer(duplicationKernel, "_Points", data.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(duplicationKernel, "_Matrices", data.NextBuffers.DuplicationBuffers[i].MatricesBuffer);
                compute.SetBuffer(duplicationKernel, "_DuplicationVisualizationIndices", data.NextBuffers.DuplicationBuffers[i].VisualizationIndicesBuffer);
                compute.SetFloat("_Step", data.NextBuffers.DuplicationBuffers[i].Settings.Step);

                compute.GetKernelThreadGroupSizes(duplicationKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationKernel, threadGroups, 1, 1);
            }

            // Extrusion
            int extrusionKernel = compute.FindKernel("ExtrusionKernel");

            for (int i = 0; i < data.NextBuffers.ExtrusionBuffers.Count; i++) {
                compute.SetBuffer(extrusionKernel, "_Points", data.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionVertices",
                    data.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionNormals",
                    data.NextBuffers.ExtrusionBuffers[i].CrossSectionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionTriangulation",
                    data.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer
                );

                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVertices",
                    data.NextBuffers.ExtrusionBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionNormals",
                    data.NextBuffers.ExtrusionBuffers[i].ExtrusionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionIndices",
                    data.NextBuffers.ExtrusionBuffers[i].ExtrusionIndicesBuffer
                );

                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVisualizationIndices",
                    data.NextBuffers.ExtrusionBuffers[i].VisualizationIndicesBuffer
                );

                compute.SetFloat("_CrossSectionVerticesLength", data.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer.count);
                compute.SetFloat("_CrossSectionTriangulationLength", data.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer.count);

                compute.GetKernelThreadGroupSizes(extrusionKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionKernel, threadGroups, 1, 1);
            }

            // Duplication Gizmos
            int duplicationGizmoKernel = compute.FindKernel("DuplicationGizmoKernel");

            for (int i = 0; i < data.NextBuffers.DuplicationGizmoBuffers.Count; i++) {
                compute.SetBuffer(duplicationGizmoKernel, "_Points", data.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    duplicationGizmoKernel,
                    "_DuplicationGizmoVertices",
                    data.NextBuffers.DuplicationGizmoBuffers[i].DuplicationVerticesBuffer
                );
                compute.SetFloat("_DuplicationGizmoStartHeart", data.NextBuffers.DuplicationGizmoBuffers[i].Settings.StartHeart);
                compute.SetFloat("_DuplicationGizmoEndHeart", data.NextBuffers.DuplicationGizmoBuffers[i].Settings.EndHeart);

                compute.GetKernelThreadGroupSizes(duplicationGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationGizmoKernel, threadGroups, 1, 1);
            }

            // Extrusion Gizmos
            int extrusionGizmoKernel = compute.FindKernel("ExtrusionGizmoKernel");

            for (int i = 0; i < data.NextBuffers.ExtrusionGizmoBuffers.Count; i++) {
                compute.SetBuffer(extrusionGizmoKernel, "_Points", data.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    extrusionGizmoKernel,
                    "_ExtrusionGizmoVertices",
                    data.NextBuffers.ExtrusionGizmoBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetFloat("_ExtrusionGizmoHeart", data.NextBuffers.ExtrusionGizmoBuffers[i].Settings.Heart);

                compute.GetKernelThreadGroupSizes(extrusionGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionGizmoKernel, threadGroups, 1, 1);
            }

            data.ComputeFence = AsyncGPUReadback.Request(data.NextBuffers.PointsBuffer);
        }
    }
}
