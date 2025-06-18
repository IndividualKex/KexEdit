StructuredBuffer<float3> _Vertices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<uint> _Triangles;
StructuredBuffer<float2> _UVs;
StructuredBuffer<uint> _VisualizationIndices;
StructuredBuffer<float4> _VisualizationData;
uniform uint _UVCount;

void GetVertexData_float(
    float vertexID, 
    out float3 Position, 
    out float3 Normal, 
    out float2 UV, 
    out float4 VisualizationData
) {
    uint index = _Triangles[(uint)vertexID];
    uint uvIndex = index - (index / _UVCount) * _UVCount;
    uint visualizationIndex = _VisualizationIndices[index];

    Position = _Vertices[index];
    Normal = _Normals[index];
    UV = _UVs[uvIndex];
    VisualizationData = _VisualizationData[visualizationIndex];
}
