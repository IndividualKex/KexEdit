using System.IO;
using System.Text;
using KexEdit.Coaster;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Nodes;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Tests {
    [TestFixture]
    [Category("Validation")]
    public class CoasterValidationTests {
        /// <summary>
        /// Exports Coaster aggregate state to JSON for validation with Python script.
        /// This test validates that LegacyImporter correctly populates the Coaster
        /// aggregate from .kex file data.
        /// </summary>
        [Test]
        [TestCase("veloci")]
        [TestCase("shuttle")]
        [TestCase("all_types")]
        public void ExportCoasterState_ForPythonValidation(string name) {
            var kexPath = $"Assets/Tests/Assets/{name}.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        string jsonPath = $"Assets/Tests/Assets/{name}.actual.json";
                        ExportCoasterToJson(coaster, jsonPath);
                        Debug.Log($"Exported Coaster state to: {jsonPath}");
                        Debug.Log($"Run validation: python tools/validate_coaster_state.py {kexPath} {jsonPath}");
                    }
                    finally {
                        coaster.Dispose();
                    }
                }
                finally {
                    serializedGraph.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// Round-trip test: .kex -> Coaster -> serialize -> deserialize -> compare
        /// </summary>
        [Test]
        [TestCase("veloci")]
        [TestCase("shuttle")]
        [TestCase("all_types")]
        public void CoasterRoundTrip_PreservesAllData(string name) {
            var kexPath = $"Assets/Tests/Assets/{name}.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var original);

                    try {
                        // Serialize to new format
                        using var writer = new KexEdit.Persistence.ChunkWriter(Allocator.Temp);
                        KexEdit.Persistence.CoasterSerializer.Write(writer, in original);
                        var serializedData = writer.ToArray();

                        // Deserialize back
                        using var reader = new KexEdit.Persistence.ChunkReader(serializedData);
                        var restored = KexEdit.Persistence.CoasterSerializer.Read(reader, Allocator.TempJob);

                        try {
                            // Compare
                            AssertCoastersEqual(original, restored);
                        }
                        finally {
                            restored.Dispose();
                        }
                    }
                    finally {
                        original.Dispose();
                    }
                }
                finally {
                    serializedGraph.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void ExportCoasterToJson(in KexEdit.Coaster.Coaster coaster, string path) {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Graph structure
            sb.AppendLine("  \"node_ids\": [");
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                sb.Append($"    {coaster.Graph.NodeIds[i]}");
                if (i < coaster.Graph.NodeIds.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            sb.AppendLine("  \"edges\": [");
            for (int i = 0; i < coaster.Graph.EdgeIds.Length; i++) {
                var srcId = coaster.Graph.EdgeSources[i];
                var tgtId = coaster.Graph.EdgeTargets[i];
                sb.Append($"    [{srcId}, {tgtId}]");
                if (i < coaster.Graph.EdgeIds.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // Scalars
            sb.AppendLine("  \"scalars\": {");
            var scalarEnumerator = coaster.Scalars.GetEnumerator();
            bool firstScalar = true;
            while (scalarEnumerator.MoveNext()) {
                if (!firstScalar) sb.AppendLine(",");
                firstScalar = false;
                sb.Append($"    \"{scalarEnumerator.Current.Key}\": {scalarEnumerator.Current.Value}");
            }
            scalarEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  },");

            // Vectors
            sb.AppendLine("  \"vectors\": {");
            var vectorEnumerator = coaster.Vectors.GetEnumerator();
            bool firstVector = true;
            while (vectorEnumerator.MoveNext()) {
                if (!firstVector) sb.AppendLine(",");
                firstVector = false;
                var vec = vectorEnumerator.Current.Value;
                sb.Append($"    \"{vectorEnumerator.Current.Key}\": [{vec.x}, {vec.y}, {vec.z}]");
            }
            vectorEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  },");


            // Durations
            sb.AppendLine("  \"durations\": {");
            var durationEnumerator = coaster.Durations.GetEnumerator();
            bool firstDuration = true;
            while (durationEnumerator.MoveNext()) {
                if (!firstDuration) sb.AppendLine(",");
                firstDuration = false;
                var dur = durationEnumerator.Current.Value;
                sb.Append($"    \"{durationEnumerator.Current.Key}\": {{\"value\": {dur.Value}, \"type\": {(int)dur.Type}}}");
            }
            durationEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  },");

            // Keyframes
            sb.AppendLine("  \"keyframes\": {");
            var keyframeEnumerator = coaster.Keyframes.Ranges.GetEnumerator();
            bool firstKeyframe = true;
            while (keyframeEnumerator.MoveNext()) {
                if (!firstKeyframe) sb.AppendLine(",");
                firstKeyframe = false;

                KexEdit.Nodes.Storage.KeyframeStore.UnpackKey(keyframeEnumerator.Current.Key, out uint nodeId, out PropertyId propertyId);
                var range = keyframeEnumerator.Current.Value;
                sb.Append($"    \"({nodeId}, {propertyId})\": {range.y}");
            }
            keyframeEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  },");

            // Steering
            sb.AppendLine("  \"steering\": [");
            var steeringEnumerator = coaster.Steering.GetEnumerator();
            bool firstSteering = true;
            while (steeringEnumerator.MoveNext()) {
                if (!firstSteering) sb.Append(", ");
                firstSteering = false;
                sb.Append(steeringEnumerator.Current);
            }
            steeringEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  ],");

            // Driven
            sb.AppendLine("  \"driven\": [");
            var drivenEnumerator = coaster.Driven.GetEnumerator();
            bool firstDriven = true;
            while (drivenEnumerator.MoveNext()) {
                if (!firstDriven) sb.Append(", ");
                firstDriven = false;
                sb.Append(drivenEnumerator.Current);
            }
            drivenEnumerator.Dispose();
            sb.AppendLine();
            sb.AppendLine("  ]");

            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
        }

        private static void AssertCoastersEqual(in KexEdit.Coaster.Coaster expected, in KexEdit.Coaster.Coaster actual) {
            // Graph structure
            Assert.AreEqual(expected.Graph.NodeIds.Length, actual.Graph.NodeIds.Length, "Node count mismatch");
            Assert.AreEqual(expected.Graph.EdgeIds.Length, actual.Graph.EdgeIds.Length, "Edge count mismatch");

            for (int i = 0; i < expected.Graph.NodeIds.Length; i++) {
                Assert.AreEqual(expected.Graph.NodeIds[i], actual.Graph.NodeIds[i], $"Node ID mismatch at index {i}");
                Assert.AreEqual(expected.Graph.NodeTypes[i], actual.Graph.NodeTypes[i], $"Node type mismatch at index {i}");
            }

            // Scalars
            Assert.AreEqual(expected.Scalars.Count, actual.Scalars.Count, "Scalar count mismatch");
            foreach (var kv in expected.Scalars) {
                Assert.IsTrue(actual.Scalars.TryGetValue(kv.Key, out float actualValue), $"Scalar key {kv.Key} not found");
                Assert.AreEqual(kv.Value, actualValue, 0.0001f, $"Scalar value mismatch for key {kv.Key}");
            }

            // Vectors
            Assert.AreEqual(expected.Vectors.Count, actual.Vectors.Count, "Vector count mismatch");
            foreach (var kv in expected.Vectors) {
                Assert.IsTrue(actual.Vectors.TryGetValue(kv.Key, out var actualValue), $"Vector key {kv.Key} not found");
                Assert.AreEqual(kv.Value.x, actualValue.x, 0.0001f, $"Vector.x mismatch for key {kv.Key}");
                Assert.AreEqual(kv.Value.y, actualValue.y, 0.0001f, $"Vector.y mismatch for key {kv.Key}");
                Assert.AreEqual(kv.Value.z, actualValue.z, 0.0001f, $"Vector.z mismatch for key {kv.Key}");
            }

            // Durations
            Assert.AreEqual(expected.Durations.Count, actual.Durations.Count, "Duration count mismatch");
            foreach (var kv in expected.Durations) {
                Assert.IsTrue(actual.Durations.TryGetValue(kv.Key, out var actualValue), $"Duration key {kv.Key} not found");
                Assert.AreEqual(kv.Value.Value, actualValue.Value, 0.0001f, $"Duration value mismatch for key {kv.Key}");
                Assert.AreEqual(kv.Value.Type, actualValue.Type, $"Duration type mismatch for key {kv.Key}");
            }

            // Keyframes
            Assert.AreEqual(expected.Keyframes.Ranges.Count, actual.Keyframes.Ranges.Count, "Keyframe count mismatch");
            foreach (var kv in expected.Keyframes.Ranges) {
                Assert.IsTrue(actual.Keyframes.Ranges.TryGetValue(kv.Key, out var actualRange), $"Keyframe key {kv.Key} not found");
                Assert.AreEqual(kv.Value.y, actualRange.y, $"Keyframe count mismatch for key {kv.Key}");
            }

            // Steering
            Assert.AreEqual(expected.Steering.Count, actual.Steering.Count, "Steering count mismatch");
            foreach (var nodeId in expected.Steering) {
                Assert.IsTrue(actual.Steering.Contains(nodeId), $"Steering node {nodeId} not found");
            }

            // Driven
            Assert.AreEqual(expected.Driven.Count, actual.Driven.Count, "Driven count mismatch");
            foreach (var nodeId in expected.Driven) {
                Assert.IsTrue(actual.Driven.Contains(nodeId), $"Driven node {nodeId} not found");
            }
        }
    }
}
