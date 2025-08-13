StructuredBuffer<float4x4> _Matrices;
StructuredBuffer<uint> _VisualizationIndices;
StructuredBuffer<float4> _VisualizationData;

void TransformInstance_float(
    float instanceID, 
    float3 pos, 
    float3 normal,
    out float3 Position, 
    out float3 Normal,
    out float4 VisualizationData
) {
    uint id = (uint)instanceID;
    
    float4x4 mat = _Matrices[id];
    float4 worldPos = mul(mat, float4(pos, 1.0));
    
    #if defined(DOTS_INSTANCING_ON)
        float4x4 worldToObject = transpose(UNITY_MATRIX_M);
    #else
        float4x4 worldToObject = unity_WorldToObject;
    #endif
    
    float4 objectPos = mul(worldToObject, worldPos);
    Position = objectPos.xyz;
    
    float3 worldNormal = mul((float3x3)mat, normal);
    float3 objectNormal = mul((float3x3)worldToObject, worldNormal);
    Normal = normalize(objectNormal);
    
    uint pointIndex = _VisualizationIndices[id];
    VisualizationData = _VisualizationData[pointIndex];
}
