using KexEdit.Coaster;
using KexEdit.Core;
using KexEdit.Legacy.Serialization;
using KexEdit.LegacyImport;
using NUnit.Framework;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class CoasterGoldTests {
        [Test]
        public void Shuttle_LoadAndEvaluate_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var kexPath = "Assets/Tests/Assets/shuttle.kex";

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
                            Assert.Greater(result.Paths.Count, 0, "No paths generated");
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
        public void Veloci_LoadAndEvaluate_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
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
                            Assert.Greater(result.Paths.Count, 0, "No paths generated");
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
    }
}
