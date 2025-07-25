using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackStyleRenderSystem : SystemBase {
        private Bounds _bounds;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<GizmoSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var gizmoSettings = SystemAPI.ManagedAPI.GetSingleton<GizmoSettings>();

            foreach (var (style, segment, section, render, entity) in SystemAPI
                .Query<TrackStyle, Segment, SectionReference, Render>()
                .WithAll<TrackPoint>()
                .WithEntityAccess()
            ) {
                if (!render) continue;

                var points = SystemAPI.GetBuffer<TrackPoint>(entity);
                if (points.Length == 0) continue;

                style.CurrentBuffers ??= new MeshBuffers(
                    1,
                    style.DuplicationMeshes,
                    style.ExtrusionMeshes,
                    style.StartCapMeshes,
                    style.EndCapMeshes,
                    gizmoSettings.DuplicationGizmos,
                    gizmoSettings.ExtrusionGizmos
                );

                if (style.ComputeFence == null) {
                    Build(globalSettings, style, gizmoSettings, entity);
                }

                if (style.ComputeFence != null && style.ComputeFence.Value.done) {
                    if (style.NextBuffers != null) {
                        (style.CurrentBuffers, style.NextBuffers) = (style.NextBuffers, style.CurrentBuffers);
                    }
                    style.ComputeFence = null;
                }

                if (style.CurrentBuffers.Count <= 1 ||
                    !SystemAPI.HasComponent<RenderedStyleHash>(section) ||
                    segment.StyleHash != SystemAPI.GetComponent<RenderedStyleHash>(section)) continue;

                foreach (var buffer in style.CurrentBuffers.DuplicationBuffers) {
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

                foreach (var buffer in style.CurrentBuffers.ExtrusionBuffers) {
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


                foreach (var buffer in style.CurrentBuffers.StartCapBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Mesh,
                        buffer.CapBuffer
                    );
                }

                foreach (var buffer in style.CurrentBuffers.EndCapBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Mesh,
                        buffer.CapBuffer
                    );
                }

                if (SystemAPI.HasSingleton<Gizmos>() &&
                    SystemAPI.GetSingleton<Gizmos>().DrawGizmos) {
                    foreach (var buffer in style.CurrentBuffers.DuplicationGizmoBuffers) {
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

                    foreach (var buffer in style.CurrentBuffers.ExtrusionGizmoBuffers) {
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
        }

        private void Build(
            GlobalSettings globalSettings,
            TrackStyle style,
            GizmoSettings gizmoSettings,
            Entity entity
        ) {
            var points = SystemAPI.GetBuffer<TrackPoint>(entity);

            float selected = 0f;
            if (SystemAPI.HasComponent<SelectedBlend>(entity)) {
                selected = SystemAPI.GetComponent<SelectedBlend>(entity).Value;
            }

            if (points.Length == 0) return;

            if (style.NextBuffers == null || style.NextBuffers.PointsBuffer.count != points.Length) {
                style.NextBuffers?.Dispose();
                style.NextBuffers = new MeshBuffers(
                    points.Length,
                    style.DuplicationMeshes,
                    style.ExtrusionMeshes,
                    style.StartCapMeshes,
                    style.EndCapMeshes,
                    gizmoSettings.DuplicationGizmos,
                    gizmoSettings.ExtrusionGizmos
                );
            }

            style.NextBuffers.PointsBuffer.SetData(points.AsNativeArray(), 0, 0, points.Length);

            var compute = globalSettings.Compute;

            // Visualization
            int visualizationKernel = compute.FindKernel("VisualizationKernel");

            compute.SetBuffer(visualizationKernel, "_Points", style.NextBuffers.PointsBuffer);
            compute.SetFloat("_Count", points.Length);

            compute.SetBuffer(visualizationKernel, "_VisualizationData", style.NextBuffers.VisualizationDataBuffer);
            compute.SetFloat("_Selected", selected);

            compute.GetKernelThreadGroupSizes(visualizationKernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

            compute.Dispatch(visualizationKernel, threadGroups, 1, 1);

            // Duplication
            int duplicationKernel = compute.FindKernel("DuplicationKernel");

            for (int i = 0; i < style.NextBuffers.DuplicationBuffers.Count; i++) {
                compute.SetBuffer(duplicationKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(duplicationKernel, "_Matrices", style.NextBuffers.DuplicationBuffers[i].MatricesBuffer);
                compute.SetFloat("_Step", style.NextBuffers.DuplicationBuffers[i].Settings.Step);
                compute.SetFloat("_Offset", style.NextBuffers.DuplicationBuffers[i].Settings.Offset);

                compute.GetKernelThreadGroupSizes(duplicationKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationKernel, threadGroups, 1, 1);
            }

            // Extrusion
            int extrusionKernel = compute.FindKernel("ExtrusionKernel");

            for (int i = 0; i < style.NextBuffers.ExtrusionBuffers.Count; i++) {
                compute.SetBuffer(extrusionKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionVertices",
                    style.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionNormals",
                    style.NextBuffers.ExtrusionBuffers[i].CrossSectionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_CrossSectionTriangulation",
                    style.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer
                );

                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVertices",
                    style.NextBuffers.ExtrusionBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionNormals",
                    style.NextBuffers.ExtrusionBuffers[i].ExtrusionNormalsBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionIndices",
                    style.NextBuffers.ExtrusionBuffers[i].ExtrusionIndicesBuffer
                );
                compute.SetBuffer(
                    extrusionKernel,
                    "_ExtrusionVisualizationIndices",
                    style.NextBuffers.ExtrusionBuffers[i].VisualizationIndicesBuffer
                );

                compute.SetFloat("_CrossSectionVerticesLength", style.NextBuffers.ExtrusionBuffers[i].CrossSectionVerticesBuffer.count);
                compute.SetFloat("_CrossSectionTriangulationLength", style.NextBuffers.ExtrusionBuffers[i].CrossSectionTriangulationBuffer.count);

                compute.GetKernelThreadGroupSizes(extrusionKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionKernel, threadGroups, 1, 1);
            }

            // Duplication Gizmos
            int duplicationGizmoKernel = compute.FindKernel("DuplicationGizmoKernel");

            for (int i = 0; i < style.NextBuffers.DuplicationGizmoBuffers.Count; i++) {
                compute.SetBuffer(duplicationGizmoKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    duplicationGizmoKernel,
                    "_DuplicationGizmoVertices",
                    style.NextBuffers.DuplicationGizmoBuffers[i].DuplicationVerticesBuffer
                );
                compute.SetFloat("_DuplicationGizmoStartHeart", style.NextBuffers.DuplicationGizmoBuffers[i].Settings.StartHeart);
                compute.SetFloat("_DuplicationGizmoEndHeart", style.NextBuffers.DuplicationGizmoBuffers[i].Settings.EndHeart);

                compute.GetKernelThreadGroupSizes(duplicationGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationGizmoKernel, threadGroups, 1, 1);
            }

            // Extrusion Gizmos
            int extrusionGizmoKernel = compute.FindKernel("ExtrusionGizmoKernel");

            for (int i = 0; i < style.NextBuffers.ExtrusionGizmoBuffers.Count; i++) {
                compute.SetBuffer(extrusionGizmoKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    extrusionGizmoKernel,
                    "_ExtrusionGizmoVertices",
                    style.NextBuffers.ExtrusionGizmoBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetFloat("_ExtrusionGizmoHeart", style.NextBuffers.ExtrusionGizmoBuffers[i].Settings.Heart);

                compute.GetKernelThreadGroupSizes(extrusionGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionGizmoKernel, threadGroups, 1, 1);
            }

            // Start Caps
            int startCapKernel = compute.FindKernel("StartCapKernel");

            for (int i = 0; i < style.NextBuffers.StartCapBuffers.Count; i++) {
                compute.SetBuffer(startCapKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(startCapKernel, "_Matrices", style.NextBuffers.StartCapBuffers[i].MatricesBuffer);

                compute.GetKernelThreadGroupSizes(startCapKernel, out threadGroupSize, out _, out _);
                threadGroups = 1;

                compute.Dispatch(startCapKernel, threadGroups, 1, 1);
            }

            // End Caps
            int endCapKernel = compute.FindKernel("EndCapKernel");

            for (int i = 0; i < style.NextBuffers.EndCapBuffers.Count; i++) {
                compute.SetBuffer(endCapKernel, "_Points", style.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(endCapKernel, "_Matrices", style.NextBuffers.EndCapBuffers[i].MatricesBuffer);

                compute.GetKernelThreadGroupSizes(endCapKernel, out threadGroupSize, out _, out _);
                threadGroups = 1;

                compute.Dispatch(endCapKernel, threadGroups, 1, 1);
            }

            style.ComputeFence = AsyncGPUReadback.Request(style.NextBuffers.PointsBuffer);
        }
    }
}
