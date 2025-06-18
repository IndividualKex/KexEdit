StructuredBuffer<uint> _DuplicationPoints;

void Discard_float(float instanceID, out float Out) {
    Out = _DuplicationPoints[instanceID];
}
