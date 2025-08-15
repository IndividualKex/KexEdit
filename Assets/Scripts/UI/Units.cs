using Unity.Mathematics;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public static class Units {
        private const float MetersToFeet = 3.28084f;
        private const float FeetToMeters = 1f / MetersToFeet;

        private const float MPSToKPH = 3.6f;
        private const float KPHToMPS = 1f / MPSToKPH;

        private const float MPSToMPH = 2.23694f;
        private const float MPHToMPS = 1f / MPSToMPH;

        public static float DistanceToDisplay(float distance) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => distance,
                DistanceUnitsType.Feet => distance * MetersToFeet,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static float DisplayToDistance(float display) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => display,
                DistanceUnitsType.Feet => display * FeetToMeters,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static float AngleToDisplay(float angle) {
            return Preferences.AngleUnits switch {
                AngleUnitsType.Degrees => angle,
                AngleUnitsType.Radians => math.radians(angle),
                _ => throw new System.Exception("Invalid angle units type"),
            };
        }

        public static float DisplayToAngle(float display) {
            return Preferences.AngleUnits switch {
                AngleUnitsType.Degrees => display,
                AngleUnitsType.Radians => math.degrees(display),
                _ => throw new System.Exception("Invalid angle units type"),
            };
        }

        public static float AnglePerTimeToDisplay(float anglePerTime) {
            return Preferences.AngleChangeUnits switch {
                AngleChangeUnitsType.Radians => anglePerTime,
                AngleChangeUnitsType.Degrees => math.degrees(anglePerTime),
                _ => throw new System.Exception("Invalid angle change units type"),
            };
        }

        public static float DisplayToAnglePerTime(float display) {
            return Preferences.AngleChangeUnits switch {
                AngleChangeUnitsType.Radians => display,
                AngleChangeUnitsType.Degrees => math.radians(display),
                _ => throw new System.Exception("Invalid angle change units type"),
            };
        }

        public static float AnglePerDistanceToDisplay(float anglePerDistance) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Radians => anglePerDistance,
                    AngleChangeUnitsType.Degrees => math.degrees(anglePerDistance),
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                DistanceUnitsType.Feet => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Radians => anglePerDistance * FeetToMeters,
                    AngleChangeUnitsType.Degrees => math.degrees(anglePerDistance) * FeetToMeters,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static float DisplayToAnglePerDistance(float display) {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Radians => display,
                    AngleChangeUnitsType.Degrees => math.radians(display),
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                DistanceUnitsType.Feet => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Radians => display * MetersToFeet,
                    AngleChangeUnitsType.Degrees => math.radians(display) * MetersToFeet,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static float SpeedToDisplay(float speed) {
            return Preferences.SpeedUnits switch {
                SpeedUnitsType.MetersPerSecond => speed,
                SpeedUnitsType.KilometersPerHour => speed * MPSToKPH,
                SpeedUnitsType.MilesPerHour => speed * MPSToMPH,
                _ => throw new System.Exception("Invalid speed units type"),
            };
        }

        public static float DisplayToSpeed(float display) {
            return Preferences.SpeedUnits switch {
                SpeedUnitsType.MetersPerSecond => display,
                SpeedUnitsType.KilometersPerHour => display * KPHToMPS,
                SpeedUnitsType.MilesPerHour => display * MPHToMPS,
                _ => throw new System.Exception("Invalid speed units type"),
            };
        }

        public static string GetDistanceUnitsString() {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => s_UnitsMeters,
                DistanceUnitsType.Feet => s_UnitsFeet,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static string GetDistanceUnitsSuffix() {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => s_UnitsSuffixMeters,
                DistanceUnitsType.Feet => s_UnitsSuffixFeet,
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static string GetAngleUnitsString() {
            return Preferences.AngleUnits switch {
                AngleUnitsType.Degrees => s_UnitsDegrees,
                AngleUnitsType.Radians => s_UnitsRadians,
                _ => throw new System.Exception("Invalid angle units type"),
            };
        }

        public static string GetAngleUnitsSuffix() {
            return Preferences.AngleUnits switch {
                AngleUnitsType.Degrees => s_UnitsSuffixDegrees,
                AngleUnitsType.Radians => s_UnitsSuffixRadians,
                _ => throw new System.Exception("Invalid angle units type"),
            };
        }

        public static string GetAnglePerTimeString() {
            return Preferences.AngleChangeUnits switch {
                AngleChangeUnitsType.Degrees => s_UnitsDegreesPerSecond,
                AngleChangeUnitsType.Radians => s_UnitsRadiansPerSecond,
                _ => throw new System.Exception("Invalid angle change units type"),
            };
        }

        public static string GetAnglePerTimeSuffix() {
            return Preferences.AngleChangeUnits switch {
                AngleChangeUnitsType.Degrees => s_UnitsSuffixDegreesPerSecond,
                AngleChangeUnitsType.Radians => s_UnitsSuffixRadiansPerSecond,
                _ => throw new System.Exception("Invalid angle change units type"),
            };
        }

        public static string GetAnglePerDistanceString() {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Degrees => s_UnitsDegreesPerMeter,
                    AngleChangeUnitsType.Radians => s_UnitsRadiansPerMeter,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                DistanceUnitsType.Feet => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Degrees => s_UnitsDegreesPerFoot,
                    AngleChangeUnitsType.Radians => s_UnitsRadiansPerFoot,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static string GetAnglePerDistanceSuffix() {
            return Preferences.DistanceUnits switch {
                DistanceUnitsType.Meters => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Degrees => s_UnitsSuffixDegreesPerMeter,
                    AngleChangeUnitsType.Radians => s_UnitsSuffixRadiansPerMeter,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                DistanceUnitsType.Feet => Preferences.AngleChangeUnits switch {
                    AngleChangeUnitsType.Degrees => s_UnitsSuffixDegreesPerFoot,
                    AngleChangeUnitsType.Radians => s_UnitsSuffixRadiansPerFoot,
                    _ => throw new System.Exception("Invalid angle change units type"),
                },
                _ => throw new System.Exception("Invalid distance units type"),
            };
        }

        public static string GetSpeedUnitsString() {
            return Preferences.SpeedUnits switch {
                SpeedUnitsType.MetersPerSecond => s_UnitsMetersPerSecond,
                SpeedUnitsType.KilometersPerHour => s_UnitsKilometersPerHour,
                SpeedUnitsType.MilesPerHour => s_UnitsMilesPerHour,
                _ => throw new System.Exception("Invalid speed units type"),
            };
        }

        public static string GetSpeedUnitsSuffix() {
            return Preferences.SpeedUnits switch {
                SpeedUnitsType.MetersPerSecond => s_UnitsSuffixMetersPerSecond,
                SpeedUnitsType.KilometersPerHour => s_UnitsSuffixKilometersPerHour,
                SpeedUnitsType.MilesPerHour => s_UnitsSuffixMilesPerHour,
                _ => throw new System.Exception("Invalid speed units type"),
            };
        }
    }
}
