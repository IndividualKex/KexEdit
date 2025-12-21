using KexEdit.Coaster;
using KexEdit.Core;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.LegacyImport;
using KexEdit.Nodes;
using NUnit.Framework;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    public class LegacyImporterTests {
        [Test]
        public void Import_EmptyGraph_CreatesValidCoaster() {
            var serializedGraph = new SerializedGraph {
                Version = SerializationVersion.CURRENT,
                Nodes = new NativeArray<SerializedNode>(0, Allocator.Temp),
                Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp)
            };

            try {
                LegacyImporter.Import(in serializedGraph, Allocator.Temp, out var coaster);

                try {
                    Assert.AreEqual(0, coaster.Graph.NodeCount);
                    Assert.AreEqual(0, coaster.Graph.PortCount);
                    Assert.AreEqual(0, coaster.Graph.EdgeCount);
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                serializedGraph.Dispose();
            }
        }

        [Test]
        public void Import_SingleNode_ConvertsGraphStructure() {
            var node = new SerializedNode {
                Node = new KexEdit.Legacy.Node {
                    Id = 1,
                    Position = new float2(100f, 200f),
                    Type = KexEdit.Legacy.NodeType.ForceSection,
                    Priority = 0,
                    Selected = false,
                    Next = default,
                    Previous = default
                },
                Anchor = PointData.Create(10f),
                FieldFlags = NodeFieldFlags.None,
                BooleanFlags = NodeFlags.None,
                PropertyOverrides = default,
                SelectedProperties = default,
                CurveData = default,
                Duration = default,
                MeshFilePath = default,
                InputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                OutputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                RollSpeedKeyframes = new NativeArray<RollSpeedKeyframe>(0, Allocator.Temp),
                NormalForceKeyframes = new NativeArray<NormalForceKeyframe>(0, Allocator.Temp),
                LateralForceKeyframes = new NativeArray<LateralForceKeyframe>(0, Allocator.Temp),
                PitchSpeedKeyframes = new NativeArray<PitchSpeedKeyframe>(0, Allocator.Temp),
                YawSpeedKeyframes = new NativeArray<YawSpeedKeyframe>(0, Allocator.Temp),
                FixedVelocityKeyframes = new NativeArray<FixedVelocityKeyframe>(0, Allocator.Temp),
                HeartKeyframes = new NativeArray<HeartKeyframe>(0, Allocator.Temp),
                FrictionKeyframes = new NativeArray<FrictionKeyframe>(0, Allocator.Temp),
                ResistanceKeyframes = new NativeArray<ResistanceKeyframe>(0, Allocator.Temp),
                TrackStyleKeyframes = new NativeArray<TrackStyleKeyframe>(0, Allocator.Temp)
            };

            var serializedGraph = new SerializedGraph {
                Version = SerializationVersion.CURRENT,
                Nodes = new NativeArray<SerializedNode>(1, Allocator.Temp),
                Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp)
            };
            serializedGraph.Nodes[0] = node;

            try {
                LegacyImporter.Import(in serializedGraph, Allocator.Temp, out var coaster);

                try {
                    Assert.AreEqual(1, coaster.Graph.NodeCount);
                    Assert.AreEqual(1u, coaster.Graph.NodeIds[0]);
                    Assert.AreEqual((uint)KexEdit.Nodes.NodeType.Force, coaster.Graph.NodeTypes[0]);
                    Assert.AreEqual(new float2(100f, 200f), coaster.Graph.NodePositions[0]);

                    Assert.IsTrue(coaster.Anchors.ContainsKey(1u));
                    var anchor = coaster.Anchors[1u];
                    Assert.AreEqual(10f, anchor.Velocity, 0.001f);
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                node.Dispose();
                serializedGraph.Dispose();
            }
        }

        [Test]
        public void Import_NodeWithDuration_ConvertsDuration() {
            var node = new SerializedNode {
                Node = new KexEdit.Legacy.Node {
                    Id = 1,
                    Position = float2.zero,
                    Type = KexEdit.Legacy.NodeType.ForceSection,
                    Priority = 0,
                    Selected = false,
                    Next = default,
                    Previous = default
                },
                Anchor = PointData.Create(10f),
                FieldFlags = NodeFieldFlags.HasDuration,
                BooleanFlags = NodeFlags.None,
                PropertyOverrides = default,
                SelectedProperties = default,
                CurveData = default,
                Duration = new KexEdit.Legacy.Duration { Value = 5f, Type = KexEdit.Legacy.DurationType.Time },
                MeshFilePath = default,
                InputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                OutputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                RollSpeedKeyframes = new NativeArray<RollSpeedKeyframe>(0, Allocator.Temp),
                NormalForceKeyframes = new NativeArray<NormalForceKeyframe>(0, Allocator.Temp),
                LateralForceKeyframes = new NativeArray<LateralForceKeyframe>(0, Allocator.Temp),
                PitchSpeedKeyframes = new NativeArray<PitchSpeedKeyframe>(0, Allocator.Temp),
                YawSpeedKeyframes = new NativeArray<YawSpeedKeyframe>(0, Allocator.Temp),
                FixedVelocityKeyframes = new NativeArray<FixedVelocityKeyframe>(0, Allocator.Temp),
                HeartKeyframes = new NativeArray<HeartKeyframe>(0, Allocator.Temp),
                FrictionKeyframes = new NativeArray<FrictionKeyframe>(0, Allocator.Temp),
                ResistanceKeyframes = new NativeArray<ResistanceKeyframe>(0, Allocator.Temp),
                TrackStyleKeyframes = new NativeArray<TrackStyleKeyframe>(0, Allocator.Temp)
            };

            var serializedGraph = new SerializedGraph {
                Version = SerializationVersion.CURRENT,
                Nodes = new NativeArray<SerializedNode>(1, Allocator.Temp),
                Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp)
            };
            serializedGraph.Nodes[0] = node;

            try {
                LegacyImporter.Import(in serializedGraph, Allocator.Temp, out var coaster);

                try {
                    Assert.IsTrue(coaster.Durations.ContainsKey(1u));
                    var duration = coaster.Durations[1u];
                    Assert.AreEqual(5f, duration.Value, 0.001f);
                    Assert.AreEqual(KexEdit.Coaster.DurationType.Time, duration.Type);
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                node.Dispose();
                serializedGraph.Dispose();
            }
        }

        [Test]
        public void Import_NodeWithSteering_AddsSteering() {
            var node = new SerializedNode {
                Node = new KexEdit.Legacy.Node {
                    Id = 1,
                    Position = float2.zero,
                    Type = KexEdit.Legacy.NodeType.GeometricSection,
                    Priority = 0,
                    Selected = false,
                    Next = default,
                    Previous = default
                },
                Anchor = PointData.Create(10f),
                FieldFlags = NodeFieldFlags.HasSteering,
                BooleanFlags = NodeFlags.Steering,
                PropertyOverrides = default,
                SelectedProperties = default,
                CurveData = default,
                Duration = default,
                MeshFilePath = default,
                InputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                OutputPorts = new NativeArray<SerializedPort>(0, Allocator.Temp),
                RollSpeedKeyframes = new NativeArray<RollSpeedKeyframe>(0, Allocator.Temp),
                NormalForceKeyframes = new NativeArray<NormalForceKeyframe>(0, Allocator.Temp),
                LateralForceKeyframes = new NativeArray<LateralForceKeyframe>(0, Allocator.Temp),
                PitchSpeedKeyframes = new NativeArray<PitchSpeedKeyframe>(0, Allocator.Temp),
                YawSpeedKeyframes = new NativeArray<YawSpeedKeyframe>(0, Allocator.Temp),
                FixedVelocityKeyframes = new NativeArray<FixedVelocityKeyframe>(0, Allocator.Temp),
                HeartKeyframes = new NativeArray<HeartKeyframe>(0, Allocator.Temp),
                FrictionKeyframes = new NativeArray<FrictionKeyframe>(0, Allocator.Temp),
                ResistanceKeyframes = new NativeArray<ResistanceKeyframe>(0, Allocator.Temp),
                TrackStyleKeyframes = new NativeArray<TrackStyleKeyframe>(0, Allocator.Temp)
            };

            var serializedGraph = new SerializedGraph {
                Version = SerializationVersion.CURRENT,
                Nodes = new NativeArray<SerializedNode>(1, Allocator.Temp),
                Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp)
            };
            serializedGraph.Nodes[0] = node;

            try {
                LegacyImporter.Import(in serializedGraph, Allocator.Temp, out var coaster);

                try {
                    Assert.IsTrue(coaster.Steering.Contains(1u));
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                node.Dispose();
                serializedGraph.Dispose();
            }
        }

        [Test]
        [Category("Golden")]
        public void Deserialize_AllTypes_AnchorMatchesGold() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var kexPath = "Assets/Tests/Assets/all_types.kex";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    // Use section 1 which has non-zero advance/arc values
                    var section = gold.sections[1];
                    var goldAnchor = section.inputs.anchor;
                    uint nodeId = section.nodeId;

                    // Find matching node in deserialized graph
                    SerializedNode? matchingNode = null;
                    for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                        if (serializedGraph.Nodes[i].Node.Id == nodeId) {
                            matchingNode = serializedGraph.Nodes[i];
                            break;
                        }
                    }

                    Assert.IsTrue(matchingNode.HasValue, $"Node {nodeId} not found in deserialized graph");
                    var deserializedAnchor = matchingNode.Value.Anchor;

                    // Log values for debugging
                    UnityEngine.Debug.Log($"=== DESERIALIZATION TEST: Node {nodeId} ({section.nodeType}) ===");
                    UnityEngine.Debug.Log($"Gold heartAdvance: {goldAnchor.heartAdvance}, spineAdvance: {goldAnchor.spineAdvance}");
                    UnityEngine.Debug.Log($"Deser HeartAdvance: {deserializedAnchor.HeartAdvance}, SpineAdvance: {deserializedAnchor.SpineAdvance}");
                    UnityEngine.Debug.Log($"Gold heartArc: {goldAnchor.heartArc}, spineArc: {goldAnchor.spineArc}");
                    UnityEngine.Debug.Log($"Deser HeartArc: {deserializedAnchor.HeartArc}, SpineArc: {deserializedAnchor.SpineArc}");
                    UnityEngine.Debug.Log($"Gold heartOffset: {goldAnchor.heartOffset}");
                    UnityEngine.Debug.Log($"Deser HeartOffset: {deserializedAnchor.HeartOffset}");

                    // Assert key fields match
                    Assert.AreEqual(goldAnchor.velocity, deserializedAnchor.Velocity, 0.0001f, "Velocity mismatch");
                    Assert.AreEqual(goldAnchor.heartAdvance, deserializedAnchor.HeartAdvance, 0.0001f, "HeartAdvance mismatch");
                    Assert.AreEqual(goldAnchor.spineAdvance, deserializedAnchor.SpineAdvance, 0.0001f, "SpineAdvance mismatch");
                    Assert.AreEqual(goldAnchor.heartArc, deserializedAnchor.HeartArc, 0.0001f, "HeartArc mismatch");
                    Assert.AreEqual(goldAnchor.spineArc, deserializedAnchor.SpineArc, 0.0001f, "SpineArc mismatch");
                    Assert.AreEqual(goldAnchor.heartOffset, deserializedAnchor.HeartOffset, 0.0001f, "HeartOffset mismatch");
                    Assert.AreEqual(goldAnchor.frictionOrigin, deserializedAnchor.FrictionOrigin, 0.0001f, "FrictionOrigin mismatch");
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
