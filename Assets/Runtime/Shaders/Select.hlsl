float3 _SelectedColor;

void Select_float(float3 color, float selected, out float3 Color, out float3 Emission) {
    float brightenFactor = 1 + selected * 0.3;
    Color = color * brightenFactor;

    float emission = selected * 0.05;
    Emission = _SelectedColor * emission;
}
