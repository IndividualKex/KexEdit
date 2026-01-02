StructuredBuffer<float3> _DeformedVertices;
StructuredBuffer<float3> _DeformedNormals;
StructuredBuffer<float4> _DeformedMask;
StructuredBuffer<float4> _DeformedData;
uniform uint _VertexCount;

void DeformVertex_float(
    float vertexID,
    float instanceID,
    out float3 Position,
    out float3 Normal,
    out float4 Mask,
    out float4 Data
) {
    uint segIdx = (uint)instanceID;
    uint vertIdx = (uint)vertexID;
    uint outputIdx = segIdx * _VertexCount + vertIdx;

    Position = _DeformedVertices[outputIdx];
    Normal = _DeformedNormals[outputIdx];
    Mask = _DeformedMask[outputIdx];
    Data = _DeformedData[outputIdx];
}
