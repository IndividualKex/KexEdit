void VisualizationColor_float(
    float Value, 
    out float3 Out
) {
    float isPositive = step(0.0, Value);
    
    // Negative side: interpolate from MinColor to ZeroColor
    float negativeT = saturate(Value / _MinValue); // MinValue is negative, so this goes 1->0
    float3 negativeColor = lerp(_ZeroColor, _MinColor, negativeT);
    
    // Positive side: interpolate from ZeroColor to MaxColor  
    float positiveT = saturate(Value / _MaxValue); // MaxValue is positive, so this goes 0->1
    float3 positiveColor = lerp(_ZeroColor, _MaxColor, positiveT);
    
    // Combine: multiply by masks and add
    Out = negativeColor * (1.0 - isPositive) + positiveColor * isPositive;
}
