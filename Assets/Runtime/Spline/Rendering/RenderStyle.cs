using UnityEngine;

namespace KexEdit.Spline.Rendering {
    public struct RenderStyle {
        public Color PrimaryColor;
        public Color SecondaryColor;
        public Color TertiaryColor;

        public static RenderStyle Default => new() {
            PrimaryColor = Color.white,
            SecondaryColor = Color.white,
            TertiaryColor = Color.white
        };
    }
}
