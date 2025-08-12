using UnityEngine;
using System;

namespace KexEdit {
    public class DuplicationMeshBuffers : IDisposable {
        public DuplicationMeshSettings Settings;
        public Mesh Mesh;
        public Material Material;
        public ComputeBuffer MatricesBuffer;
        public GraphicsBuffer DuplicationBuffer;
        public MaterialPropertyBlock MatProps;

        public DuplicationMeshBuffers(
            MeshBuffers meshBuffers,
            DuplicationMeshSettings settings,
            Mesh mesh,
            Material material
        ) {
            Settings = settings;
            Mesh = mesh;
            Material = material;

            int count = meshBuffers.Count;
            int matrixCount = settings.Offset < count - 1
                ? Mathf.Max(1, (count - 2 - settings.Offset) / settings.Step + 1)
                : 1;

            MatricesBuffer = new ComputeBuffer(matrixCount, 16 * sizeof(float));
            DuplicationBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            var duplicationData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            duplicationData[0].indexCountPerInstance = mesh.GetIndexCount(0);
            duplicationData[0].instanceCount = (uint)matrixCount;
            DuplicationBuffer.SetData(duplicationData);

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Matrices", MatricesBuffer);
            MatProps.SetBuffer("_VisualizationData", meshBuffers.VisualizationDataBuffer);
            MatProps.SetInt("_Count", matrixCount);
        }

        public void Dispose() {
            MatricesBuffer?.Dispose();
            DuplicationBuffer?.Dispose();
        }
    }
}
