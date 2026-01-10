using System;
using KexEdit.Sim.Schema;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KexEdit.Rendering {
    public sealed class TrackMeshPipeline : IDisposable {
        private const int MAX_THREAD_GROUPS = 65535;
        private const int THREADS_PER_GROUP = 64;
        private const int MAX_VERTS_PER_DISPATCH = MAX_THREAD_GROUPS * THREADS_PER_GROUP;

        private struct PieceBuffers {
            public ComputeBuffer SourceVertices;
            public ComputeBuffer SourceNormals;
            public ComputeBuffer SourceMask;
            public ComputeBuffer OutputVertices;
            public ComputeBuffer OutputNormals;
            public ComputeBuffer OutputMask;
            public ComputeBuffer OutputData;
            public GraphicsBuffer ArgsBuffer;
        }

        private readonly ComputeShader _computeShader;
        private readonly int _kernelId;
        private readonly Material _material;
        private readonly PieceMesh[] _pieces;
        private readonly NativeArray<TrackPiece> _trackPieces;
        private readonly NativeArray<StylePieceRange> _styleRanges;
        private readonly int _defaultStyleIndex;

        private PieceBuffers[] _pieceBuffers;
        private ComputeBuffer _splineBuffer;
        private ComputeBuffer _segmentBuffer;
        private ComputeBuffer _visualizationBuffer;
        private ComputeBuffer _sectionHighlightsBuffer;

        private int _maxSegments;
        private int _maxSplinePoints;
        private int _maxSections;
        private readonly Bounds _bounds;
        private readonly MaterialPropertyBlock _matProps;
        private readonly uint[] _argsData = new uint[5];

        public TrackMeshPipeline(
            ComputeShader computeShader,
            Material material,
            PieceMesh[] allPieces,
            NativeArray<TrackPiece> trackPieces,
            NativeArray<StylePieceRange> styleRanges,
            int defaultStyleIndex
        ) {
            _computeShader = computeShader;
            _kernelId = computeShader.FindKernel("DeformKernel");
            _material = material;
            _pieces = allPieces;
            _trackPieces = trackPieces;
            _styleRanges = styleRanges;
            _defaultStyleIndex = defaultStyleIndex;

            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            _maxSegments = 256;
            _maxSplinePoints = 4096;
            _maxSections = 64;
            _matProps = new MaterialPropertyBlock();

            CreateBuffers();
        }

        public void Dispose() {
            DisposeBuffers();
            DisposePieceBuffers();
        }

        public void Render(
            in Track.Track track,
            in KeyframeStore keyframes,
            in RenderStyle style,
            VisualizationMode visualizationMode,
            NativeArray<float> sectionHighlights = default) {

            if (!track.IsCreated || track.SectionCount == 0) return;

            // Select visualization channel based on mode (zero-copy aliasing)
            var selectedVisData = visualizationMode switch {
                VisualizationMode.Velocity => track.SplineVelocities.AsArray(),
                VisualizationMode.NormalForce => track.SplineNormalForces.AsArray(),
                VisualizationMode.LateralForce => track.SplineLateralForces.AsArray(),
                VisualizationMode.RollSpeed => track.SplineRollSpeeds.AsArray(),
                _ => default
            };

            int pieceCount = _pieces.Length;
            var gpuSplinePoints = new NativeList<GPUSplinePoint>(_maxSplinePoints, Allocator.Temp);
            var gpuVisualizationData = new NativeList<float>(_maxSplinePoints, Allocator.Temp);
            var allSegments = new NativeList<GPUSegmentBoundary>(_maxSegments, Allocator.Temp);
            var pieceCounts = new NativeArray<int>(pieceCount, Allocator.Temp);

            var pieceConfig = new StylePieceConfig(_trackPieces, _styleRanges, _defaultStyleIndex);
            int maxStyleIndex = pieceConfig.StyleCount - 1;

            var allBreakpoints = new NativeList<StyleBreakpoint>(track.SectionCount * 2, Allocator.Temp);

            try {
                StyleBreakpointDetector.DetectAllBreakpoints(in track, in keyframes, maxStyleIndex, ref allBreakpoints);
                SegmentBuilder.Build(in track, selectedVisData, in pieceConfig, in allBreakpoints,
                    ref gpuSplinePoints, ref gpuVisualizationData, ref allSegments, ref pieceCounts);
                RenderPieces(in track, in gpuSplinePoints, in gpuVisualizationData, in allSegments, in pieceCounts,
                    in style, sectionHighlights);
            }
            finally {
                allBreakpoints.Dispose();
                gpuSplinePoints.Dispose();
                gpuVisualizationData.Dispose();
                allSegments.Dispose();
                pieceCounts.Dispose();
            }
        }

        private void RenderPieces(
            in Track.Track track,
            in NativeList<GPUSplinePoint> gpuSplinePoints,
            in NativeList<float> gpuVisualizationData,
            in NativeList<GPUSegmentBoundary> allSegments,
            in NativeArray<int> pieceCounts,
            in RenderStyle style,
            NativeArray<float> sectionHighlights) {

            int totalSplinePoints = gpuSplinePoints.Length;
            int sectionCount = track.SectionCount;
            int maxPieceSegments = 0;
            for (int p = 0; p < pieceCounts.Length; p++) {
                maxPieceSegments = math.max(maxPieceSegments, pieceCounts[p]);
            }

            bool needsResize = maxPieceSegments > _maxSegments ||
                               totalSplinePoints > _maxSplinePoints ||
                               sectionCount > _maxSections;

            if (needsResize) {
                _maxSegments = math.max(_maxSegments, maxPieceSegments);
                _maxSplinePoints = math.max(_maxSplinePoints, totalSplinePoints);
                _maxSections = math.max(_maxSections, sectionCount);
                DisposeBuffers();
                CreateBuffers();
            }

            _splineBuffer.SetData(gpuSplinePoints.AsArray());
            if (gpuVisualizationData.Length > 0) {
                _visualizationBuffer.SetData(gpuVisualizationData.AsArray());
            }

            // Prepare section highlight data (default to 0.0 if not provided)
            var highlights = new NativeArray<float>(sectionCount, Allocator.Temp);
            if (sectionHighlights.IsCreated && sectionHighlights.Length > 0) {
                int copyCount = math.min(highlights.Length, sectionHighlights.Length);
                NativeArray<float>.Copy(sectionHighlights, highlights, copyCount);
            }
            _sectionHighlightsBuffer.SetData(highlights);
            highlights.Dispose();

            int segmentOffset = 0;
            for (int p = 0; p < _pieces.Length; p++) {
                int segmentCount = pieceCounts[p];
                if (segmentCount == 0) continue;

                ref readonly var piece = ref _pieces[p];
                ref var buffers = ref _pieceBuffers[p];

                _segmentBuffer.SetData(allSegments.AsArray(), segmentOffset, 0, segmentCount);

                _computeShader.SetBuffer(_kernelId, "_SourceVertices", buffers.SourceVertices);
                _computeShader.SetBuffer(_kernelId, "_SourceNormals", buffers.SourceNormals);
                _computeShader.SetBuffer(_kernelId, "_SourceMask", buffers.SourceMask);
                _computeShader.SetBuffer(_kernelId, "_SplinePoints", _splineBuffer);
                _computeShader.SetBuffer(_kernelId, "_SourceData", _visualizationBuffer);
                _computeShader.SetBuffer(_kernelId, "_SectionHighlights", _sectionHighlightsBuffer);
                _computeShader.SetBuffer(_kernelId, "_Segments", _segmentBuffer);
                _computeShader.SetBuffer(_kernelId, "_OutputVertices", buffers.OutputVertices);
                _computeShader.SetBuffer(_kernelId, "_OutputNormals", buffers.OutputNormals);
                _computeShader.SetBuffer(_kernelId, "_OutputMask", buffers.OutputMask);
                _computeShader.SetBuffer(_kernelId, "_OutputData", buffers.OutputData);
                _computeShader.SetFloat("_NominalLength", piece.NominalLength);

                // Batch dispatch to stay within thread group limits
                int maxSegmentsPerBatch = MAX_VERTS_PER_DISPATCH / piece.VertexCount;
                int batchSegmentStart = 0;

                while (batchSegmentStart < segmentCount) {
                    int batchSegmentCount = math.min(segmentCount - batchSegmentStart, maxSegmentsPerBatch);
                    int batchVerts = piece.VertexCount * batchSegmentCount;
                    int threadGroups = (batchVerts + THREADS_PER_GROUP - 1) / THREADS_PER_GROUP;

                    _computeShader.SetInt("_VertexCount", piece.VertexCount);
                    _computeShader.SetInt("_SegmentCount", batchSegmentCount);
                    _computeShader.SetInt("_SegmentStart", batchSegmentStart);
                    _computeShader.Dispatch(_kernelId, threadGroups, 1, 1);

                    batchSegmentStart += batchSegmentCount;
                }

                segmentOffset += segmentCount;

                _argsData[0] = (uint)piece.Mesh.GetIndexCount(0);
                _argsData[1] = (uint)segmentCount;
                _argsData[2] = (uint)piece.Mesh.GetIndexStart(0);
                _argsData[3] = (uint)piece.Mesh.GetBaseVertex(0);
                _argsData[4] = 0;
                buffers.ArgsBuffer.SetData(_argsData);

                _matProps.Clear();
                _matProps.SetBuffer("_DeformedVertices", buffers.OutputVertices);
                _matProps.SetBuffer("_DeformedNormals", buffers.OutputNormals);
                _matProps.SetBuffer("_DeformedMask", buffers.OutputMask);
                _matProps.SetBuffer("_DeformedData", buffers.OutputData);
                _matProps.SetInt("_VertexCount", piece.VertexCount);
                _matProps.SetColor("_PrimaryColor", style.PrimaryColor);
                _matProps.SetColor("_SecondaryColor", style.SecondaryColor);
                _matProps.SetColor("_TertiaryColor", style.TertiaryColor);

                var rp = new RenderParams(_material) {
                    worldBounds = _bounds,
                    matProps = _matProps,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true
                };

                Graphics.RenderMeshIndirect(rp, piece.Mesh, buffers.ArgsBuffer);
            }
        }

        private void CreateBuffers() {
            _pieceBuffers = new PieceBuffers[_pieces.Length];

            for (int i = 0; i < _pieces.Length; i++) {
                ref readonly var piece = ref _pieces[i];
                ref var buffers = ref _pieceBuffers[i];
                var mesh = piece.Mesh;
                int vertexCount = piece.VertexCount;

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var colors = mesh.colors;

                buffers.SourceVertices = new ComputeBuffer(vertexCount, 3 * sizeof(float));
                buffers.SourceNormals = new ComputeBuffer(vertexCount, 3 * sizeof(float));
                buffers.SourceMask = new ComputeBuffer(vertexCount, 4 * sizeof(float));

                var vertexData = new float3[vertexCount];
                var normalData = new float3[vertexCount];
                var maskData = new float4[vertexCount];
                for (int v = 0; v < vertexCount; v++) {
                    vertexData[v] = vertices[v];
                    normalData[v] = normals[v];
                    if (colors != null && v < colors.Length) {
                        var c = colors[v];
                        maskData[v] = new float4(c.r, c.g, c.b, 1f);
                    }
                    else {
                        maskData[v] = new float4(1, 1, 1, 1);
                    }
                }
                buffers.SourceVertices.SetData(vertexData);
                buffers.SourceNormals.SetData(normalData);
                buffers.SourceMask.SetData(maskData);

                buffers.OutputVertices = new ComputeBuffer(vertexCount * _maxSegments, 3 * sizeof(float));
                buffers.OutputNormals = new ComputeBuffer(vertexCount * _maxSegments, 3 * sizeof(float));
                buffers.OutputMask = new ComputeBuffer(vertexCount * _maxSegments, 4 * sizeof(float));
                buffers.OutputData = new ComputeBuffer(vertexCount * _maxSegments, 4 * sizeof(float));
                buffers.ArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            }

            _splineBuffer = new ComputeBuffer(_maxSplinePoints, GPUSplinePoint.Stride);
            _segmentBuffer = new ComputeBuffer(_maxSegments, GPUSegmentBoundary.Stride);
            _visualizationBuffer = new ComputeBuffer(_maxSplinePoints, sizeof(float));
            _sectionHighlightsBuffer = new ComputeBuffer(_maxSections, sizeof(float));
        }

        private void DisposeBuffers() {
            _splineBuffer?.Dispose();
            _segmentBuffer?.Dispose();
            _visualizationBuffer?.Dispose();
            _sectionHighlightsBuffer?.Dispose();
            _splineBuffer = null;
            _segmentBuffer = null;
            _visualizationBuffer = null;
            _sectionHighlightsBuffer = null;

            if (_pieceBuffers != null) {
                foreach (ref var buffers in _pieceBuffers.AsSpan()) {
                    buffers.OutputVertices?.Dispose();
                    buffers.OutputNormals?.Dispose();
                    buffers.OutputMask?.Dispose();
                    buffers.OutputData?.Dispose();
                    buffers.ArgsBuffer?.Dispose();
                    buffers.OutputVertices = null;
                    buffers.OutputNormals = null;
                    buffers.OutputMask = null;
                    buffers.OutputData = null;
                    buffers.ArgsBuffer = null;
                }
            }
        }

        private void DisposePieceBuffers() {
            if (_pieceBuffers == null) return;

            foreach (ref var buffers in _pieceBuffers.AsSpan()) {
                buffers.SourceVertices?.Dispose();
                buffers.SourceNormals?.Dispose();
                buffers.SourceMask?.Dispose();
                buffers.SourceVertices = null;
                buffers.SourceNormals = null;
                buffers.SourceMask = null;
            }
            _pieceBuffers = null;
        }
    }
}
