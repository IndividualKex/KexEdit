using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using KexEdit.UI;

namespace KexEdit.Legacy.Editor {
    public static class TrackDataExporter {
        [MenuItem("KexEdit/Export Track Data")]
        public static void Export() {
            if (!Application.isPlaying) {
                Debug.LogError("Track data export requires Play mode with a track loaded.");
                return;
            }

            if (World.DefaultGameObjectInjectionWorld == null) {
                Debug.LogError("No ECS World available. Enter Play mode first.");
                return;
            }

            string outputDir = Path.Combine(Application.dataPath, "Tests", "TrackData");
            if (!Directory.Exists(outputDir)) {
                Directory.CreateDirectory(outputDir);
            }

            try {
                string outputPath = ExportCurrentTrack(outputDir);
                if (!string.IsNullOrEmpty(outputPath)) {
                    Debug.Log($"Track data exported to: {outputPath}");
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to export track data: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static string ExportCurrentTrack(string outputDir) {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) {
                Debug.LogError("No ECS World available.");
                return null;
            }

            var entityManager = world.EntityManager;

            var coasterQuery = entityManager.CreateEntityQuery(typeof(Coaster));
            if (coasterQuery.IsEmpty) {
                Debug.LogWarning("No coaster found. Make sure track is loaded in Play mode.");
                return null;
            }

            string trackName = GetTrackName();

            var trackData = new TrackDataRoot {
                Metadata = new TrackDataMetadata {
                    SourceFile = trackName,
                    ExportedAt = DateTime.UtcNow.ToString("o"),
                    KexEditVersion = Application.version
                },
                Graph = new TrackDataGraph(),
                Sections = new List<TrackDataSection>()
            };

            var coaster = coasterQuery.GetSingleton<Coaster>();
            var orderedNodes = BuildNodeOrder(entityManager, coaster.RootNode);

            trackData.Graph.RootNodeId = GetNodeId(entityManager, coaster.RootNode);
            trackData.Graph.NodeOrder = new List<uint>();

            foreach (var nodeEntity in orderedNodes) {
                var node = entityManager.GetComponentData<Node>(nodeEntity);
                trackData.Graph.NodeOrder.Add(node.Id);

                var section = ExtractSectionData(entityManager, nodeEntity);
                if (section != null) {
                    trackData.Sections.Add(section);
                }
            }

            orderedNodes.Dispose();

            string outputPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(trackName)}.json");
            string json = SerializeToJson(trackData);
            File.WriteAllText(outputPath, json, Encoding.UTF8);
            return outputPath;
        }

        private static string GetTrackName() {
            return ProjectOperations.GetProjectDisplayName();
        }

        private static NativeList<Entity> BuildNodeOrder(EntityManager entityManager, Entity rootNode) {
            var orderedNodes = new NativeList<Entity>(Allocator.Temp);
            var currentNode = rootNode;

            while (currentNode != Entity.Null) {
                orderedNodes.Add(currentNode);
                var node = entityManager.GetComponentData<Node>(currentNode);
                currentNode = node.Next;
            }

            return orderedNodes;
        }

        private static uint GetNodeId(EntityManager entityManager, Entity entity) {
            if (entity == Entity.Null) return 0;
            if (!entityManager.HasComponent<Node>(entity)) return 0;
            return entityManager.GetComponentData<Node>(entity).Id;
        }

        private static TrackDataSection ExtractSectionData(EntityManager entityManager, Entity nodeEntity) {
            if (!entityManager.HasComponent<Node>(nodeEntity)) return null;

            var node = entityManager.GetComponentData<Node>(nodeEntity);

            if (!IsSectionNode(node.Type)) return null;

            var section = new TrackDataSection {
                NodeId = node.Id,
                NodeType = node.Type.ToString(),
                Position = ToVec2(node.Position),
                Inputs = new TrackDataInputs(),
                Outputs = new TrackDataOutputs()
            };

            if (entityManager.HasComponent<Anchor>(nodeEntity)) {
                var anchor = entityManager.GetComponentData<Anchor>(nodeEntity);
                section.Inputs.Anchor = ToPointDataDto(anchor.Value);
            }

            if (entityManager.HasComponent<Duration>(nodeEntity)) {
                var duration = entityManager.GetComponentData<Duration>(nodeEntity);
                section.Inputs.Duration = new TrackDataDuration {
                    Type = duration.Type.ToString(),
                    Value = duration.Value
                };
            }

            if (entityManager.HasComponent<PropertyOverrides>(nodeEntity)) {
                var overrides = entityManager.GetComponentData<PropertyOverrides>(nodeEntity);
                section.Inputs.PropertyOverrides = new TrackDataPropertyOverrides {
                    FixedVelocity = overrides.FixedVelocity,
                    Heart = overrides.Heart,
                    Friction = overrides.Friction,
                    Resistance = overrides.Resistance
                };
            }

            if (entityManager.HasComponent<CurveData>(nodeEntity)) {
                var curveData = entityManager.GetComponentData<CurveData>(nodeEntity);
                section.Inputs.CurveData = new TrackDataCurve {
                    Radius = curveData.Radius,
                    Arc = curveData.Arc,
                    Axis = curveData.Axis,
                    LeadIn = curveData.LeadIn,
                    LeadOut = curveData.LeadOut
                };
            }

            if (entityManager.HasComponent<Steering>(nodeEntity)) {
                section.Inputs.Steering = entityManager.GetComponentData<Steering>(nodeEntity).Value;
            }

            section.Inputs.Keyframes = ExtractKeyframes(entityManager, nodeEntity);

            if (node.Type == NodeType.CopyPathSection) {
                ExtractCopyPathInputs(entityManager, nodeEntity, section);
            }

            if (node.Type == NodeType.Bridge) {
                ExtractBridgeInputs(entityManager, nodeEntity, section);
            }

            if (entityManager.HasBuffer<CorePointBuffer>(nodeEntity)) {
                var points = entityManager.GetBuffer<CorePointBuffer>(nodeEntity);
                section.Outputs.Points = new List<TrackDataPoint>(points.Length);
                for (int i = 0; i < points.Length; i++) {
                    section.Outputs.Points.Add(ToPointDataDto(points[i].ToPointData()));
                }
                section.Outputs.PointCount = points.Length;
                if (points.Length > 0) {
                    section.Outputs.TotalLength = points[^1].HeartArc();
                }
            }

            return section;
        }

        private static void ExtractCopyPathInputs(EntityManager entityManager, Entity nodeEntity, TrackDataSection section) {
            if (!entityManager.HasBuffer<InputPortReference>(nodeEntity)) return;

            var inputPorts = entityManager.GetBuffer<InputPortReference>(nodeEntity);
            if (inputPorts.Length < 4) return;

            var pathPortEntity = inputPorts[1].Value;
            if (entityManager.HasBuffer<PathPort>(pathPortEntity)) {
                var pathBuffer = entityManager.GetBuffer<PathPort>(pathPortEntity);
                section.Inputs.SourcePath = new List<TrackDataPoint>(pathBuffer.Length);
                for (int i = 0; i < pathBuffer.Length; i++) {
                    section.Inputs.SourcePath.Add(ToPointDataDto(pathBuffer[i].Value));
                }
            }

            var startPortEntity = inputPorts[2].Value;
            if (entityManager.HasComponent<StartPort>(startPortEntity)) {
                section.Inputs.Start = entityManager.GetComponentData<StartPort>(startPortEntity).Value;
            }

            var endPortEntity = inputPorts[3].Value;
            if (entityManager.HasComponent<EndPort>(endPortEntity)) {
                section.Inputs.End = entityManager.GetComponentData<EndPort>(endPortEntity).Value;
            }
        }

        private static void ExtractBridgeInputs(EntityManager entityManager, Entity nodeEntity, TrackDataSection section) {
            if (!entityManager.HasBuffer<InputPortReference>(nodeEntity)) return;

            var inputPorts = entityManager.GetBuffer<InputPortReference>(nodeEntity);
            if (inputPorts.Length < 2) return;

            var targetAnchorPortEntity = inputPorts[1].Value;
            if (entityManager.HasComponent<AnchorPort>(targetAnchorPortEntity)) {
                var targetAnchor = entityManager.GetComponentData<AnchorPort>(targetAnchorPortEntity);
                section.Inputs.TargetAnchor = ToPointDataDto(targetAnchor.Value);
            }

            if (inputPorts.Length > 2) {
                var outWeightPortEntity = inputPorts[2].Value;
                if (entityManager.HasComponent<OutWeightPort>(outWeightPortEntity)) {
                    section.Inputs.OutWeight = entityManager.GetComponentData<OutWeightPort>(outWeightPortEntity).Value;
                }
            }

            if (inputPorts.Length > 3) {
                var inWeightPortEntity = inputPorts[3].Value;
                if (entityManager.HasComponent<InWeightPort>(inWeightPortEntity)) {
                    section.Inputs.InWeight = entityManager.GetComponentData<InWeightPort>(inWeightPortEntity).Value;
                }
            }
        }

        private static bool IsSectionNode(NodeType type) {
            return type == NodeType.ForceSection ||
                   type == NodeType.GeometricSection ||
                   type == NodeType.CurvedSection ||
                   type == NodeType.CopyPathSection ||
                   type == NodeType.Bridge;
        }

        private static TrackDataKeyframes ExtractKeyframes(EntityManager entityManager, Entity nodeEntity) {
            var keyframes = new TrackDataKeyframes();

            if (entityManager.HasBuffer<RollSpeedKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<RollSpeedKeyframe>(nodeEntity);
                keyframes.RollSpeed = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<NormalForceKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<NormalForceKeyframe>(nodeEntity);
                keyframes.NormalForce = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<LateralForceKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<LateralForceKeyframe>(nodeEntity);
                keyframes.LateralForce = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<PitchSpeedKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<PitchSpeedKeyframe>(nodeEntity);
                keyframes.PitchSpeed = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<YawSpeedKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<YawSpeedKeyframe>(nodeEntity);
                keyframes.YawSpeed = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<FixedVelocityKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<FixedVelocityKeyframe>(nodeEntity);
                keyframes.FixedVelocity = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<HeartKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<HeartKeyframe>(nodeEntity);
                keyframes.Heart = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<FrictionKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<FrictionKeyframe>(nodeEntity);
                keyframes.Friction = ToKeyframeList(buffer);
            }

            if (entityManager.HasBuffer<ResistanceKeyframe>(nodeEntity)) {
                var buffer = entityManager.GetBuffer<ResistanceKeyframe>(nodeEntity);
                keyframes.Resistance = ToKeyframeList(buffer);
            }

            return keyframes;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<RollSpeedKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<NormalForceKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<LateralForceKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<PitchSpeedKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<YawSpeedKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<FixedVelocityKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<HeartKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<FrictionKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static List<TrackDataKeyframe> ToKeyframeList(DynamicBuffer<ResistanceKeyframe> buffer) {
            var list = new List<TrackDataKeyframe>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) list.Add(ToKeyframeDto(buffer[i].Value));
            return list;
        }

        private static TrackDataKeyframe ToKeyframeDto(Keyframe kf) {
            return new TrackDataKeyframe {
                Id = kf.Id,
                Time = kf.Time,
                Value = kf.Value,
                InInterpolation = kf.InInterpolation.ToString(),
                OutInterpolation = kf.OutInterpolation.ToString(),
                HandleType = kf.HandleType.ToString(),
                InTangent = kf.InTangent,
                OutTangent = kf.OutTangent,
                InWeight = kf.InWeight,
                OutWeight = kf.OutWeight
            };
        }

        private static TrackDataPoint ToPointDataDto(PointData p) {
            float effectiveFrictionDistance = p.HeartArc - p.FrictionOrigin;
            float center = p.HeartOffset * 0.9f;
            float centerY = p.HeartPosition.y + p.Normal.y * center;
            float kineticEnergy = 0.5f * p.Velocity * p.Velocity;
            float gravitationalPE = Constants.G * centerY;
            float frictionPE = Constants.G * effectiveFrictionDistance * p.Friction;

            return new TrackDataPoint {
                Position = ToVec3(p.HeartPosition),
                Direction = ToVec3(p.Direction),
                Lateral = ToVec3(p.Lateral),
                Normal = ToVec3(p.Normal),
                Roll = p.Roll,
                Velocity = p.Velocity,
                Energy = p.Energy,
                NormalForce = p.NormalForce,
                LateralForce = p.LateralForce,
                DistanceFromLast = p.HeartAdvance,
                HeartDistanceFromLast = p.SpineAdvance,
                AngleFromLast = p.AngleFromLast,
                PitchFromLast = p.PitchFromLast,
                YawFromLast = p.YawFromLast,
                RollSpeed = p.RollSpeed,
                TotalLength = p.HeartArc,
                TotalHeartLength = p.SpineArc,
                FrictionCompensation = p.FrictionOrigin,
                Heart = p.HeartOffset,
                Friction = p.Friction,
                Resistance = p.Resistance,
                Facing = p.Facing,
                EffectiveFrictionDistance = effectiveFrictionDistance,
                KineticEnergy = kineticEnergy,
                GravitationalPE = gravitationalPE,
                FrictionPE = frictionPE,
                CenterY = centerY
            };
        }

        private static TrackDataVec3 ToVec3(float3 v) => new() { X = v.x, Y = v.y, Z = v.z };
        private static TrackDataVec2 ToVec2(float2 v) => new() { X = v.x, Y = v.y };

        private static string SerializeToJson(TrackDataRoot data) {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            WriteMetadata(sb, data.Metadata, 1);
            sb.AppendLine(",");
            WriteGraph(sb, data.Graph, 1);
            sb.AppendLine(",");
            WriteSections(sb, data.Sections, 1);
            sb.AppendLine();
            sb.Append("}");
            return sb.ToString();
        }

        private static void WriteMetadata(StringBuilder sb, TrackDataMetadata m, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"metadata\": {{");
            sb.AppendLine($"{ind}  \"sourceFile\": \"{Escape(m.SourceFile)}\",");
            sb.AppendLine($"{ind}  \"exportedAt\": \"{Escape(m.ExportedAt)}\",");
            sb.AppendLine($"{ind}  \"kexEditVersion\": \"{Escape(m.KexEditVersion)}\"");
            sb.Append($"{ind}}}");
        }

