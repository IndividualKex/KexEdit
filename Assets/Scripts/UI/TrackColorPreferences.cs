using UnityEngine;

namespace KexEdit.UI {
    public static class TrackColorPreferences {
        public static Color GetColor(string trackStyle, int colorIndex, Color defaultColor) {
            string baseKey = GetColorKey(trackStyle, colorIndex);
            
            if (!IsOverridden(trackStyle, colorIndex)) {
                return defaultColor;
            }

            float r = PlayerPrefs.GetFloat($"{baseKey}_R", defaultColor.r);
            float g = PlayerPrefs.GetFloat($"{baseKey}_G", defaultColor.g);
            float b = PlayerPrefs.GetFloat($"{baseKey}_B", defaultColor.b);
            
            return new Color(r, g, b, defaultColor.a);
        }

        public static void SetColor(string trackStyle, int colorIndex, Color color) {
            string baseKey = GetColorKey(trackStyle, colorIndex);
            
            PlayerPrefs.SetFloat($"{baseKey}_R", color.r);
            PlayerPrefs.SetFloat($"{baseKey}_G", color.g);
            PlayerPrefs.SetFloat($"{baseKey}_B", color.b);
            PlayerPrefs.SetInt($"{baseKey}_Override", 1);
            PlayerPrefs.Save();
        }

        public static bool IsOverridden(string trackStyle, int colorIndex) {
            string baseKey = GetColorKey(trackStyle, colorIndex);
            return PlayerPrefs.GetInt($"{baseKey}_Override", 0) == 1;
        }

        public static void ResetColor(string trackStyle, int colorIndex) {
            string baseKey = GetColorKey(trackStyle, colorIndex);
            
            PlayerPrefs.DeleteKey($"{baseKey}_R");
            PlayerPrefs.DeleteKey($"{baseKey}_G");
            PlayerPrefs.DeleteKey($"{baseKey}_B");
            PlayerPrefs.DeleteKey($"{baseKey}_Override");
            PlayerPrefs.Save();
        }

        public static void ResetAllColors(string trackStyle) {
            for (int i = 0; i < 16; i++) {
                if (IsOverridden(trackStyle, i)) {
                    ResetColor(trackStyle, i);
                }
            }
        }

        private static string GetColorKey(string trackStyle, int colorIndex) {
            string cleanTrackStyle = trackStyle.Replace(".json", "");
            return $"{cleanTrackStyle}_Color{colorIndex}";
        }
    }
}
