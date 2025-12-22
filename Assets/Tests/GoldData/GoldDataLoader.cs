using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tests {
    public static class GoldDataLoader {
        public static GoldTrackData Load(string path) {
            string fullPath = Path.Combine(Application.dataPath, "..", path);
            string json = File.ReadAllText(fullPath);
            return JsonUtility.FromJson<GoldTrackData>(json);
        }

        public static List<GoldSection> GetForceSections(GoldTrackData data) {
            var result = new List<GoldSection>();
            foreach (var section in data.sections) {
                if (section.nodeType == "ForceSection") {
                    result.Add(section);
                }
            }
            return result;
        }

        public static GoldSection GetForceSectionByIndex(GoldTrackData data, int index) {
            var forceSections = GetForceSections(data);
            if (index < 0 || index >= forceSections.Count) {
                throw new System.IndexOutOfRangeException(
                    $"ForceSection index {index} out of range. Found {forceSections.Count} force sections.");
            }
            return forceSections[index];
        }

        public static List<GoldSection> GetGeometricSections(GoldTrackData data) {
            var result = new List<GoldSection>();
            foreach (var section in data.sections) {
                if (section.nodeType == "GeometricSection") {
                    result.Add(section);
                }
            }
            return result;
        }

        public static GoldSection GetGeometricSectionByIndex(GoldTrackData data, int index) {
            var geometricSections = GetGeometricSections(data);
            if (index < 0 || index >= geometricSections.Count) {
                throw new System.IndexOutOfRangeException(
                    $"GeometricSection index {index} out of range. Found {geometricSections.Count} geometric sections.");
            }
            return geometricSections[index];
        }

        public static List<GoldSection> GetCurvedSections(GoldTrackData data) {
            var result = new List<GoldSection>();
            foreach (var section in data.sections) {
                if (section.nodeType == "CurvedSection") {
                    result.Add(section);
                }
            }
            return result;
        }

        public static GoldSection GetCurvedSectionByIndex(GoldTrackData data, int index) {
            var curvedSections = GetCurvedSections(data);
            if (index < 0 || index >= curvedSections.Count) {
                throw new System.IndexOutOfRangeException(
                    $"CurvedSection index {index} out of range. Found {curvedSections.Count} curved sections.");
            }
            return curvedSections[index];
        }

        public static List<GoldSection> GetCopyPathSections(GoldTrackData data) {
            var result = new List<GoldSection>();
            foreach (var section in data.sections) {
                if (section.nodeType == "CopyPathSection") {
                    result.Add(section);
                }
            }
            return result;
        }

        public static GoldSection GetCopyPathSectionByIndex(GoldTrackData data, int index) {
            var copyPathSections = GetCopyPathSections(data);
            if (index < 0 || index >= copyPathSections.Count) {
                throw new System.IndexOutOfRangeException(
                    $"CopyPathSection index {index} out of range. Found {copyPathSections.Count} copypath sections.");
            }
            return copyPathSections[index];
        }

        public static List<GoldSection> GetBridgeSections(GoldTrackData data) {
            var result = new List<GoldSection>();
            foreach (var section in data.sections) {
                if (section.nodeType == "Bridge") {
                    result.Add(section);
                }
            }
            return result;
        }

        public static GoldSection GetBridgeSectionByIndex(GoldTrackData data, int index) {
            var bridgeSections = GetBridgeSections(data);
            if (index < 0 || index >= bridgeSections.Count) {
                throw new System.IndexOutOfRangeException(
                    $"BridgeSection index {index} out of range. Found {bridgeSections.Count} bridge sections.");
            }
            return bridgeSections[index];
        }
    }
}
