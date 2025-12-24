using System.IO;
using KexEdit.Coaster;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using NodeMeta = KexEdit.Coaster.NodeMeta;
using CoasterAggregate = KexEdit.Coaster.Coaster;

namespace Tests {
    [TestFixture]
    [Category("Facing")]
    public class FacingTests {
        [Test]
        public void Veloci_LegacyImport_PreservesFacing() {
            var kexPath = "Assets/Tests/Assets/veloci.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                            var serializedNode = serializedGraph.Nodes[i];
                            uint nodeId = serializedNode.Node.Id;

                            int expectedFacing = serializedNode.Anchor.Facing;
                            ulong facingKey = CoasterAggregate.InputKey(nodeId, NodeMeta.Facing);
                            int actualFacing = coaster.Flags.TryGetValue(facingKey, out int f) ? f : 1;

                            Assert.AreEqual(expectedFacing, actualFacing,
                                $"Node {nodeId} facing mismatch. Expected {expectedFacing}, got {actualFacing}");
                        }

                        UnityEngine.Debug.Log($"Facing validation passed for {serializedGraph.Nodes.Length} nodes");
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

        [Test]
        public void Veloci_AllPointsAreFacingForward() {
            var kexPath = "Assets/Tests/Assets/veloci.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.TempJob);

                        try {
                            World world = new World("FacingTestWorld");
                            try {
                                var em = world.EntityManager;
                                var coasterEntity = em.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
                                em.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

                                foreach (var nodeId in result.Paths.GetKeyArray(Allocator.Temp)) {
                                    var nodeEntity = em.CreateEntity(
                                        typeof(Node),
                                        typeof(CoasterReference),
                                        typeof(CorePointBuffer)
                                    );

                                    em.SetComponentData(nodeEntity, new Node { Id = nodeId });
                                    em.SetComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

                                    ulong facingKeyLocal = CoasterAggregate.InputKey(nodeId, NodeMeta.Facing);
                                    int expectedFacing = coaster.Flags.TryGetValue(facingKeyLocal, out int f) ? f : 1;

                                    var pointBuffer = em.GetBuffer<CorePointBuffer>(nodeEntity);
                                    var path = result.Paths[nodeId];

                                    if (path.Length > 0) {
                                        var firstPoint = path[0];
                                        CorePointBuffer.CreateFirst(in firstPoint, expectedFacing, out var first);
                                        pointBuffer.Add(first);

                                        for (int i = 1; i < path.Length; i++) {
                                            var currPoint = path[i];
                                            var prevPoint = path[i - 1];
                                            CorePointBuffer.Create(in currPoint, in prevPoint, expectedFacing, out var point);
                                            pointBuffer.Add(point);
                                        }
                                    }

                                    Assert.AreEqual(path.Length, pointBuffer.Length,
                                        $"Node {nodeId}: path length mismatch");

                                    for (int i = 0; i < pointBuffer.Length; i++) {
                                        Assert.AreEqual(expectedFacing, pointBuffer[i].Facing,
                                            $"Node {nodeId} point[{i}]: facing mismatch. Expected {expectedFacing}, got {pointBuffer[i].Facing}");
                                    }

                                    UnityEngine.Debug.Log($"Node {nodeId}: {pointBuffer.Length} points, all facing={expectedFacing}");
                                }
                            }
                            finally {
                                world.Dispose();
                            }
                        }
                        finally {
                            result.Dispose();
                        }
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

        [Test]
        public void Veloci_Serialization_RoundTripPreservesFacing() {
            var kexPath = "Assets/Tests/Assets/veloci.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        var writer = new KexEdit.Persistence.ChunkWriter(Allocator.TempJob);
                        try {
                            KexEdit.Persistence.CoasterSerializer.Write(writer, in coaster);
                            var serializedData = writer.ToArray();

                            try {
                                var reader = new KexEdit.Persistence.ChunkReader(serializedData);
                                var deserialized = KexEdit.Persistence.CoasterSerializer.Read(reader, Allocator.TempJob);

                                try {
                                    int coasterFacingCount = 0;
                                    int deserializedFacingCount = 0;
                                    foreach (var kv in coaster.Flags) {
                                        CoasterAggregate.UnpackInputKey(kv.Key, out _, out int idx);
                                        if (idx == NodeMeta.Facing) coasterFacingCount++;
                                    }
                                    foreach (var kv in deserialized.Flags) {
                                        CoasterAggregate.UnpackInputKey(kv.Key, out _, out int idx);
                                        if (idx == NodeMeta.Facing) deserializedFacingCount++;
                                    }
                                    Assert.AreEqual(coasterFacingCount, deserializedFacingCount,
                                        "Facing count mismatch after round-trip");

                                    foreach (var kv in coaster.Flags) {
                                        CoasterAggregate.UnpackInputKey(kv.Key, out uint nodeId, out int idx);
                                        if (idx != NodeMeta.Facing) continue;
                                        Assert.IsTrue(deserialized.Flags.TryGetValue(kv.Key, out int deserializedFacing),
                                            $"Node {nodeId} facing not found after round-trip");
                                        Assert.AreEqual(kv.Value, deserializedFacing,
                                            $"Node {nodeId} facing mismatch. Expected {kv.Value}, got {deserializedFacing}");
                                    }

                                    UnityEngine.Debug.Log($"Round-trip serialization preserved facing for {coasterFacingCount} nodes");
                                }
                                finally {
                                    deserialized.Dispose();
                                }
                            }
                            finally {
                                serializedData.Dispose();
                            }
                        }
                        finally {
                            writer.Dispose();
                        }
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
    }
}
