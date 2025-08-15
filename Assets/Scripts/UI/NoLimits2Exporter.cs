using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SFB;

namespace KexEdit.UI {
    public static class NoLimits2Exporter {
        public static void ExportTrack(float metersPerNode = 2f) {
            try {
                string filePath = StandaloneFileBrowser.SaveFilePanel(
                    "Export NoLimits 2 Track",
                    Application.persistentDataPath,
                    "track",
                    new[] {
                        new ExtensionFilter("NoLimits 2 Element Files", "nl2elem"),
                        new ExtensionFilter("All Files", "*")
                    });

                if (!string.IsNullOrEmpty(filePath)) {
                    ExportTrackToFile(filePath, metersPerNode);
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to export track: {ex.Message}");
            }
        }

        private static void ExportTrackToFile(string filePath, float metersPerNode) {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            using var orderedNodes = BuildTrackGraph(entityManager);
            if (orderedNodes.Length == 0) {
                Debug.LogWarning("No connected track sections found to export");
                return;
            }

            var allPoints = new List<PointData>();
            foreach (var nodeEntity in orderedNodes) {
                var points = entityManager.GetBuffer<Point>(nodeEntity);
                for (int i = 0; i < points.Length; i++) {
                    allPoints.Add(points[i]);
                }
            }

            if (allPoints.Count < 2) {
                Debug.LogWarning("Not enough points to export");
                return;
            }

            var sampledPoints = SamplePoints(allPoints, metersPerNode);
            WriteXmlOutput(writer, sampledPoints);
        }

        private static NativeList<Entity> BuildTrackGraph(EntityManager entityManager) {
            var orderedNodes = new NativeList<Entity>(Allocator.Temp);

            var coasterQuery = entityManager.CreateEntityQuery(typeof(Coaster));
            if (coasterQuery.IsEmpty) {
                return orderedNodes;
            }

            var coaster = coasterQuery.GetSingleton<Coaster>();
            var currentNode = coaster.RootNode;

            while (currentNode != Entity.Null) {
                orderedNodes.Add(currentNode);

                var node = entityManager.GetComponentData<Node>(currentNode);
                currentNode = node.Next;
            }

            return orderedNodes;
        }

        private static List<SampledPoint> SamplePoints(List<PointData> allPoints, float metersPerNode) {
            var sampledPoints = new List<SampledPoint>();

            if (allPoints.Count < 2) return sampledPoints;

            PointData first = allPoints[0];
            PointData last = allPoints[^1];

            float totalLength = last.TotalHeartLength - first.TotalHeartLength;
            int numPoints = math.max(2, (int)math.round(totalLength / metersPerNode));
            float adjustedSpacing = totalLength / (numPoints - 1);

            float startLength = first.TotalHeartLength;
            float nextLength = startLength;

            sampledPoints.Add(new SampledPoint {
                Position = first.Position,
                Direction = first.Direction,
                Lateral = first.Lateral,
                Normal = first.Normal,
                Coord = 0f,
                IsStrict = true
            });

            for (int i = 0; i < allPoints.Count - 1; i++) {
                PointData p0 = allPoints[i];
                PointData p1 = allPoints[i + 1];

                float start = p0.TotalHeartLength;
                float end = p1.TotalHeartLength;

                while (nextLength <= end) {
                    float t = math.saturate((nextLength - start) / (end - start));

                    var sampledPoint = new SampledPoint {
                        Position = math.lerp(p0.Position, p1.Position, t),
                        Direction = math.normalize(math.lerp(p0.Direction, p1.Direction, t)),
                        Lateral = math.normalize(math.lerp(p0.Lateral, p1.Lateral, t)),
                        Normal = math.normalize(math.lerp(p0.Normal, p1.Normal, t)),
                        Coord = (nextLength - startLength) / totalLength,
                        IsStrict = false
                    };

                    sampledPoints.Add(sampledPoint);
                    nextLength += adjustedSpacing;
                }
            }

            sampledPoints.Add(new SampledPoint {
                Position = last.Position,
                Direction = last.Direction,
                Lateral = last.Lateral,
                Normal = last.Normal,
                Coord = 1f,
                IsStrict = true
            });

            return sampledPoints;
        }

        private static void WriteXmlOutput(StreamWriter writer, List<SampledPoint> sampledPoints) {
            writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            writer.WriteLine("<root>");
            writer.WriteLine("\t<element>");
            writer.WriteLine("\t\t<description>KexEdit Export Data</description>");

            foreach (var point in sampledPoints) {
                writer.WriteLine("\t\t<vertex>");
                WriteElement(writer, "x", -point.Position.x);
                WriteElement(writer, "y", point.Position.y);
                WriteElement(writer, "z", point.Position.z);
                if (point.IsStrict) {
                    writer.WriteLine("\t\t\t<strict>true</strict>");
                }
                writer.WriteLine("\t\t</vertex>");
            }

            foreach (var point in sampledPoints) {
                writer.WriteLine("\t\t<roll>");
                WriteElement(writer, "ux", point.Normal.x);
                WriteElement(writer, "uy", -point.Normal.y);
                WriteElement(writer, "uz", -point.Normal.z);
                WriteElement(writer, "rx", point.Lateral.x);
                WriteElement(writer, "ry", -point.Lateral.y);
                WriteElement(writer, "rz", -point.Lateral.z);
                WriteElement(writer, "coord", point.Coord);
                writer.WriteLine("\t\t\t<strict>false</strict>");
                writer.WriteLine("\t\t</roll>");
            }

            writer.WriteLine("\t</element>");
            writer.WriteLine("</root>");
        }

        private static void WriteElement(StreamWriter writer, string name, float value) {
            writer.WriteLine($"\t\t\t<{name}>{value.ToString("e", CultureInfo.InvariantCulture)}</{name}>");
        }

        private struct SampledPoint {
            public float3 Position;
            public float3 Direction;
            public float3 Lateral;
            public float3 Normal;
            public float Coord;
            public bool IsStrict;
        }
    }
}
