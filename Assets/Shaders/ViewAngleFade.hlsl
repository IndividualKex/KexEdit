void ViewAngleFade_float(float3 worldPos, out float Out)
{
    const float threshold = 0.01;
    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
    float3 gridNormal = float3(0, 1, 0);
    float viewAngle = abs(dot(viewDir, gridNormal));
    Out = saturate(max(viewAngle - threshold, 0) / (1 - threshold));
}
