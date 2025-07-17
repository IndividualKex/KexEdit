using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public class TrackStyleConfig {
        public string Name;
        public Color[] Colors = new Color[0];
        public int DefaultStyle = 0;
        public List<TrackStyleMeshConfig> Styles = new();

        [NonSerialized]
        public string SourceFileName;

        public Color GetColor(int index) {
            if (Colors.Length == 0) return Color.white;
            if (index < 0 || index >= Colors.Length) return Colors[0];

            Color defaultColor = Colors[index];

            if (!string.IsNullOrEmpty(SourceFileName)) {
                return ColorPreferences.GetColor(SourceFileName, index, defaultColor);
            }

            return defaultColor;
        }
    }

    [Serializable]
    public class TrackStyleMeshConfig {
        public List<DuplicationMeshConfig> DuplicationMeshes = new();
        public List<ExtrusionMeshConfig> ExtrusionMeshes = new();
        public List<CapMeshConfig> StartCapMeshes = new();
        public List<CapMeshConfig> EndCapMeshes = new();
        public float Spacing = 0.4f;
        public float Threshold = 0f;
    }

    [Serializable]
    public class DuplicationMeshConfig {
        public string MeshPath;
        public int Step = 1;
        public int Offset = 0;
        public Color Color = Color.clear;
        public int ColorIndex = 0;
        public string TexturePath = "";

        public Color GetColor(Color baseColor) {
            return Color.a > 0 ? Color : baseColor;
        }

        public Color GetColor(TrackStyleConfig config) {
            if (Color.a > 0) return Color;
            return config.GetColor(ColorIndex);
        }

        public bool HasTexture() {
            return !string.IsNullOrEmpty(TexturePath);
        }
    }

    [Serializable]
    public class ExtrusionMeshConfig {
        public string MeshPath;
        public Color Color = Color.clear;
        public int ColorIndex = 0;
        public string TexturePath = "";

        public Color GetColor(Color baseColor) {
            return Color.a > 0 ? Color : baseColor;
        }

        public Color GetColor(TrackStyleConfig config) {
            if (Color.a > 0) return Color;
            return config.GetColor(ColorIndex);
        }

        public bool HasTexture() {
            return !string.IsNullOrEmpty(TexturePath);
        }
    }

    [Serializable]
    public class CapMeshConfig {
        public string MeshPath;
        public Color Color = Color.clear;
        public int ColorIndex = 0;
        public string TexturePath = "";

        public Color GetColor(Color baseColor) {
            return Color.a > 0 ? Color : baseColor;
        }

        public Color GetColor(TrackStyleConfig config) {
            if (Color.a > 0) return Color;
            return config.GetColor(ColorIndex);
        }

        public bool HasTexture() {
            return !string.IsNullOrEmpty(TexturePath);
        }
    }
}
