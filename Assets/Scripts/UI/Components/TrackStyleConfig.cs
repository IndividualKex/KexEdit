using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit.UI {
    [Serializable]
    public class TrackStyleConfig {
        public string name;
        public Color[] colors = new Color[0];
        public List<PieceMeshConfig> pieces = new();
        public List<StyleEntry> styles = new();
        public int defaultStyle;

        [NonSerialized]
        public string SourceFileName;

        public Color GetColor(int index) {
            if (colors.Length == 0) return Color.white;
            if (index < 0 || index >= colors.Length) return colors[0];

            Color defaultColor = colors[index];

            if (!string.IsNullOrEmpty(SourceFileName)) {
                return TrackColorPreferences.GetColor(SourceFileName, index, defaultColor);
            }

            return defaultColor;
        }
    }

    [Serializable]
    public class StyleEntry {
        public List<PieceMeshConfig> pieces = new();
    }

    [Serializable]
    public class PieceMeshConfig {
        public string mesh;
        public float length;
    }
}
