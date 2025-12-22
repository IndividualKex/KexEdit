using System.Collections.Generic;
using Unity.Mathematics;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public static class TrainCarPositionCalculator {
        public static float GetCarOffsetFromIndex(int carIndex, int totalCarCount, float carSpacing) {
            if (totalCarCount <= 0) return 0f;
            if (carIndex < 0 || carIndex >= totalCarCount) return 0f;

            if (totalCarCount == 1) {
                return 0f;
            }

            float totalTrainLength = (totalCarCount - 1) * carSpacing;
            float halfLength = totalTrainLength * 0.5f;

            return halfLength - (carIndex * carSpacing);
        }

        public static List<float> GetAllCarOffsets(int totalCarCount, float carSpacing) {
            var offsets = new List<float>();

            for (int i = 0; i < totalCarCount; i++) {
                offsets.Add(GetCarOffsetFromIndex(i, totalCarCount, carSpacing));
            }

            return offsets;
        }

        public static int GetCarIndexFromOffset(float offset, int totalCarCount, float carSpacing, float tolerance = 0.001f) {
            if (totalCarCount <= 0) return -1;

            for (int i = 0; i < totalCarCount; i++) {
                float carOffset = GetCarOffsetFromIndex(i, totalCarCount, carSpacing);
                if (math.abs(offset - carOffset) < tolerance) {
                    return i;
                }
            }

            return -1;
        }

        public static List<float> GetCarOffsetsFromConfig(TrainStyleConfig config) {
            if (config == null) {
                return new List<float> { 0f };
            }
            return GetAllCarOffsets(config.CarCount, config.CarSpacing);
        }

        public static string GetCarNameFromOffset(float offset, TrainStyleConfig config) {
            if (config == null) return FormatOffset(offset);

            int carCount = config.CarCount;

            if (carCount == 1 && math.abs(offset) < 0.001f) {
                return "Car 1";
            }

            if (carCount > 1 && math.abs(offset) < 0.001f) {
                return "Center";
            }

            var carOffsets = GetCarOffsetsFromConfig(config);
            for (int i = 0; i < carOffsets.Count; i++) {
                if (math.abs(offset - carOffsets[i]) < 0.001f) {
                    return $"Car {i + 1}";
                }
            }

            return FormatOffset(offset);
        }

        private static string FormatOffset(float offset) {
            float displayOffset = Units.DistanceToDisplay(offset);
            return StatsStringPool.GetDecimalTwo(displayOffset);
        }
    }
}
