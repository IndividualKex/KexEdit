using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KexEdit.Document;
using KexEdit.Graph;
using KexEdit.Graph.Typed;
using KexEdit.Legacy;
using KexEdit.Sim;
using NUnit.Framework;
using KexEdit.Sim.Schema;
using SchemaNodeType = KexEdit.Sim.Schema.NodeType;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DocumentAggregate = KexEdit.Document.Document;

namespace Tests {
    [TestFixture]
    [Category("GoldExport")]
    public class GoldDataExporter {
        private const string VERSION = "0.12.0";
        private const int MAX_POINTS_PER_SECTION = 50000;

        [Test]
        [TestCase("shuttle")]
        [TestCase("circuit")]
        [TestCase("all_types")]
        [TestCase("switch")]
        public void ExportGoldData(string name) {
            var kexPath = $"Assets/Tests/Assets/{name}.kex";
            var outputPath = $"Assets/Tests/TrackData/{name}.json";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        var goldData = BuildGoldData(name, coaster, track);
                        string json = SerializeGoldData(goldData);
                        File.WriteAllText(outputPath, json, Encoding.UTF8);
                        Debug.Log($"Exported gold data to: {outputPath}");
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

        private GoldTrackData BuildGoldData(string sourceName, in DocumentAggregate doc, in KexEdit.Track.Track track) {
            var data = new GoldTrackData {
                metadata = new GoldMetadata {
                    sourceFile = sourceName,
                    exportedAt = DateTime.UtcNow.ToString("o"),
                    kexEditVersion = VERSION
                },
                graph = BuildGoldGraph(doc),
                sections = new List<GoldSection>()
            };

            // Build sections in graph traversal order
            for (int i = 0; i < doc.Graph.NodeCount; i++) {
                uint nodeId = doc.Graph.NodeIds[i];
                uint nodeType = doc.Graph.NodeTypes[i];

                // Skip non-section nodes (Anchor, Reverse, etc.)
                if (!IsSectionNode((SchemaNodeType)nodeType)) continue;

                // Find corresponding track section
                if (!track.NodeToSection.TryGetValue(nodeId, out int sectionIndex)) continue;

                var trackSection = track.Sections[sectionIndex];
                var sectionPoints = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                var goldSection = BuildGoldSection(nodeId, nodeType, i, doc, track, sectionPoints);
                data.sections.Add(goldSection);
            }

            return data;
        }

        private GoldGraph BuildGoldGraph(in DocumentAggregate doc) {
            var graph = new GoldGraph {
                rootNodeId = doc.Graph.NodeCount > 0 ? doc.Graph.NodeIds[0] : 0,
                nodeOrder = new List<uint>()
            };

            for (int i = 0; i < doc.Graph.NodeCount; i++) {
                uint nodeId = doc.Graph.NodeIds[i];
                uint nodeType = doc.Graph.NodeTypes[i];
                if (IsSectionNode((SchemaNodeType)nodeType)) {
                    graph.nodeOrder.Add(nodeId);
                }
            }

            return graph;
        }

        private bool IsSectionNode(SchemaNodeType nodeType) {
            return nodeType == SchemaNodeType.Force ||
                   nodeType == SchemaNodeType.Geometric ||
                   nodeType == SchemaNodeType.Curved ||
                   nodeType == SchemaNodeType.CopyPath ||
                   nodeType == SchemaNodeType.Bridge;
        }

        private GoldSection BuildGoldSection(uint nodeId, uint nodeType, int graphIndex,
                                             in DocumentAggregate doc, in KexEdit.Track.Track track, NativeArray<Point> points) {
            float2 nodePos = doc.Graph.NodePositions[graphIndex];

            var section = new GoldSection {
                nodeId = nodeId,
                nodeType = GetNodeTypeName((SchemaNodeType)nodeType),
                position = new GoldVec2 { x = nodePos.x, y = nodePos.y },
                inputs = BuildGoldInputs(nodeId, (SchemaNodeType)nodeType, doc, track, points),
                outputs = BuildGoldOutputs(points)
            };

            return section;
        }

        private string GetNodeTypeName(SchemaNodeType nodeType) {
            return nodeType switch {
                SchemaNodeType.Force => "ForceSection",
                SchemaNodeType.Geometric => "GeometricSection",
                SchemaNodeType.Curved => "CurvedSection",
                SchemaNodeType.CopyPath => "CopyPathSection",
                SchemaNodeType.Bridge => "Bridge",
                _ => nodeType.ToString()
            };
        }

        private GoldInputs BuildGoldInputs(uint nodeId, SchemaNodeType nodeType, in DocumentAggregate doc,
                                           in KexEdit.Track.Track track, NativeArray<Point> points) {
            var inputs = new GoldInputs();

            // Anchor point (first point of section)
            if (points.Length > 0) {
                inputs.anchor = PointToGoldPointData(points[0]);
            }

            // Duration
            ulong durKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Duration);
            ulong durTypeKey = DocumentAggregate.InputKey(nodeId, NodeMeta.DurationType);
            if (doc.Scalars.TryGetValue(durKey, out float durValue)) {
                int durType = doc.Flags.TryGetValue(durTypeKey, out int t) ? t : 0;
                inputs.duration = new GoldDuration {
                    type = durType == 0 ? "Time" : "Distance",
                    value = durValue
                };
            }

            // Property overrides
            inputs.propertyOverrides = new GoldPropertyOverrides();
            ulong overrideHeartKey = DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideHeart);
            ulong overrideFrictionKey = DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideFriction);
            ulong overrideResistanceKey = DocumentAggregate.InputKey(nodeId, NodeMeta.OverrideResistance);

