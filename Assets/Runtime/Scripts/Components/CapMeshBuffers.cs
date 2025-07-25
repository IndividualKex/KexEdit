using UnityEngine;
using System;

namespace KexEdit {
    public class CapMeshBuffers : IDisposable {
        public Mesh Mesh;
        public Material Material;
        public ComputeBuffer MatricesBuffer;
        public GraphicsBuffer CapBuffer;
        public MaterialPropertyBlock MatProps;

        public CapMeshBuffers(MeshBuffers meshBuffers, Mesh capMesh, Material material, int capCount) {
            Mesh = capMesh;
            Material = material;

            MatricesBuffer = new ComputeBuffer(capCount, 16 * sizeof(float));
            CapBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            var capData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            capData[0].indexCountPerInstance = capMesh.GetIndexCount(0);
            capData[0].instanceCount = (uint)capCount;
            CapBuffer.SetData(capData);

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Matrices", MatricesBuffer);
            MatProps.SetBuffer("_VisualizationData", meshBuffers.VisualizationDataBuffer);
            MatProps.SetInt("_Count", capCount);
        }

        public void Dispose() {
            MatricesBuffer?.Dispose();
            CapBuffer?.Dispose();
        }
    }
}