        private static void WriteGraph(StringBuilder sb, TrackDataGraph g, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"graph\": {{");
            sb.AppendLine($"{ind}  \"rootNodeId\": {g.RootNodeId},");
            sb.Append($"{ind}  \"nodeOrder\": [");
            if (g.NodeOrder.Count > 0) {
                sb.Append(string.Join(", ", g.NodeOrder));
            }
            sb.AppendLine("]");
            sb.Append($"{ind}}}");
        }

        private static void WriteSections(StringBuilder sb, List<TrackDataSection> sections, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"sections\": [");
            for (int i = 0; i < sections.Count; i++) {
                WriteSection(sb, sections[i], indent + 1);
                if (i < sections.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.Append($"{ind}]");
        }

        private static void WriteSection(StringBuilder sb, TrackDataSection s, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}{{");
            sb.AppendLine($"{ind}  \"nodeId\": {s.NodeId},");
            sb.AppendLine($"{ind}  \"nodeType\": \"{s.NodeType}\",");
            sb.AppendLine($"{ind}  \"position\": {Vec2Json(s.Position)},");
            WriteInputs(sb, s.Inputs, indent + 1);
            sb.AppendLine(",");
            WriteOutputs(sb, s.Outputs, indent + 1);
            sb.AppendLine();
            sb.Append($"{ind}}}");
        }

        private static void WriteInputs(StringBuilder sb, TrackDataInputs inp, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"inputs\": {{");

            if (inp.Anchor != null) {
                sb.AppendLine($"{ind}  \"anchor\": {PointDataJson(inp.Anchor)},");
            }

            if (inp.Duration != null) {
                sb.AppendLine($"{ind}  \"duration\": {{ \"type\": \"{inp.Duration.Type}\", \"value\": {F(inp.Duration.Value)} }},");
            }

            if (inp.PropertyOverrides != null) {
                var po = inp.PropertyOverrides;
                sb.AppendLine($"{ind}  \"propertyOverrides\": {{ \"fixedVelocity\": {B(po.FixedVelocity)}, \"heart\": {B(po.Heart)}, \"friction\": {B(po.Friction)}, \"resistance\": {B(po.Resistance)} }},");
            }

            if (inp.CurveData != null) {
                var cd = inp.CurveData;
                sb.AppendLine($"{ind}  \"curveData\": {{ \"radius\": {F(cd.Radius)}, \"arc\": {F(cd.Arc)}, \"axis\": {F(cd.Axis)}, \"leadIn\": {F(cd.LeadIn)}, \"leadOut\": {F(cd.LeadOut)} }},");
            }

            if (inp.Steering.HasValue) {
                sb.AppendLine($"{ind}  \"steering\": {B(inp.Steering.Value)},");
            }

            if (inp.Start.HasValue) {
                sb.AppendLine($"{ind}  \"start\": {F(inp.Start.Value)},");
            }

            if (inp.End.HasValue) {
                sb.AppendLine($"{ind}  \"end\": {F(inp.End.Value)},");
            }

            if (inp.SourcePath != null && inp.SourcePath.Count > 0) {
                sb.Append($"{ind}  \"sourcePath\": [");
                sb.AppendLine();
                for (int i = 0; i < inp.SourcePath.Count; i++) {
                    sb.Append($"{ind}    {PointDataJson(inp.SourcePath[i])}");
                    if (i < inp.SourcePath.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine($"{ind}  ],");
            }

            if (inp.TargetAnchor != null) {
                sb.AppendLine($"{ind}  \"targetAnchor\": {PointDataJson(inp.TargetAnchor)},");
            }

            if (inp.OutWeight.HasValue) {
                sb.AppendLine($"{ind}  \"outWeight\": {F(inp.OutWeight.Value)},");
            }

            if (inp.InWeight.HasValue) {
                sb.AppendLine($"{ind}  \"inWeight\": {F(inp.InWeight.Value)},");
            }

            WriteKeyframes(sb, inp.Keyframes, indent + 1);
            sb.AppendLine();
            sb.Append($"{ind}}}");
        }

        private static void WriteKeyframes(StringBuilder sb, TrackDataKeyframes kf, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"keyframes\": {{");

            var entries = new List<string>();
            if (kf.RollSpeed?.Count > 0) entries.Add($"{ind}  \"rollSpeed\": {KeyframeArrayJson(kf.RollSpeed)}");
            if (kf.NormalForce?.Count > 0) entries.Add($"{ind}  \"normalForce\": {KeyframeArrayJson(kf.NormalForce)}");
            if (kf.LateralForce?.Count > 0) entries.Add($"{ind}  \"lateralForce\": {KeyframeArrayJson(kf.LateralForce)}");
            if (kf.PitchSpeed?.Count > 0) entries.Add($"{ind}  \"pitchSpeed\": {KeyframeArrayJson(kf.PitchSpeed)}");
            if (kf.YawSpeed?.Count > 0) entries.Add($"{ind}  \"yawSpeed\": {KeyframeArrayJson(kf.YawSpeed)}");
            if (kf.FixedVelocity?.Count > 0) entries.Add($"{ind}  \"fixedVelocity\": {KeyframeArrayJson(kf.FixedVelocity)}");
            if (kf.Heart?.Count > 0) entries.Add($"{ind}  \"heart\": {KeyframeArrayJson(kf.Heart)}");
            if (kf.Friction?.Count > 0) entries.Add($"{ind}  \"friction\": {KeyframeArrayJson(kf.Friction)}");
            if (kf.Resistance?.Count > 0) entries.Add($"{ind}  \"resistance\": {KeyframeArrayJson(kf.Resistance)}");

            sb.Append(string.Join(",\n", entries));
            if (entries.Count > 0) sb.AppendLine();
            sb.Append($"{ind}}}");
        }

        private static void WriteOutputs(StringBuilder sb, TrackDataOutputs outp, int indent) {
            string ind = new(' ', indent * 2);
            sb.AppendLine($"{ind}\"outputs\": {{");
            sb.AppendLine($"{ind}  \"pointCount\": {outp.PointCount},");
            sb.AppendLine($"{ind}  \"totalLength\": {F(outp.TotalLength)},");
            sb.Append($"{ind}  \"points\": [");

            if (outp.Points != null && outp.Points.Count > 0) {
                sb.AppendLine();
                for (int i = 0; i < outp.Points.Count; i++) {
                    sb.Append($"{ind}    {PointDataJson(outp.Points[i])}");
                    if (i < outp.Points.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.Append($"{ind}  ");
            }
            sb.AppendLine("]");
            sb.Append($"{ind}}}");
        }

        private static string PointDataJson(TrackDataPoint p) {
            return $"{{ \"position\": {Vec3Json(p.Position)}, \"direction\": {Vec3Json(p.Direction)}, \"lateral\": {Vec3Json(p.Lateral)}, \"normal\": {Vec3Json(p.Normal)}, \"roll\": {F(p.Roll)}, \"velocity\": {F(p.Velocity)}, \"energy\": {F(p.Energy)}, \"normalForce\": {F(p.NormalForce)}, \"lateralForce\": {F(p.LateralForce)}, \"distanceFromLast\": {F(p.DistanceFromLast)}, \"heartDistanceFromLast\": {F(p.HeartDistanceFromLast)}, \"angleFromLast\": {F(p.AngleFromLast)}, \"pitchFromLast\": {F(p.PitchFromLast)}, \"yawFromLast\": {F(p.YawFromLast)}, \"rollSpeed\": {F(p.RollSpeed)}, \"totalLength\": {F(p.TotalLength)}, \"totalHeartLength\": {F(p.TotalHeartLength)}, \"frictionCompensation\": {F(p.FrictionCompensation)}, \"heart\": {F(p.Heart)}, \"friction\": {F(p.Friction)}, \"resistance\": {F(p.Resistance)}, \"facing\": {p.Facing}, \"effectiveFrictionDistance\": {F(p.EffectiveFrictionDistance)}, \"kineticEnergy\": {F(p.KineticEnergy)}, \"gravitationalPE\": {F(p.GravitationalPE)}, \"frictionPE\": {F(p.FrictionPE)}, \"centerY\": {F(p.CenterY)} }}";
        }

        private static string KeyframeArrayJson(List<TrackDataKeyframe> keyframes) {
            if (keyframes == null || keyframes.Count == 0) return "[]";
            var items = new List<string>();
            foreach (var kf in keyframes) {
                items.Add($"{{ \"id\": {kf.Id}, \"time\": {F(kf.Time)}, \"value\": {F(kf.Value)}, \"inInterpolation\": \"{kf.InInterpolation}\", \"outInterpolation\": \"{kf.OutInterpolation}\", \"handleType\": \"{kf.HandleType}\", \"inTangent\": {F(kf.InTangent)}, \"outTangent\": {F(kf.OutTangent)}, \"inWeight\": {F(kf.InWeight)}, \"outWeight\": {F(kf.OutWeight)} }}");
            }
            return "[" + string.Join(", ", items) + "]";
        }

        private static string Vec3Json(TrackDataVec3 v) => $"{{ \"x\": {F(v.X)}, \"y\": {F(v.Y)}, \"z\": {F(v.Z)} }}";
        private static string Vec2Json(TrackDataVec2 v) => $"{{ \"x\": {F(v.X)}, \"y\": {F(v.Y)} }}";
        private static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
        private static string B(bool v) => v ? "true" : "false";
        private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }

    #region DTOs

    public class TrackDataRoot {
        public TrackDataMetadata Metadata;
        public TrackDataGraph Graph;
        public List<TrackDataSection> Sections;
    }

    public class TrackDataMetadata {
        public string SourceFile;
        public string ExportedAt;
        public string KexEditVersion;
    }

    public class TrackDataGraph {
        public uint RootNodeId;
        public List<uint> NodeOrder;
    }

    public class TrackDataSection {
        public uint NodeId;
        public string NodeType;
        public TrackDataVec2 Position;
        public TrackDataInputs Inputs;
        public TrackDataOutputs Outputs;
    }

    public class TrackDataInputs {
        public TrackDataPoint Anchor;
        public TrackDataDuration Duration;
        public TrackDataPropertyOverrides PropertyOverrides;
        public TrackDataCurve CurveData;
        public bool? Steering;
        public TrackDataKeyframes Keyframes;
        public List<TrackDataPoint> SourcePath;
        public float? Start;
        public float? End;
        public TrackDataPoint TargetAnchor;
        public float? OutWeight;
        public float? InWeight;
    }

    public class TrackDataOutputs {
        public List<TrackDataPoint> Points;
        public int PointCount;
        public float TotalLength;
    }

    public class TrackDataDuration {
        public string Type;
        public float Value;
    }

    public class TrackDataPropertyOverrides {
        public bool FixedVelocity;
        public bool Heart;
        public bool Friction;
        public bool Resistance;
    }

    public class TrackDataCurve {
        public float Radius;
        public float Arc;
        public float Axis;
        public float LeadIn;
        public float LeadOut;
    }

    public class TrackDataKeyframes {
        public List<TrackDataKeyframe> RollSpeed;
        public List<TrackDataKeyframe> NormalForce;
        public List<TrackDataKeyframe> LateralForce;
        public List<TrackDataKeyframe> PitchSpeed;
        public List<TrackDataKeyframe> YawSpeed;
        public List<TrackDataKeyframe> FixedVelocity;
        public List<TrackDataKeyframe> Heart;
        public List<TrackDataKeyframe> Friction;
        public List<TrackDataKeyframe> Resistance;
    }

    public class TrackDataKeyframe {
        public uint Id;
        public float Time;
        public float Value;
        public string InInterpolation;
        public string OutInterpolation;
        public string HandleType;
        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;
    }

    public class TrackDataPoint {
        public TrackDataVec3 Position;
        public TrackDataVec3 Direction;
        public TrackDataVec3 Lateral;
        public TrackDataVec3 Normal;
        public float Roll;
        public float Velocity;
        public float Energy;
        public float NormalForce;
        public float LateralForce;
        public float DistanceFromLast;
        public float HeartDistanceFromLast;
        public float AngleFromLast;
        public float PitchFromLast;
        public float YawFromLast;
        public float RollSpeed;
        public float TotalLength;
        public float TotalHeartLength;
        public float FrictionCompensation;
        public float Heart;
        public float Friction;
        public float Resistance;
        public int Facing;

        // Derived energy fields for analysis
        public float EffectiveFrictionDistance;
        public float KineticEnergy;
        public float GravitationalPE;
        public float FrictionPE;
        public float CenterY;
    }

    public class TrackDataVec3 {
        public float X;
        public float Y;
        public float Z;
    }

    public class TrackDataVec2 {
        public float X;
        public float Y;
    }

    #endregion
}
