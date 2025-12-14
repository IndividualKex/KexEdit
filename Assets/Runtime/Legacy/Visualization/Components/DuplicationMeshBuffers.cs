using UnityEngine;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit {
    public class DuplicationMeshBuffers : IDisposable {
        public Mesh Mesh;
        public Material Material;
        public int Step;
        public int Offset;

        public ComputeBuffer MatricesBuffer;
        public ComputeBuffer VisualizationIndicesBuffer;
        public GraphicsBuffer DuplicationBuffer;
        public MaterialPropertyBlock MatProps;

        private GraphicsBuffer.IndirectDrawIndexedArgs[] _duplicationData;

        public DuplicationMeshBuffers(
            Mesh mesh,
            Material material,
            int step,
            int offset
        ) {
            Mesh = mesh;
            Material = material;
            Step = step;
            Offset = offset;

            DuplicationBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            _duplicationData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _duplicationData[0].indexCountPerInstance = mesh.GetIndexCount(0);
            DuplicationBuffer.SetData(_duplicationData);

            MatProps = new MaterialPropertyBlock();
            MatProps.SetBuffer("_Matrices", MatricesBuffer);
        }

        public void Initialize(
            int totalPointCount, 
            NativeArray<int2> segmentBoundaries,
            ComputeBuffer visualizationData
        ) {
            MatricesBuffer?.Dispose();
            VisualizationIndicesBuffer?.Dispose();

            int totalMatrixCount = 0;
            foreach (var segment in segmentBoundaries) {
                int segmentLength = segment.y - segment.x + 1;
                int effectiveLength = segmentLength - 1;
                int segmentMatrixCount = Offset < effectiveLength
                    ? ((effectiveLength - 1 - Offset) / Step) + 1
                    : 0;
                totalMatrixCount += segmentMatrixCount;
            }

            int bufferSize = math.max(1, totalMatrixCount);
            MatricesBuffer = new ComputeBuffer(bufferSize, 16 * sizeof(float));
            VisualizationIndicesBuffer = new ComputeBuffer(bufferSize, sizeof(uint));

            var identityMatrices = new NativeArray<float4x4>(bufferSize, Allocator.Temp);
            for (int i = 0; i < bufferSize; i++) {
                identityMatrices[i] = float4x4.identity;
            }
            MatricesBuffer.SetData(identityMatrices);
            identityMatrices.Dispose();

            var visualizationIndices = new NativeArray<uint>(bufferSize, Allocator.Temp);
            for (int i = 0; i < bufferSize; i++) {
                visualizationIndices[i] = 0;
            }
            VisualizationIndicesBuffer.SetData(visualizationIndices);
            visualizationIndices.Dispose();

            _duplicationData[0].instanceCount = (uint)totalMatrixCount;
            DuplicationBuffer.SetData(_duplicationData);

            MatProps.SetBuffer("_Matrices", MatricesBuffer);
            MatProps.SetBuffer("_VisualizationIndices", VisualizationIndicesBuffer);
            MatProps.SetInt("_Count", totalMatrixCount);
            MatProps.SetBuffer("_VisualizationData", visualizationData);
        }

        public void Dispose() {
            MatricesBuffer?.Dispose();
            VisualizationIndicesBuffer?.Dispose();
            DuplicationBuffer?.Dispose();
        }
    }
}
