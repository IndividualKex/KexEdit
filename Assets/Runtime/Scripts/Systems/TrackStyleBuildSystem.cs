using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.Entities;

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

            foreach (var (segmentRW, buffers, section, render, entity) in SystemAPI
                .Query<RefRW<Segment>, TrackStyleBuffers, SectionReference, Render>()
                .WithAll<TrackPoint>()
                .WithEntityAccess()
            ) {
                ref var segment = ref segmentRW.ValueRW;
                segment.HasBuffers = false;

                if (!render) continue;

                var points = SystemAPI.GetBuffer<TrackPoint>(entity);
                if (points.Length == 0) continue;

                if (buffers.CurrentBuffers == null) {
                    buffers.CurrentBuffers = new MeshBuffers(1);
                    var duplicationMeshes = SystemAPI.GetBuffer<DuplicationMeshReference>(segment.Style);
                    var extrusionMeshes = SystemAPI.GetBuffer<ExtrusionMeshReference>(segment.Style);
                    var startCapMeshes = SystemAPI.GetBuffer<StartCapMeshReference>(segment.Style);
                    var endCapMeshes = SystemAPI.GetBuffer<EndCapMeshReference>(segment.Style);
                    foreach (var duplicationMesh in duplicationMeshes) {
                        var settings = SystemAPI.GetComponent<DuplicationMeshSettings>(duplicationMesh);
                        var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(duplicationMesh);
                        var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(duplicationMesh);
                        buffers.CurrentBuffers.AddDuplicationMesh(settings, mesh, material);
                    }
                    foreach (var extrusionMesh in extrusionMeshes) {
                        var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(extrusionMesh);
                        var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(extrusionMesh);
                        buffers.CurrentBuffers.AddExtrusionMesh(mesh, material);
                    }
                    foreach (var startCapMesh in startCapMeshes) {
                        var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(startCapMesh);
                        var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(startCapMesh);
                        buffers.CurrentBuffers.AddStartCapMesh(mesh, material);
                    }
                    foreach (var endCapMesh in endCapMeshes) {
                        var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(endCapMesh);
                        var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(endCapMesh);
                        buffers.CurrentBuffers.AddEndCapMesh(mesh, material);
                    }
                    foreach (var duplicationGizmo in gizmoSettings.DuplicationGizmos) {
                        buffers.CurrentBuffers.AddDuplicationGizmo(duplicationGizmo);
                    }
                    foreach (var extrusionGizmo in gizmoSettings.ExtrusionGizmos) {
                        buffers.CurrentBuffers.AddExtrusionGizmo(extrusionGizmo);
                    }
                }

                if (buffers.ComputeFence == null) {
                    Build(globalSettings, gizmoSettings, entity);
                }

                if (buffers.ComputeFence != null && buffers.ComputeFence.Value.done) {
                    if (buffers.NextBuffers != null) {
                        (buffers.CurrentBuffers, buffers.NextBuffers) = (buffers.NextBuffers, buffers.CurrentBuffers);
                    }
                    buffers.ComputeFence = null;
                }

                segment.HasBuffers = buffers.CurrentBuffers.Count > 1;
            }
        }

        private void Build(
            GlobalSettings globalSettings,
            GizmoSettings gizmoSettings,
            Entity entity
        ) {
            var points = SystemAPI.GetBuffer<TrackPoint>(entity);

            float selected = 0f;
            if (SystemAPI.HasComponent<SelectedBlend>(entity)) {
                selected = SystemAPI.GetComponent<SelectedBlend>(entity).Value;
            }

            if (points.Length == 0) return;

            var segment = SystemAPI.GetComponent<Segment>(entity);
            var buffers = SystemAPI.ManagedAPI.GetComponent<TrackStyleBuffers>(entity);

            if (buffers.NextBuffers == null || buffers.NextBuffers.PointsBuffer.count != points.Length) {
                buffers.NextBuffers?.Dispose();

                buffers.NextBuffers = new MeshBuffers(points.Length);

                var duplicationMeshes = SystemAPI.GetBuffer<DuplicationMeshReference>(segment.Style);
                var extrusionMeshes = SystemAPI.GetBuffer<ExtrusionMeshReference>(segment.Style);
                var startCapMeshes = SystemAPI.GetBuffer<StartCapMeshReference>(segment.Style);
                var endCapMeshes = SystemAPI.GetBuffer<EndCapMeshReference>(segment.Style);
                foreach (var duplicationMesh in duplicationMeshes) {
                    var settings = SystemAPI.GetComponent<DuplicationMeshSettings>(duplicationMesh);
                    var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(duplicationMesh);
                    var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(duplicationMesh);
                    buffers.NextBuffers.AddDuplicationMesh(settings, mesh, material);
                }
                foreach (var extrusionMesh in extrusionMeshes) {
                    var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(extrusionMesh);
                    var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(extrusionMesh);
                    buffers.NextBuffers.AddExtrusionMesh(mesh, material);
                }
                foreach (var startCapMesh in startCapMeshes) {
                    var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(startCapMesh);
                    var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(startCapMesh);
                    buffers.NextBuffers.AddStartCapMesh(mesh, material);
                }
                foreach (var endCapMesh in endCapMeshes) {
                    var mesh = SystemAPI.ManagedAPI.GetComponent<MeshReference>(endCapMesh);
                    var material = SystemAPI.ManagedAPI.GetComponent<MaterialReference>(endCapMesh);
                    buffers.NextBuffers.AddEndCapMesh(mesh, material);
                }
                foreach (var duplicationGizmo in gizmoSettings.DuplicationGizmos) {
                    buffers.NextBuffers.AddDuplicationGizmo(duplicationGizmo);
                }
                foreach (var extrusionGizmo in gizmoSettings.ExtrusionGizmos) {
                    buffers.NextBuffers.AddExtrusionGizmo(extrusionGizmo);
                }
            }

            buffers.NextBuffers.PointsBuffer.SetData(points.AsNativeArray(), 0, 0, points.Length);

            var compute = globalSettings.Compute;

            int visualizationKernel = compute.FindKernel("VisualizationKernel");

            compute.SetBuffer(visualizationKernel, "_Points", buffers.NextBuffers.PointsBuffer);
            compute.SetFloat("_Count", points.Length);

            compute.SetBuffer(visualizationKernel, "_VisualizationData", buffers.NextBuffers.VisualizationDataBuffer);
            compute.SetFloat("_Selected", selected);

            compute.GetKernelThreadGroupSizes(visualizationKernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

            compute.Dispatch(visualizationKernel, threadGroups, 1, 1);

            int duplicationKernel = compute.FindKernel("DuplicationKernel");

            for (int i = 0; i < buffers.NextBuffers.DuplicationBuffers.Count; i++) {
                compute.SetBuffer(duplicationKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(duplicationKernel, "_Matrices", buffers.NextBuffers.DuplicationBuffers[i].MatricesBuffer);
                compute.SetFloat("_Step", buffers.NextBuffers.DuplicationBuffers[i].Settings.Step);
                compute.SetFloat("_Offset", buffers.NextBuffers.DuplicationBuffers[i].Settings.Offset);

                compute.GetKernelThreadGroupSizes(duplicationKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationKernel, threadGroups, 1, 1);
            }

            int extrusionKernel = compute.FindKernel("ExtrusionKernel");

            for (int i = 0; i < buffers.NextBuffers.ExtrusionBuffers.Count; i++) {
                compute.SetBuffer(extrusionKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

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

            int duplicationGizmoKernel = compute.FindKernel("DuplicationGizmoKernel");

            for (int i = 0; i < buffers.NextBuffers.DuplicationGizmoBuffers.Count; i++) {
                compute.SetBuffer(duplicationGizmoKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    duplicationGizmoKernel,
                    "_DuplicationGizmoVertices",
                    buffers.NextBuffers.DuplicationGizmoBuffers[i].DuplicationVerticesBuffer
                );
                compute.SetFloat("_DuplicationGizmoStartHeart", buffers.NextBuffers.DuplicationGizmoBuffers[i].Settings.StartHeart);
                compute.SetFloat("_DuplicationGizmoEndHeart", buffers.NextBuffers.DuplicationGizmoBuffers[i].Settings.EndHeart);

                compute.GetKernelThreadGroupSizes(duplicationGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(duplicationGizmoKernel, threadGroups, 1, 1);
            }

            int extrusionGizmoKernel = compute.FindKernel("ExtrusionGizmoKernel");

            for (int i = 0; i < buffers.NextBuffers.ExtrusionGizmoBuffers.Count; i++) {
                compute.SetBuffer(extrusionGizmoKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(
                    extrusionGizmoKernel,
                    "_ExtrusionGizmoVertices",
                    buffers.NextBuffers.ExtrusionGizmoBuffers[i].ExtrusionVerticesBuffer
                );
                compute.SetFloat("_ExtrusionGizmoHeart", buffers.NextBuffers.ExtrusionGizmoBuffers[i].Settings.Heart);

                compute.GetKernelThreadGroupSizes(extrusionGizmoKernel, out threadGroupSize, out _, out _);
                threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

                compute.Dispatch(extrusionGizmoKernel, threadGroups, 1, 1);
            }

            int startCapKernel = compute.FindKernel("StartCapKernel");

            for (int i = 0; i < buffers.NextBuffers.StartCapBuffers.Count; i++) {
                compute.SetBuffer(startCapKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(startCapKernel, "_Matrices", buffers.NextBuffers.StartCapBuffers[i].MatricesBuffer);

                compute.GetKernelThreadGroupSizes(startCapKernel, out threadGroupSize, out _, out _);
                threadGroups = 1;

                compute.Dispatch(startCapKernel, threadGroups, 1, 1);
            }

            int endCapKernel = compute.FindKernel("EndCapKernel");

            for (int i = 0; i < buffers.NextBuffers.EndCapBuffers.Count; i++) {
                compute.SetBuffer(endCapKernel, "_Points", buffers.NextBuffers.PointsBuffer);
                compute.SetFloat("_Count", points.Length);

                compute.SetBuffer(endCapKernel, "_Matrices", buffers.NextBuffers.EndCapBuffers[i].MatricesBuffer);

                compute.GetKernelThreadGroupSizes(endCapKernel, out threadGroupSize, out _, out _);
                threadGroups = 1;

                compute.Dispatch(endCapKernel, threadGroups, 1, 1);
            }

            buffers.ComputeFence = AsyncGPUReadback.Request(buffers.NextBuffers.PointsBuffer);
        }
    }
}
