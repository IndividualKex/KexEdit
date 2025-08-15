StructuredBuffer<float3> _Vertices;

void GetVertexPosition_float(float vertexID, out float3 Position) {
    Position = _Vertices[(uint)vertexID];
}
