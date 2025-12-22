using UnityEngine;
using System;

namespace KexEdit.Legacy {
    public class CapMeshBuffers : IDisposable {
        public Mesh Mesh;
        public Material Material;

        public ComputeBuffer MatricesBuffer;
        public ComputeBuffer VisualizationIndicesBuffer;
        public GraphicsBuffer CapBuffer;
        public MaterialPropertyBlock MatProps;

        private GraphicsBuffer.IndirectDrawIndexedArgs[] _capData;

        public CapMeshBuffers(Mesh mesh, Material material) {
            Mesh = mesh;
            Material = material;

            CapBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            _capData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _capData[0].indexCountPerInstance = mesh.GetIndexCount(0);
            CapBuffer.SetData(_capData);

            MatProps = new MaterialPropertyBlock();
        }

        public void Initialize(int count, ComputeBuffer visualizationData) {
            MatricesBuffer?.Dispose();
            VisualizationIndicesBuffer?.Dispose();

            MatricesBuffer = new ComputeBuffer(count, 16 * sizeof(float));
            VisualizationIndicesBuffer = new ComputeBuffer(count, sizeof(uint));

            _capData[0].instanceCount = (uint)count;
            CapBuffer.SetData(_capData);

            MatProps.SetBuffer("_Matrices", MatricesBuffer);
            MatProps.SetBuffer("_VisualizationIndices", VisualizationIndicesBuffer);
            MatProps.SetInt("_Count", count);
            MatProps.SetBuffer("_VisualizationData", visualizationData);
        }

        public void Dispose() {
            MatricesBuffer?.Dispose();
            VisualizationIndicesBuffer?.Dispose();
            CapBuffer?.Dispose();
        }
    }
}
