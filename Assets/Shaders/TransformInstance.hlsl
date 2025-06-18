StructuredBuffer<float4x4> _Matrices;
StructuredBuffer<uint> _VisualizationIndices;
StructuredBuffer<float4> _VisualizationData;

void TransformInstance_float(float instanceID, float3 pos, out float3 Position, out float4 VisualizationData) {
    uint id = (uint)instanceID;
    uint visualizationIndex = _VisualizationIndices[id];
    
    float4x4 mat = _Matrices[id];
    float4 worldPos = mul(mat, float4(pos, 1.0));
    float4 objectPos = mul(unity_WorldToObject, worldPos);
    Position = objectPos.xyz;
    VisualizationData = _VisualizationData[visualizationIndex];
}
