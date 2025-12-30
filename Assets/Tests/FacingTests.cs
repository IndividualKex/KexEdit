using System.IO;
using KexEdit.Document;
using KexEdit.Legacy;
using KexEdit.Persistence;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using NodeMeta = KexEdit.Document.NodeMeta;
using Coaster = KexEdit.Document.Document;
using TrackData = KexEdit.Track.Track;

namespace Tests {
    [TestFixture]
    [Category("Facing")]
    public class FacingTests {
        [Test]
        public void Veloci_AllPointsAreFacingForward() {
            var kexPath = "Assets/Tests/Assets/veloci.kex";
            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    TrackData.Build(in coaster, Allocator.TempJob, out var track);

                    try {
                        World world = new World("FacingTestWorld");
                        try {
                            var em = world.EntityManager;
                            var coasterEntity = em.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
                            em.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
                            em.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

                            var nodeIds = track.NodeToSection.GetKeyArray(Allocator.Temp);
                            foreach (var nodeId in nodeIds) {
                                var nodeEntity = em.CreateEntity(
                                    typeof(Node),
                                    typeof(CoasterReference),
                                    typeof(CorePointBuffer)
                                );

                                em.SetComponentData(nodeEntity, new Node { Id = nodeId });
                                em.SetComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

                                ulong facingKeyLocal = Coaster.InputKey(nodeId, NodeMeta.Facing);
                                int expectedFacing = coaster.Flags.TryGetValue(facingKeyLocal, out int f) ? f : 1;

                                var pointBuffer = em.GetBuffer<CorePointBuffer>(nodeEntity);

                                if (track.NodeToSection.TryGetValue(nodeId, out int sectionIdx)) {
                                    var section = track.Sections[sectionIdx];
                                    if (section.IsValid) {
                                        var path = track.Points.AsArray().GetSubArray(section.StartIndex, section.Length);

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
                            }
                            nodeIds.Dispose();
                        }
                        finally {
                            world.Dispose();
                        }
                    }
                    finally {
                        track.Dispose();
                    }
                }
                finally {
                    coaster.Dispose();
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
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    var writer = new KexEdit.Persistence.ChunkWriter(Allocator.TempJob);
                    try {
                        KexEdit.Persistence.CoasterSerializer.Write(writer, in coaster);
                        var serializedData = writer.ToArray();

                        try {
                            var reader = new KexEdit.Persistence.ChunkReader(serializedData);
                            var deserialized = KexEdit.Persistence.CoasterSerializer.Read(ref reader, Allocator.TempJob);

                            try {
                                int coasterFacingCount = 0;
                                int deserializedFacingCount = 0;
                                foreach (var kv in coaster.Flags) {
                                    Coaster.UnpackInputKey(kv.Key, out _, out int idx);
                                    if (idx == NodeMeta.Facing) coasterFacingCount++;
                                }
                                foreach (var kv in deserialized.Flags) {
                                    Coaster.UnpackInputKey(kv.Key, out _, out int idx);
                                    if (idx == NodeMeta.Facing) deserializedFacingCount++;
                                }
                                Assert.AreEqual(coasterFacingCount, deserializedFacingCount,
                                    "Facing count mismatch after round-trip");

                                foreach (var kv in coaster.Flags) {
                                    Coaster.UnpackInputKey(kv.Key, out uint nodeId, out int idx);
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
                buffer.Dispose();
            }
        }
    }
}
