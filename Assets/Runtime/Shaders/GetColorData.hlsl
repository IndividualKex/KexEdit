void GetColorData_float(float2 uv, out float3 Color) {
    float isRail = step(0.5, uv.x);
    Color = lerp(_RailColor.rgb, _TrackColor.rgb, isRail);
}
