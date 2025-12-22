void VertexColor_float(
    float4 Mask,
    out float3 Color
) {
    Color = Mask.r * _PrimaryColor.rgb
          + Mask.g * _SecondaryColor.rgb
          + Mask.b * _TertiaryColor.rgb;
}
