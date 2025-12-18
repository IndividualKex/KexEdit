using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public static class TrainCarCountPreferences {
        public static int GetCarCount(string trainStyle, int defaultCount) {
            if (!IsOverridden(trainStyle)) {
                return defaultCount;
            }

            return PlayerPrefs.GetInt(GetCarCountKey(trainStyle), defaultCount);
        }

        public static void SetCarCount(string trainStyle, int count) {
            string key = GetCarCountKey(trainStyle);
            
            PlayerPrefs.SetInt(key, count);
            PlayerPrefs.SetInt($"{key}_Override", 1);
            PlayerPrefs.Save();
        }

        public static bool IsOverridden(string trainStyle) {
            string key = GetCarCountKey(trainStyle);
            return PlayerPrefs.GetInt($"{key}_Override", 0) == 1;
        }

        public static void ResetCarCount(string trainStyle) {
            string key = GetCarCountKey(trainStyle);
            
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey($"{key}_Override");
            PlayerPrefs.Save();
        }

        private static string GetCarCountKey(string trainStyle) {
            string cleanTrainStyle = trainStyle.Replace(".json", "");
            return $"{cleanTrainStyle}_CarCount";
        }
    }
}