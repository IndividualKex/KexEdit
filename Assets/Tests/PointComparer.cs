using KexEdit.Legacy;
using System.Collections.Generic;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Tests {
    public static class PointComparer {
        private const float BaseTolerance = 1e-3f;
        private const float TolerancePerStep = 1e-5f;
        private const int BoundaryCount = 5;
        private const int SampleInterval = 50;

        public static void AssertPointsMatch(
            DynamicBuffer<Point> actual,
            List<GoldPointData> expected,
            float tolerance = BaseTolerance
        ) {
            Assert.AreEqual(expected.Count, actual.Length,
                $"Point count mismatch: expected {expected.Count}, got {actual.Length}");

            // Find max drift for analysis
            float maxDrift = 0f;
            int maxDriftIndex = 0;
            for (int i = 0; i < expected.Count; i++) {
                var drift = ComputeDrift(actual[i].Value, expected[i]);
                if (drift > maxDrift) {
                    maxDrift = drift;
                    maxDriftIndex = i;
                }
            }
            UnityEngine.Debug.Log($"[PointComparer] Max drift {maxDrift:G9} at index {maxDriftIndex}");

            foreach (int i in GetSampleIndices(expected.Count)) {
                float indexTolerance = BaseTolerance + TolerancePerStep * i;
                AssertPointMatch(actual[i].Value, expected[i], i, indexTolerance);
            }
        }

        private static float ComputeDrift(PointData actual, GoldPointData expected) {
            float maxDrift = 0f;
            maxDrift = math.max(maxDrift, math.abs(actual.Position.x - expected.position.x));
            maxDrift = math.max(maxDrift, math.abs(actual.Position.y - expected.position.y));
            maxDrift = math.max(maxDrift, math.abs(actual.Position.z - expected.position.z));
            maxDrift = math.max(maxDrift, math.abs(actual.Velocity - expected.velocity));
            maxDrift = math.max(maxDrift, math.abs(actual.Energy - expected.energy));
            return maxDrift;
        }

        private static IEnumerable<int> GetSampleIndices(int count) {
            var yielded = new HashSet<int>();

            // First N points
            for (int i = 0; i < math.min(BoundaryCount, count); i++) {
                if (yielded.Add(i)) yield return i;
            }

            // Last N points
            for (int i = math.max(0, count - BoundaryCount); i < count; i++) {
                if (yielded.Add(i)) yield return i;
            }

            // Every Mth point
            for (int i = SampleInterval; i < count - BoundaryCount; i += SampleInterval) {
                if (yielded.Add(i)) yield return i;
            }
        }

        public static void AssertPointMatch(
            PointData actual,
            GoldPointData expected,
            int index,
            float tolerance = BaseTolerance
        ) {
            AssertFloat3(actual.Position, expected.position, "Position", index, tolerance);
            AssertFloat3(actual.Direction, expected.direction, "Direction", index, tolerance);
            AssertFloat3(actual.Lateral, expected.lateral, "Lateral", index, tolerance);
            AssertFloat3(actual.Normal, expected.normal, "Normal", index, tolerance);

            AssertFloat(actual.Velocity, expected.velocity, "Velocity", index, tolerance);
            AssertFloat(actual.Energy, expected.energy, "Energy", index, tolerance);
            AssertFloat(actual.NormalForce, expected.normalForce, "NormalForce", index, tolerance);
            AssertFloat(actual.LateralForce, expected.lateralForce, "LateralForce", index, tolerance);

            AssertFloat(actual.DistanceFromLast, expected.distanceFromLast, "DistanceFromLast", index, tolerance);
            AssertFloat(actual.HeartDistanceFromLast, expected.heartDistanceFromLast, "HeartDistanceFromLast", index, tolerance);
            AssertFloat(actual.AngleFromLast, expected.angleFromLast, "AngleFromLast", index, tolerance);
            AssertFloat(actual.PitchFromLast, expected.pitchFromLast, "PitchFromLast", index, tolerance);
            AssertFloat(actual.YawFromLast, expected.yawFromLast, "YawFromLast", index, tolerance);
            AssertFloat(actual.RollSpeed, expected.rollSpeed, "RollSpeed", index, tolerance);

            AssertFloat(actual.TotalLength, expected.totalLength, "TotalLength", index, tolerance);
            AssertFloat(actual.TotalHeartLength, expected.totalHeartLength, "TotalHeartLength", index, tolerance);
            AssertFloat(actual.FrictionCompensation, expected.frictionCompensation, "FrictionCompensation", index, tolerance);

            AssertFloat(actual.Heart, expected.heart, "Heart", index, tolerance);
            AssertFloat(actual.Friction, expected.friction, "Friction", index, tolerance);
            AssertFloat(actual.Resistance, expected.resistance, "Resistance", index, tolerance);

            Assert.AreEqual(expected.facing, actual.Facing,
                $"Point[{index}].Facing: expected {expected.facing}, got {actual.Facing}");
        }

        private static void AssertFloat(float actual, float expected, string field, int index, float tolerance) {
            Assert.AreEqual(expected, actual, tolerance,
                $"Point[{index}].{field}: expected {expected:G9}, got {actual:G9}, diff {math.abs(expected - actual):G9}");
        }

        private static void AssertFloat3(float3 actual, GoldVec3 expected, string field, int index, float tolerance) {
            var exp = new float3(expected.x, expected.y, expected.z);
            AssertFloat(actual.x, exp.x, $"{field}.x", index, tolerance);
            AssertFloat(actual.y, exp.y, $"{field}.y", index, tolerance);
            AssertFloat(actual.z, exp.z, $"{field}.z", index, tolerance);
        }
    }
}
