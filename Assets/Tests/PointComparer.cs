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
            DynamicBuffer<CorePointBuffer> actual,
            List<GoldPointData> expected,
            float tolerance = BaseTolerance
        ) {
            Assert.AreEqual(expected.Count, actual.Length,
                $"Point count mismatch: expected {expected.Count}, got {actual.Length}");

            // Find max drift for analysis
            float maxDrift = 0f;
            int maxDriftIndex = 0;
            for (int i = 0; i < expected.Count; i++) {
                var drift = ComputeDrift(actual[i].ToPointData(), expected[i]);
                if (drift > maxDrift) {
                    maxDrift = drift;
                    maxDriftIndex = i;
                }
            }
            UnityEngine.Debug.Log($"[PointComparer] Max drift {maxDrift:G9} at index {maxDriftIndex}");

            foreach (int i in GetSampleIndices(expected.Count)) {
                float indexTolerance = BaseTolerance + TolerancePerStep * i;
                AssertPointMatch(actual[i].ToPointData(), expected[i], i, indexTolerance);
            }
        }

        private static float ComputeDrift(PointData actual, GoldPointData expected) {
            float maxDrift = 0f;
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.x - expected.heartPosition.x));
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.y - expected.heartPosition.y));
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.z - expected.heartPosition.z));
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
            AssertFloat3(actual.HeartPosition, expected.heartPosition, "HeartPosition", index, tolerance);
            AssertFloat3(actual.Direction, expected.direction, "Direction", index, tolerance);
            AssertFloat3(actual.Lateral, expected.lateral, "Lateral", index, tolerance);
            AssertFloat3(actual.Normal, expected.normal, "Normal", index, tolerance);

            AssertFloat(actual.Velocity, expected.velocity, "Velocity", index, tolerance);
            AssertFloat(actual.Energy, expected.energy, "Energy", index, tolerance);
            AssertFloat(actual.NormalForce, expected.normalForce, "NormalForce", index, tolerance);
            AssertFloat(actual.LateralForce, expected.lateralForce, "LateralForce", index, tolerance);

            AssertFloat(actual.HeartAdvance, expected.heartAdvance, "HeartAdvance", index, tolerance);
            AssertFloat(actual.SpineAdvance, expected.spineAdvance, "SpineAdvance", index, tolerance);
            AssertFloat(actual.AngleFromLast, expected.angleFromLast, "AngleFromLast", index, tolerance);
            AssertFloat(actual.PitchFromLast, expected.pitchFromLast, "PitchFromLast", index, tolerance);
            AssertFloat(actual.YawFromLast, expected.yawFromLast, "YawFromLast", index, tolerance);
            AssertFloat(actual.RollSpeed, expected.rollSpeed, "RollSpeed", index, tolerance);

            AssertFloat(actual.HeartArc, expected.heartArc, "HeartArc", index, tolerance);
            AssertFloat(actual.SpineArc, expected.spineArc, "SpineArc", index, tolerance);
            AssertFloat(actual.FrictionOrigin, expected.frictionOrigin, "FrictionOrigin", index, tolerance);

            AssertFloat(actual.HeartOffset, expected.heartOffset, "HeartOffset", index, tolerance);
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
