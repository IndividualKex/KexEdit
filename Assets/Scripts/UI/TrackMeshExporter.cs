using System;
using System.IO;
using System.Text;
using KexEdit.Legacy;
using KexEdit.Rendering;
using KexEdit.Sim.Schema;
using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using SFB;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    [BurstCompile]
    public static class TrackMeshExporter {
        public static void ExportTrackMesh() {
            try {
                string filePath = StandaloneFileBrowser.SaveFilePanel(
                    "Export Track Mesh",
                    Application.persistentDataPath,
                    "track",
                    new[] {
                        new ExtensionFilter("OBJ", "obj"),
                        new ExtensionFilter("All Files", "*")
                    });

                if (!string.IsNullOrEmpty(filePath)) {
                    ExportTrackMeshInternal(filePath);
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to export track mesh: {ex.Message}");
            }
        }

        private static void ExportTrackMeshInternal(string filePath) {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            var trackQuery = entityManager.CreateEntityQuery(typeof(TrackSingleton));
            if (trackQuery.IsEmpty) {
                Debug.LogWarning("No track data found to export");
                return;
            }
            var trackSingleton = trackQuery.GetSingleton<TrackSingleton>();
            ref readonly var track = ref trackSingleton.Value;

            if (!track.IsCreated || track.SectionCount == 0) {
                Debug.LogWarning("Track has no sections to export");
                return;
            }

            var pieceStyleQuery = entityManager.CreateEntityQuery(typeof(PieceStyleSingleton));
            if (pieceStyleQuery.IsEmpty) {
                Debug.LogWarning("No style configuration found");
                return;
            }
            var pieceStyle = pieceStyleQuery.GetSingleton<PieceStyleSingleton>();

            var styleConfigQuery = entityManager.CreateEntityQuery(typeof(StyleConfigSingleton));
            if (styleConfigQuery.IsEmpty) {
                Debug.LogWarning("No style config found");
                return;
            }
            var styleConfig = styleConfigQuery.GetSingleton<StyleConfigSingleton>();

            KeyframeStore keyframes = default;
            var coasterQuery = entityManager.CreateEntityQuery(typeof(CoasterData));
            var coasterEntities = coasterQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in coasterEntities) {
                var coasterData = entityManager.GetComponentData<CoasterData>(entity);
                if (coasterData.Value.Keyframes.Keyframes.IsCreated) {
                    keyframes = coasterData.Value.Keyframes;
                    break;
                }
            }
            coasterEntities.Dispose();

            var pieceConfig = new StylePieceConfig(pieceStyle.TrackPieces, pieceStyle.StyleRanges, styleConfig.DefaultStyleIndex);
            int maxStyleIndex = pieceConfig.StyleCount - 1;

            var allBreakpoints = new NativeList<StyleBreakpoint>(track.SectionCount * 2, Allocator.Temp);
            var gpuSplinePoints = new NativeList<GPUSplinePoint>(4096, Allocator.Temp);
            var gpuVisualizationData = new NativeList<float>(4096, Allocator.Temp);
            var allSegments = new NativeList<GPUSegmentBoundary>(256, Allocator.Temp);
            var pieceCounts = new NativeArray<int>(pieceStyle.AllPieces.Length, Allocator.Temp);

            try {
                StyleBreakpointDetector.DetectAllBreakpoints(in track, in keyframes, maxStyleIndex, ref allBreakpoints);
                SegmentBuilder.Build(in track, default, in pieceConfig, in allBreakpoints,
                    ref gpuSplinePoints, ref gpuVisualizationData, ref allSegments, ref pieceCounts);

                // Convert GPU spline points to SplinePoint for CPU deformation
                var splinePoints = new NativeArray<SplinePoint>(gpuSplinePoints.Length, Allocator.Temp);
                for (int i = 0; i < gpuSplinePoints.Length; i++) {
                    var gp = gpuSplinePoints[i];
                    splinePoints[i] = new SplinePoint(gp.Arc, gp.Position, gp.Direction, gp.Normal, gp.Lateral);
                }

                ExportSegmentsToObj(filePath, pieceStyle.AllPieces, allSegments.AsArray(), splinePoints);

                splinePoints.Dispose();
                Debug.Log($"Exported track mesh to: {filePath}");
            }
            finally {
                allBreakpoints.Dispose();
                gpuSplinePoints.Dispose();
                gpuVisualizationData.Dispose();
                allSegments.Dispose();
                pieceCounts.Dispose();
            }
        }

        private static void ExportSegmentsToObj(
            string filePath,
            PieceMesh[] pieces,
            NativeArray<GPUSegmentBoundary> segments,
            NativeArray<SplinePoint> splinePoints
        ) {
            var buffer = new StringBuilder(1024 * 1024);
            buffer.AppendLine("# Track mesh exported from KexEdit");
            buffer.AppendLine($"# Export date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            buffer.AppendLine($"# Segments: {segments.Length}");
            buffer.AppendLine("");

            int vertexOffset = 1;
            int normalOffset = 1;

            for (int s = 0; s < segments.Length; s++) {
                var segment = segments[s];
                if (segment.PieceIndex < 0 || segment.PieceIndex >= pieces.Length) continue;

                ref readonly var piece = ref pieces[segment.PieceIndex];
                var mesh = piece.Mesh;
                if (mesh == null) continue;

                var sourceVertices = mesh.vertices;
                var sourceNormals = mesh.normals;
                var triangles = mesh.triangles;

                int vertexCount = sourceVertices.Length;
                if (vertexCount == 0) continue;

                var srcVerts = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var srcNorms = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var outVerts = new NativeArray<float3>(vertexCount, Allocator.Temp);
                var outNorms = new NativeArray<float3>(vertexCount, Allocator.Temp);

                for (int i = 0; i < vertexCount; i++) {
                    srcVerts[i] = sourceVertices[i];
                    srcNorms[i] = i < sourceNormals.Length ? (float3)sourceNormals[i] : new float3(0, 1, 0);
                }

                int splineStart = segment.SplineStartIndex;
                int splineCount = segment.SplineCount;
                if (splineStart < 0 || splineStart + splineCount > splinePoints.Length) {
                    srcVerts.Dispose();
                    srcNorms.Dispose();
                    outVerts.Dispose();
                    outNorms.Dispose();
                    continue;
                }

                var splineSlice = splinePoints.GetSubArray(splineStart, splineCount);

                Deform.Mesh(
                    srcVerts,
                    srcNorms,
                    splineSlice,
                    segment.StartArc,
                    segment.Length,
                    piece.NominalLength,
                    ref outVerts,
                    ref outNorms
                );

                buffer.AppendLine($"o Segment_{s}");

                // Unity is left-handed, OBJ is right-handed: negate Z
                for (int i = 0; i < vertexCount; i++) {
                    var v = outVerts[i];
                    buffer.AppendLine($"v {v.x:F6} {v.y:F6} {-v.z:F6}");
                }

                for (int i = 0; i < vertexCount; i++) {
                    var n = outNorms[i];
                    buffer.AppendLine($"vn {n.x:F6} {n.y:F6} {-n.z:F6}");
                }

                // Flip winding: combined with Z negation for correct handedness conversion
                for (int i = 0; i < triangles.Length; i += 3) {
                    int i0 = triangles[i] + vertexOffset;
                    int i1 = triangles[i + 1] + vertexOffset;
                    int i2 = triangles[i + 2] + vertexOffset;
                    int n0 = triangles[i] + normalOffset;
                    int n1 = triangles[i + 1] + normalOffset;
                    int n2 = triangles[i + 2] + normalOffset;
                    buffer.AppendLine($"f {i2}//{n2} {i1}//{n1} {i0}//{n0}");
                }

                vertexOffset += vertexCount;
                normalOffset += vertexCount;
                buffer.AppendLine("");

                srcVerts.Dispose();
                srcNorms.Dispose();
                outVerts.Dispose();
                outNorms.Dispose();
            }

            var utf8WithoutBom = new UTF8Encoding(false);
            File.WriteAllText(filePath, buffer.ToString(), utf8WithoutBom);
        }
    }
}
