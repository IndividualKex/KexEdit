void VisualizationColor_float(
    float Value, 
    out float3 Out
) {
    float isPositive = step(0.0, Value);
    float gamma = 0.4; // Power scaling gamma for better visual contrast
    
    // Negative side: interpolate from MinColor to ZeroColor with power scaling
    float negativeT = saturate(Value / _MinValue); // MinValue is negative, so this goes 1->0
    negativeT = pow(negativeT, gamma); // Apply power scaling
    float3 negativeColor = lerp(_ZeroColor, _MinColor, negativeT);
    
    // Positive side: interpolate from ZeroColor to MaxColor with power scaling
    float positiveT = saturate(Value / _MaxValue); // MaxValue is positive, so this goes 0->1
    positiveT = pow(positiveT, gamma); // Apply power scaling
    float3 positiveColor = lerp(_ZeroColor, _MaxColor, positiveT);
    
    // Combine: multiply by masks and add
    Out = negativeColor * (1.0 - isPositive) + positiveColor * isPositive;
}
