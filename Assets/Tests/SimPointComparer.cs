using System.Collections.Generic;
using KexEdit.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    public static class SimPointComparer {
        private const float BaseTolerance = 1e-3f;
        private const float TolerancePerStep = 1e-5f;
        private const int BoundaryCount = 5;
        private const int SampleInterval = 50;

        public static void AssertMatchesGold(
            NativeList<Point> actual,
            List<GoldPointData> expected,
            int cumulativeOffset = 0
        ) {
            Assert.AreEqual(expected.Count, actual.Length,
                $"Point count mismatch: expected {expected.Count}, got {actual.Length}");

            int firstDivergence = -1;
            float maxDrift = 0f;
            int maxDriftIndex = 0;
            for (int i = 0; i < expected.Count; i++) {
                float tolerance = BaseTolerance + TolerancePerStep * (cumulativeOffset + i);
                var drift = ComputeDrift(actual[i], expected[i]);
                if (drift > maxDrift) {
                    maxDrift = drift;
                    maxDriftIndex = i;
                }
                if (firstDivergence < 0 && drift > tolerance) {
                    firstDivergence = i;
                }
            }

            UnityEngine.Debug.Log($"=== DRIFT ANALYSIS (offset={cumulativeOffset}) ===");
            UnityEngine.Debug.Log($"First divergence at index {firstDivergence}");
            UnityEngine.Debug.Log($"Max drift {maxDrift:G9} at index {maxDriftIndex}");

            UnityEngine.Debug.Log($"=== FIRST 20 POINTS ===");
            for (int i = 0; i < math.min(20, expected.Count); i++) {
                LogPointComparison(actual[i], expected[i], i, cumulativeOffset);
            }

            UnityEngine.Debug.Log($"=== SAMPLE POINTS ===");
            int[] sampleIndices = { 50, 100, 200, 500, 1000, 1500 };
            foreach (int i in sampleIndices) {
                if (i < expected.Count) {
                    LogPointComparison(actual[i], expected[i], i, cumulativeOffset);
                }
            }

            if (firstDivergence >= 0 && firstDivergence > 20) {
                UnityEngine.Debug.Log($"=== AROUND FIRST DIVERGENCE ===");
                int start = math.max(0, firstDivergence - 5);
                int end = math.min(expected.Count, firstDivergence + 6);
                for (int i = start; i < end; i++) {
                    LogPointComparison(actual[i], expected[i], i, cumulativeOffset);
                }
            }

            foreach (int i in GetSampleIndices(expected.Count)) {
                float tolerance = BaseTolerance + TolerancePerStep * (cumulativeOffset + i);
                AssertPointMatchesGold(actual[i], expected[i], i, tolerance);
            }
        }

        private static float ComputeDrift(Point actual, GoldPointData expected) {
            float maxDrift = 0f;
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.x - expected.heartPosition.x));
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.y - expected.heartPosition.y));
            maxDrift = math.max(maxDrift, math.abs(actual.HeartPosition.z - expected.heartPosition.z));
            maxDrift = math.max(maxDrift, math.abs(actual.Velocity - expected.velocity));
            maxDrift = math.max(maxDrift, math.abs(actual.Energy - expected.energy));
            return maxDrift;
        }

        private static void LogPointComparison(Point actual, GoldPointData expected, int index, int cumulativeOffset) {
            var marker = ComputeDrift(actual, expected) > BaseTolerance + TolerancePerStep * (cumulativeOffset + index) ? ">>> " : "    ";
            UnityEngine.Debug.Log($"{marker}[{index}] Pos: ({actual.HeartPosition.x:F6}, {actual.HeartPosition.y:F6}, {actual.HeartPosition.z:F6}) vs ({expected.heartPosition.x:F6}, {expected.heartPosition.y:F6}, {expected.heartPosition.z:F6})");
            UnityEngine.Debug.Log($"{marker}[{index}] Vel: {actual.Velocity:F6} vs {expected.velocity:F6}, diff={math.abs(actual.Velocity - expected.velocity):G6}");
            UnityEngine.Debug.Log($"{marker}[{index}] Energy: {actual.Energy:F6} vs {expected.energy:F6}, diff={math.abs(actual.Energy - expected.energy):G6}");
            UnityEngine.Debug.Log($"{marker}[{index}] HeartArc: {actual.HeartArc:F6} vs {expected.heartArc:F6}");
        }

        private static IEnumerable<int> GetSampleIndices(int count) {
            var yielded = new HashSet<int>();

            for (int i = 0; i < math.min(BoundaryCount, count); i++) {
                if (yielded.Add(i)) yield return i;
            }

            for (int i = math.max(0, count - BoundaryCount); i < count; i++) {
                if (yielded.Add(i)) yield return i;
            }

            for (int i = SampleInterval; i < count - BoundaryCount; i += SampleInterval) {
                if (yielded.Add(i)) yield return i;
            }
        }

        public static void AssertPointMatchesGold(
            Point actual,
            GoldPointData expected,
            int index,
            float tolerance
        ) {
            AssertFloat3(actual.HeartPosition, expected.heartPosition, "HeartPosition", index, tolerance);
            AssertFloat3(actual.Direction, expected.direction, "Direction", index, tolerance);
            AssertFloat3(actual.Lateral, expected.lateral, "Lateral", index, tolerance);
            AssertFloat3(actual.Normal, expected.normal, "Normal", index, tolerance);

            AssertFloat(actual.Velocity, expected.velocity, "Velocity", index, tolerance);
            AssertFloat(actual.Energy, expected.energy, "Energy", index, tolerance);

            AssertFloat(actual.NormalForce, expected.normalForce, "NormalForce", index, tolerance);
            AssertFloat(actual.LateralForce, expected.lateralForce, "LateralForce", index, tolerance);

            AssertFloat(actual.HeartArc, expected.heartArc, "HeartArc", index, tolerance);
            AssertFloat(actual.SpineArc, expected.spineArc, "SpineArc", index, tolerance);

            AssertFloat(actual.RollSpeed, expected.rollSpeed, "RollSpeed", index, tolerance);
            AssertFloat(actual.HeartOffset, expected.heartOffset, "HeartOffset", index, tolerance);
            AssertFloat(actual.Friction, expected.friction, "Friction", index, tolerance);
            AssertFloat(actual.Resistance, expected.resistance, "Resistance", index, tolerance);
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
