void ApplyVisualization_float(
    float4 Data,
    float3 BaseColor,
    float4 Mask,
    out float3 Color,
    out float3 Emission
) {
    float visValue = Data.x;
    float highlight = Data.y;

    // Mode 0 = no visualization
    float visActive = step(0.5, _VisualizationMode);

    float isPositive = step(0.0, visValue);
    const float gamma = 0.2;

    float negativeT = saturate(visValue / _MinValue);
    negativeT = pow(negativeT, gamma);
    float3 negativeColor = lerp(_ZeroColor, _MinColor, negativeT);

    float positiveT = saturate(visValue / _MaxValue);
    positiveT = pow(positiveT, gamma);
    float3 positiveColor = lerp(_ZeroColor, _MaxColor, positiveT);

    float3 visColor = negativeColor * (1.0 - isPositive) + positiveColor * isPositive;
    float3 baseWithVis = lerp(BaseColor, lerp(BaseColor, visColor, Mask.a), visActive);

    // Apply highlight: brighten by 30%
    float brighten = 1.0 + highlight * 0.3;
    Color = baseWithVis * brighten;

    // Highlight emission glow (5% of highlight color)
    Emission = _HighlightColor * (highlight * 0.05);
}
