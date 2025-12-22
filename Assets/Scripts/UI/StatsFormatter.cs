using Unity.Mathematics;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public static class StatsFormatter {

        public static string FormatPositionX(float x) {
            float displayValue = Units.DistanceToDisplay(x);
            return StatsStringPool.GetDecimalTwo(displayValue);
        }

        public static string FormatPositionY(float y) {
            float displayValue = Units.DistanceToDisplay(y);
            return StatsStringPool.GetDecimalTwo(displayValue);
        }

        public static string FormatPositionZ(float z) {
            float displayValue = Units.DistanceToDisplay(z);
            return StatsStringPool.GetDecimalTwo(displayValue);
        }

        public static string FormatRoll(float roll) {
            float displayValue = Units.AngleToDisplay(roll);
            return StatsStringPool.GetAngle(displayValue);
        }

        public static string FormatPitch(float pitch) {
            float displayValue = Units.AngleToDisplay(pitch);
            return StatsStringPool.GetAngle(displayValue);
        }

        public static string FormatYaw(float yaw) {
            float displayValue = Units.AngleToDisplay(yaw);
            return StatsStringPool.GetAngle(displayValue);
        }

        public static string FormatVelocity(float velocity) {
            bool isMetric = Units.GetSpeedUnitsString() == "km/h";
            return StatsStringPool.GetSpeed(velocity, isMetric);
        }

        public static string FormatRollSpeed(float rollSpeed) {
            float displayValue = Units.AnglePerTimeToDisplay(rollSpeed);
            return StatsStringPool.GetAnglePerTime(displayValue);
        }

        public static string FormatPitchSpeed(float pitchSpeed) {
            float displayValue = Units.AnglePerTimeToDisplay(pitchSpeed);
            return StatsStringPool.GetAnglePerTime(displayValue);
        }

        public static string FormatYawSpeed(float yawSpeed) {
            float displayValue = Units.AnglePerTimeToDisplay(yawSpeed);
            return StatsStringPool.GetAnglePerTime(displayValue);
        }

        public static string FormatNormalForce(float normalForce) {
            return StatsStringPool.GetForce(normalForce);
        }

        public static string FormatLateralForce(float lateralForce) {
            return StatsStringPool.GetForce(lateralForce);
        }

        public static string FormatCameraX(float x) {
            float displayValue = Units.DistanceToDisplay(x);
            return StatsStringPool.GetDecimalOne(displayValue);
        }

        public static string FormatCameraY(float y) {
            float displayValue = Units.DistanceToDisplay(y);
            return StatsStringPool.GetDecimalOne(displayValue);
        }

        public static string FormatCameraZ(float z) {
            float displayValue = Units.DistanceToDisplay(z);
            return StatsStringPool.GetDecimalOne(displayValue);
        }
    }
}