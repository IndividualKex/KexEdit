using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KexEdit.Document;
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
        [TestCase("veloci")]
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

                var goldSection = BuildGoldSection(nodeId, nodeType, i, doc, sectionPoints);
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
                                             in DocumentAggregate doc, NativeArray<Point> points) {
            float2 nodePos = doc.Graph.NodePositions[graphIndex];

            var section = new GoldSection {
                nodeId = nodeId,
                nodeType = GetNodeTypeName((SchemaNodeType)nodeType),
                position = new GoldVec2 { x = nodePos.x, y = nodePos.y },
                inputs = BuildGoldInputs(nodeId, (SchemaNodeType)nodeType, doc, points),
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
                                           NativeArray<Point> points) {
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

            // Check for DrivenVelocity keyframes as proxy for fixedVelocity
            inputs.propertyOverrides.fixedVelocity =
                doc.Keyframes.TryGet(nodeId, PropertyId.DrivenVelocity, out _);

            // Steering flag
            ulong steeringKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Steering);
            inputs.steering = doc.Flags.TryGetValue(steeringKey, out int s) && s == 1;

            // Keyframes
            inputs.keyframes = BuildGoldKeyframes(nodeId, doc);

            // Curve data for curved sections
            if (nodeType == SchemaNodeType.Curved) {
                inputs.curveData = BuildCurveData(nodeId, doc);
            }

            return inputs;
        }

        private GoldCurveData BuildCurveData(uint nodeId, in DocumentAggregate doc) {
            // Look up curve data from document scalars
            // These would be stored with specific input indices for curved nodes
            var curveData = new GoldCurveData();
            // Default values if not found
            return curveData;
        }

        private GoldKeyframes BuildGoldKeyframes(uint nodeId, in DocumentAggregate doc) {
            var keyframes = new GoldKeyframes {
                rollSpeed = GetKeyframeList(nodeId, PropertyId.RollSpeed, doc),
                normalForce = GetKeyframeList(nodeId, PropertyId.NormalForce, doc),
                lateralForce = GetKeyframeList(nodeId, PropertyId.LateralForce, doc),
                pitchSpeed = GetKeyframeList(nodeId, PropertyId.PitchSpeed, doc),
                yawSpeed = GetKeyframeList(nodeId, PropertyId.YawSpeed, doc),
                fixedVelocity = GetKeyframeList(nodeId, PropertyId.DrivenVelocity, doc),
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
