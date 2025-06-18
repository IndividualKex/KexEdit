using UnityEngine;
using System;

namespace KexEdit {
    public class ExtrusionMeshBuffers : IDisposable {
        public ExtrusionMeshSettings Settings;
        public GraphicsBuffer CrossSectionVerticesBuffer;
        public GraphicsBuffer CrossSectionUVsBuffer;
        public GraphicsBuffer CrossSectionNormalsBuffer;
        public GraphicsBuffer CrossSectionTriangulationBuffer;
        public ComputeBuffer ExtrusionVerticesBuffer;
        public ComputeBuffer ExtrusionNormalsBuffer;
        public ComputeBuffer ExtrusionIndicesBuffer;
        public ComputeBuffer VisualizationIndicesBuffer;
        public MaterialPropertyBlock MatProps;

        public ExtrusionMeshBuffers(
            MeshBuffers meshBuffers,
            ExtrusionMeshSettings settings
        ) {
            int count = meshBuffers.Count;
            Settings = settings;

            CrossSectionVerticesBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                settings.Mesh.vertices.Length,
                sizeof(float) * 3
            );
            CrossSectionUVsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                settings.Mesh.uv.Length,
                sizeof(float) * 2
            );
            CrossSectionNormalsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                settings.Mesh.normals.Length,
                sizeof(float) * 3
            );
            CrossSectionTriangulationBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                settings.Mesh.triangles.Length,
                sizeof(uint)
            );

            CrossSectionVerticesBuffer.SetData(settings.Mesh.vertices);
            CrossSectionUVsBuffer.SetData(settings.Mesh.uv);
            CrossSectionNormalsBuffer.SetData(settings.Mesh.normals);
            CrossSectionTriangulationBuffer.SetData(settings.Mesh.triangles);

            ExtrusionVerticesBuffer = new ComputeBuffer(count * CrossSectionVerticesBuffer.count, sizeof(float) * 3);
            ExtrusionNormalsBuffer = new ComputeBuffer(count * CrossSectionVerticesBuffer.count, sizeof(float) * 3);
            ExtrusionIndicesBuffer = new ComputeBuffer(count * CrossSectionTriangulationBuffer.count, sizeof(uint));
            VisualizationIndicesBuffer = new ComputeBuffer(count * CrossSectionVerticesBuffer.count, sizeof(uint));

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Vertices", ExtrusionVerticesBuffer);
            MatProps.SetBuffer("_UVs", CrossSectionUVsBuffer);
            MatProps.SetBuffer("_Normals", ExtrusionNormalsBuffer);
            MatProps.SetBuffer("_Triangles", ExtrusionIndicesBuffer);
            MatProps.SetBuffer("_VisualizationIndices", VisualizationIndicesBuffer);
            MatProps.SetBuffer("_VisualizationData", meshBuffers.VisualizationDataBuffer);
            MatProps.SetInt("_UVCount", CrossSectionUVsBuffer.count);
        }

        public void Dispose() {
            CrossSectionVerticesBuffer?.Dispose();
            CrossSectionUVsBuffer?.Dispose();
            CrossSectionNormalsBuffer?.Dispose();
            CrossSectionTriangulationBuffer?.Dispose();
            ExtrusionVerticesBuffer?.Dispose();
            ExtrusionNormalsBuffer?.Dispose();
            ExtrusionIndicesBuffer?.Dispose();
            VisualizationIndicesBuffer?.Dispose();
        }
    }
}
