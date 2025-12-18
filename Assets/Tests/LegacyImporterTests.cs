using KexEdit.Coaster;
using KexEdit.Core;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.LegacyImport;
using KexEdit.Nodes;
using NUnit.Framework;
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
    }
}
