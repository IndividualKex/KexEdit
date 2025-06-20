using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public static class Units {
        private const float MetersToFeet = 3.28084f;
        private const float FeetToMeters = 1f / MetersToFeet;

        public static float MetersToDisplay(float meters) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => meters,
                DistanceUnitsType.Feet => meters * MetersToFeet,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static float DisplayToMeters(float display) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => display,
                DistanceUnitsType.Feet => display * FeetToMeters,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static string GetDistanceUnitsString() {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => s_UnitsMeters,
                DistanceUnitsType.Feet => s_UnitsFeet,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }
    }
}
