using KexEdit.Spline;
using KexEdit.Spline.Resampling;
using KexEdit.Document;
using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Anchor;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;
using NodeMeta = KexEdit.Document.NodeMeta;
using TrackData = KexEdit.Track.Track;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoasterSplineSyncTests {
        [Test]
        public void SplineSync_ForceNode_ProducesSplinePoints() {
            var coaster = Coaster.Create(Allocator.Temp);
            try {
                uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
                coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

                uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out _, Allocator.Temp);
                coaster.Scalars[Coaster.InputKey(forceId, NodeMeta.Duration)] = 1f;

                coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);

                anchorOutputs.Dispose();
                forceInputs.Dispose();

                TrackData.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
                try {
                    Assert.IsTrue(track.NodeToSection.TryGetValue(forceId, out int sectionIdx), "Force node should have section");
                    var section = track.Sections[sectionIdx];
                    Assert.IsTrue(section.IsValid, "Section should be valid");

                    var path = track.Points.AsArray().GetSubArray(section.StartIndex, section.Length);
                    Assert.Greater(path.Length, 1, "Path should have multiple points");

                    var splineOutput = new NativeList<SplinePoint>(256, Allocator.Temp);
                    try {
                        SplineResampler.Resample(path, 0.1f, ref splineOutput);

                        Assert.Greater(splineOutput.Length, 1, "Spline should have multiple points");

                        for (int i = 1; i < splineOutput.Length; i++) {
                            float arcDelta = splineOutput[i].Arc - splineOutput[i - 1].Arc;
                            Assert.That(arcDelta, Is.EqualTo(0.1f).Within(0.02f),
                                $"Spline point {i} should be ~0.1m from previous");
                        }

                        Assert.That(splineOutput[0].Arc, Is.EqualTo(path[0].SpineArc).Within(0.01f),
                            "First spline point should match first path point arc");
                    }
                    finally {
                        splineOutput.Dispose();
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

        [Test]
        public void SplineSync_GeometricNode_ProducesSplinePoints() {
            var coaster = Coaster.Create(Allocator.Temp);
            try {
                uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
                coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

                uint geoId = coaster.Graph.CreateNode(NodeType.Geometric, new float2(100f, 0f), out var geoInputs, out _, Allocator.Temp);
                coaster.Scalars[Coaster.InputKey(geoId, NodeMeta.Duration)] = 2f;

                coaster.Graph.AddEdge(anchorOutputs[0], geoInputs[0]);

                anchorOutputs.Dispose();
                geoInputs.Dispose();

                TrackData.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
                try {
                    Assert.IsTrue(track.NodeToSection.TryGetValue(geoId, out int sectionIdx), "Geometric node should have section");
                    var section = track.Sections[sectionIdx];
                    var path = track.Points.AsArray().GetSubArray(section.StartIndex, section.Length);

                    var splineOutput = new NativeList<SplinePoint>(256, Allocator.Temp);
                    try {
                        SplineResampler.Resample(path, 0.1f, ref splineOutput);

                        Assert.Greater(splineOutput.Length, 1, "Spline should have multiple points");

                        var firstSpline = splineOutput[0];
                        var firstPath = path[0];
                        Assert.AreEqual(firstPath.SpinePosition(firstPath.HeartOffset).x, firstSpline.Position.x, 0.01f);
                        Assert.AreEqual(firstPath.SpinePosition(firstPath.HeartOffset).y, firstSpline.Position.y, 0.01f);
                        Assert.AreEqual(firstPath.SpinePosition(firstPath.HeartOffset).z, firstSpline.Position.z, 0.01f);
                    }
                    finally {
                        splineOutput.Dispose();
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

        [Test]
        public void SplineSync_CurvedNode_ProducesUniformArcSpacing() {
            var coaster = Coaster.Create(Allocator.Temp);
            try {
                uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
                coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

                uint curvedId = coaster.Graph.CreateNode(NodeType.Curved, new float2(100f, 0f), out var curvedInputs, out _, Allocator.Temp);
                coaster.Scalars[Coaster.InputKey(curvedId, 1)] = 20f;

                coaster.Graph.AddEdge(anchorOutputs[0], curvedInputs[0]);

                anchorOutputs.Dispose();
                curvedInputs.Dispose();

                TrackData.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
                try {
                    Assert.IsTrue(track.NodeToSection.TryGetValue(curvedId, out int sectionIdx), "Curved node should have section");
                    var section = track.Sections[sectionIdx];
                    var path = track.Points.AsArray().GetSubArray(section.StartIndex, section.Length);

                    float resolution = 0.5f;
                    var splineOutput = new NativeList<SplinePoint>(256, Allocator.Temp);
                    try {
                        SplineResampler.Resample(path, resolution, ref splineOutput);

                        Assert.Greater(splineOutput.Length, 2, "Spline should have multiple points for curved track");

                        for (int i = 1; i < splineOutput.Length; i++) {
                            float arcDelta = splineOutput[i].Arc - splineOutput[i - 1].Arc;
                            Assert.That(arcDelta, Is.EqualTo(resolution).Within(0.05f),
                                $"Spline point {i}: arc spacing {arcDelta} should be ~{resolution}");
                        }
                    }
                    finally {
                        splineOutput.Dispose();
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

        [Test]
        public void SplineSync_PreservesDirectionNormalLateral() {
            var coaster = Coaster.Create(Allocator.Temp);
            try {
                uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
                coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

                uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out _, Allocator.Temp);
                coaster.Scalars[Coaster.InputKey(forceId, NodeMeta.Duration)] = 0.5f;

                coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);

                anchorOutputs.Dispose();
                forceInputs.Dispose();

                TrackData.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
                try {
                    Assert.IsTrue(track.NodeToSection.TryGetValue(forceId, out int sectionIdx), "Force node should have section");
                    var section = track.Sections[sectionIdx];
                    var path = track.Points.AsArray().GetSubArray(section.StartIndex, section.Length);

                    var splineOutput = new NativeList<SplinePoint>(256, Allocator.Temp);
                    try {
                        SplineResampler.Resample(path, ref splineOutput);

                        Assert.AreEqual(path.Length, splineOutput.Length, "Direct resample should preserve point count");

                        for (int i = 0; i < splineOutput.Length; i++) {
                            var sp = splineOutput[i];
                            Assert.That(math.length(sp.Direction), Is.EqualTo(1f).Within(0.01f),
                                $"Point {i}: Direction should be normalized");
                            Assert.That(math.length(sp.Normal), Is.EqualTo(1f).Within(0.01f),
                                $"Point {i}: Normal should be normalized");
                            Assert.That(math.length(sp.Lateral), Is.EqualTo(1f).Within(0.01f),
                                $"Point {i}: Lateral should be normalized");
                        }
                    }
                    finally {
                        splineOutput.Dispose();
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
    }
}
