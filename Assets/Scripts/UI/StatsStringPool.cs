using System.Collections.Generic;
using Unity.Mathematics;

namespace KexEdit.UI {
    public static class StatsStringPool {
        private static readonly Dictionary<int, string> s_IntegerStrings = new();
        private static readonly Dictionary<int, string> s_DecimalOneStrings = new();
        private static readonly Dictionary<int, string> s_DecimalTwoStrings = new();
        private static readonly Dictionary<int, string> s_AngleStrings = new();
        private static readonly Dictionary<int, string> s_AnglePerTimeStrings = new();
        private static readonly Dictionary<int, string> s_ForceStrings = new();
        private static readonly Dictionary<int, string> s_SpeedMetricStrings = new();
        private static readonly Dictionary<int, string> s_SpeedImperialStrings = new();
        private static readonly Dictionary<int, string> s_CarStrings = new();

        private const string NullValue = "--";

        public static void Initialize() {
            for (int i = -1000; i <= 1000; i++) {
                s_IntegerStrings[i] = i.ToString();
            }

            for (int i = -10000; i <= 10000; i++) {
                float value = i * 0.1f;
                s_DecimalOneStrings[i] = value.ToString("F1");
            }

            for (int i = -100000; i <= 100000; i++) {
                float value = i * 0.01f;
                s_DecimalTwoStrings[i] = value.ToString("F2");
            }

            for (int i = -3600; i <= 7200; i++) {
                float angle = i * 0.1f;
                s_AngleStrings[i] = angle.ToString("F1") + "°";
            }

            for (int i = -10000; i <= 10000; i++) {
                float value = i * 0.01f;
                s_AnglePerTimeStrings[i] = value.ToString("F2");
            }

            for (int i = -1000; i <= 1000; i++) {
                float value = i * 0.01f;
                s_ForceStrings[i] = value.ToString("F2") + " G";
            }

            for (int i = 0; i <= 2000; i++) {
                float value = i * 0.1f;
                s_SpeedMetricStrings[i] = value.ToString("F1") + " km/h";
                s_SpeedImperialStrings[i] = (value * 0.621371f).ToString("F1") + " mph";
            }

            for (int i = 1; i <= 20; i++) {
                s_CarStrings[i] = "Car " + i;
            }
        }

        public static string GetInteger(int value) {
            return s_IntegerStrings.TryGetValue(value, out var str) ? str : value.ToString();
        }

        public static string GetDecimalOne(float value) {
            int key = (int)math.round(value * 10f);
            return s_DecimalOneStrings.TryGetValue(key, out var str) ? str : value.ToString("F1");
        }

        public static string GetDecimalTwo(float value) {
            int key = (int)math.round(value * 100f);
            return s_DecimalTwoStrings.TryGetValue(key, out var str) ? str : value.ToString("F2");
        }

        public static string GetAngle(float degrees) {
            int key = (int)math.round(degrees * 10f);
            return s_AngleStrings.TryGetValue(key, out var str) ? str : degrees.ToString("F1") + "°";
        }

        public static string GetAnglePerTime(float value) {
            int key = (int)math.round(value * 100f);
            return s_AnglePerTimeStrings.TryGetValue(key, out var str) ? str : value.ToString("F2");
        }

        public static string GetForce(float force) {
            int key = (int)math.round(force * 100f);
            return s_ForceStrings.TryGetValue(key, out var str) ? str : force.ToString("F2") + " G";
        }

        public static string GetSpeedMetric(float kmh) {
            int key = (int)math.round(kmh * 10f);
            return s_SpeedMetricStrings.TryGetValue(key, out var str) ? str : kmh.ToString("F1") + " km/h";
        }

        public static string GetSpeedImperial(float mph) {
            int key = (int)math.round(mph * 10f / 0.621371f);
            return s_SpeedImperialStrings.TryGetValue(key, out var str) ? str : mph.ToString("F1") + " mph";
        }

        public static string GetSpeed(float speed, bool metric) {
            if (metric) {
                float kmh = Units.SpeedToDisplay(speed);
                return GetSpeedMetric(kmh);
            }
            else {
                float mph = Units.SpeedToDisplay(speed);
                return GetSpeedImperial(mph);
            }
        }

        public static string GetNull() {
            return NullValue;
        }

        public static string GetCarString(int carNumber) {
            return s_CarStrings.TryGetValue(carNumber, out var str) ? str : "Car " + carNumber;
        }
    }
}