            inputs.propertyOverrides.heart = doc.Flags.TryGetValue(overrideHeartKey, out int h) && h == 1;
            inputs.propertyOverrides.friction = doc.Flags.TryGetValue(overrideFrictionKey, out int fr) && fr == 1;
            inputs.propertyOverrides.resistance = doc.Flags.TryGetValue(overrideResistanceKey, out int r) && r == 1;

            // Check for Driven flag (not keyframes presence)
            ulong drivenKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Driven);
            inputs.propertyOverrides.driven = doc.Flags.TryGetValue(drivenKey, out int drivenVal) && drivenVal == 1;

            // Steering flag
            ulong steeringKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Steering);
            inputs.steering = doc.Flags.TryGetValue(steeringKey, out int s) && s == 1;

            // Keyframes
            inputs.keyframes = BuildGoldKeyframes(nodeId, doc);

            // Curve data for curved sections
            if (nodeType == SchemaNodeType.Curved) {
                inputs.curveData = BuildCurveData(nodeId, doc);
            }

            // Bridge-specific inputs
            if (nodeType == SchemaNodeType.Bridge) {
                // Target anchor comes from the connected Target input port (port 1)
                inputs.targetAnchor = BuildBridgeTarget(nodeId, doc, track);

                // BridgePorts: OutWeight = 2, InWeight = 3
                inputs.outWeight = GetScalar(doc.Scalars, nodeId, 2, 0.3f);
                inputs.inWeight = GetScalar(doc.Scalars, nodeId, 3, 0.3f);
            }

            // CopyPath-specific inputs
            if (nodeType == SchemaNodeType.CopyPath) {
                // CopyPathPorts: Start = 2, End = 3
                inputs.start = GetScalar(doc.Scalars, nodeId, 2, 0f);
                inputs.end = GetScalar(doc.Scalars, nodeId, 3, -1f);

                // sourcePath comes from the Path input (port 1), which connects to another section's output
                inputs.sourcePath = BuildCopyPathSource(nodeId, doc, track);
            }

            return inputs;
        }

        private GoldCurveData BuildCurveData(uint nodeId, in DocumentAggregate doc) {
            // Curved port indices from CurvedPorts
            const int RadiusPort = 1;
            const int ArcPort = 2;
            const int AxisPort = 3;
            const int LeadInPort = 4;
            const int LeadOutPort = 5;

            return new GoldCurveData {
                radius = GetScalar(doc.Scalars, nodeId, RadiusPort, 10f),
                arc = GetScalar(doc.Scalars, nodeId, ArcPort, 90f),
                axis = GetScalar(doc.Scalars, nodeId, AxisPort, 0f),
                leadIn = GetScalar(doc.Scalars, nodeId, LeadInPort, 0f),
                leadOut = GetScalar(doc.Scalars, nodeId, LeadOutPort, 0f)
            };
        }

        private float GetScalar(NativeHashMap<ulong, float> scalars, uint nodeId, int inputIndex, float defaultValue) {
            ulong key = DocumentAggregate.InputKey(nodeId, inputIndex);
            return scalars.TryGetValue(key, out float value) ? value : defaultValue;
        }

        private GoldPointData BuildBridgeTarget(uint nodeId, in DocumentAggregate doc, in KexEdit.Track.Track track) {
            // Bridge Target input is Anchor type, localIndex 1 (second anchor-type port)
            if (!doc.Graph.TryGetInputBySpec(nodeId, PortDataType.Anchor, 1, out uint targetPortId)) {
                return null;
            }

            // Find the edge that targets this port
            for (int i = 0; i < doc.Graph.EdgeIds.Length; i++) {
                if (doc.Graph.EdgeTargets[i] != targetPortId) continue;

                uint sourcePortId = doc.Graph.EdgeSources[i];
                if (!doc.Graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = doc.Graph.PortOwners[portIndex];

                // Get the source section's first point from the track
                if (track.NodeToSection.TryGetValue(sourceNodeId, out int sectionIndex)) {
                    var sourceSection = track.Sections[sectionIndex];
                    if (sourceSection.Length > 0) {
                        var firstPoint = track.Points[sourceSection.StartIndex];
                        return PointToGoldPointData(firstPoint);
                    }
                }
            }

            return null;
        }

        private List<GoldPointData> BuildCopyPathSource(uint nodeId, in DocumentAggregate doc, in KexEdit.Track.Track track) {
            // Get input ports by index - Path is at index 1 (CopyPathPorts.Path = 1)
            doc.Graph.GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);
            if (inputPorts.Length < 2) {
                inputPorts.Dispose();
                return new List<GoldPointData>();
            }

            uint pathPortId = inputPorts[1]; // Path input is at index 1
            inputPorts.Dispose();

            // Find the edge that targets this port
            for (int i = 0; i < doc.Graph.EdgeIds.Length; i++) {
                if (doc.Graph.EdgeTargets[i] != pathPortId) continue;

                uint sourcePortId = doc.Graph.EdgeSources[i];
                if (!doc.Graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = doc.Graph.PortOwners[portIndex];

                // Get the source section's points from the track
                if (track.NodeToSection.TryGetValue(sourceNodeId, out int sectionIndex)) {
                    var sourceSection = track.Sections[sectionIndex];
                    var sourcePoints = track.Points.AsArray().GetSubArray(sourceSection.StartIndex, sourceSection.Length);

                    var result = new List<GoldPointData>(sourcePoints.Length);
                    for (int k = 0; k < sourcePoints.Length; k++) {
                        result.Add(PointToGoldPointData(sourcePoints[k]));
                    }
                    return result;
                }
            }

            return new List<GoldPointData>();
        }

        private GoldKeyframes BuildGoldKeyframes(uint nodeId, in DocumentAggregate doc) {
            var keyframes = new GoldKeyframes {
                rollSpeed = GetKeyframeList(nodeId, PropertyId.RollSpeed, doc),
                normalForce = GetKeyframeList(nodeId, PropertyId.NormalForce, doc),
                lateralForce = GetKeyframeList(nodeId, PropertyId.LateralForce, doc),
                pitchSpeed = GetKeyframeList(nodeId, PropertyId.PitchSpeed, doc),
                yawSpeed = GetKeyframeList(nodeId, PropertyId.YawSpeed, doc),
                drivenVelocity = GetKeyframeList(nodeId, PropertyId.DrivenVelocity, doc),
                heart = GetKeyframeList(nodeId, PropertyId.HeartOffset, doc),
                friction = GetKeyframeList(nodeId, PropertyId.Friction, doc),
                resistance = GetKeyframeList(nodeId, PropertyId.Resistance, doc)
            };
            return keyframes;
        }

        private List<GoldKeyframe> GetKeyframeList(uint nodeId, PropertyId propertyId, in DocumentAggregate doc) {
            var list = new List<GoldKeyframe>();
            if (doc.Keyframes.TryGet(nodeId, propertyId, out var keyframes)) {
                for (int i = 0; i < keyframes.Length; i++) {
                    var kf = keyframes[i];
                    list.Add(new GoldKeyframe {
                        id = (uint)(nodeId * 1000 + (int)propertyId * 100 + i), // Generate deterministic ID
                        time = kf.Time,
                        value = kf.Value,
                        inInterpolation = kf.InInterpolation.ToString(),
                        outInterpolation = kf.OutInterpolation.ToString(),
                        handleType = "Aligned",
                        inTangent = kf.InTangent,
                        outTangent = kf.OutTangent,
                        inWeight = kf.InWeight,
                        outWeight = kf.OutWeight
                    });
                }
            }
            return list;
        }

        private GoldOutputs BuildGoldOutputs(NativeArray<Point> points) {
            int cappedLength = math.min(points.Length, MAX_POINTS_PER_SECTION);

            var outputs = new GoldOutputs {
                pointCount = 0,
                totalLength = 0f,
                points = new List<GoldPointData>()
            };

            for (int i = 0; i < cappedLength; i++) {
                outputs.points.Add(PointToGoldPointData(points[i]));
            }

            outputs.pointCount = outputs.points.Count;
            outputs.totalLength = outputs.points.Count > 0 ? points[outputs.points.Count - 1].HeartArc : 0f;

            return outputs;
        }

        private GoldPointData PointToGoldPointData(Point pt) {
            float roll = math.atan2(pt.Lateral.y, -pt.Normal.y);
            float3 spinePos = pt.HeartPosition + pt.Normal * pt.HeartOffset;
            float centerY = pt.HeartPosition.y + pt.Normal.y * (pt.HeartOffset * 0.9f);
            float frictionDistance = pt.SpineArc - pt.FrictionOrigin;
            float kineticEnergy = 0.5f * pt.Velocity * pt.Velocity;
            float gravitationalPE = Sim.G * centerY;
            float frictionPE = Sim.G * frictionDistance * pt.Friction;

            return new GoldPointData {
                heartPosition = Float3ToGoldVec3(pt.HeartPosition),
                direction = Float3ToGoldVec3(pt.Direction),
                lateral = Float3ToGoldVec3(pt.Lateral),
                normal = Float3ToGoldVec3(pt.Normal),
                roll = roll,
                velocity = pt.Velocity,
                energy = kineticEnergy + gravitationalPE + frictionPE,  // Computed for backwards compatibility with gold format
                normalForce = pt.NormalForce,
                lateralForce = pt.LateralForce,
                spineAdvance = pt.HeartAdvance,
                heartAdvance = pt.HeartAdvance,
                angleFromLast = 0f,
                pitchFromLast = 0f,
                yawFromLast = 0f,
                rollSpeed = pt.RollSpeed,
                spineArc = pt.SpineArc,
                heartArc = pt.HeartArc,
                frictionOrigin = pt.FrictionOrigin,
                heartOffset = pt.HeartOffset,
                friction = pt.Friction,
                resistance = pt.Resistance,
                facing = 1,
                effectiveFrictionDistance = frictionDistance,
                kineticEnergy = kineticEnergy,
                gravitationalPE = gravitationalPE,
                frictionPE = frictionPE,
                centerY = centerY
            };
        }

        private GoldVec3 Float3ToGoldVec3(float3 v) {
            return new GoldVec3 { x = v.x, y = v.y, z = v.z };
        }

        private string SerializeGoldData(GoldTrackData data) {
            // Use Unity's JsonUtility for serialization
            return JsonUtility.ToJson(data, true);
        }
    }
}
