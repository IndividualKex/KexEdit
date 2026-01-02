using System.Collections.Generic;
using System.IO;
using System.Linq;
using KexEdit.Legacy;
using KexEdit.Rendering;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using UnityEngine;

namespace KexEdit.UI {
    public static class TrackStyleResourceLoader {
        public static TrackStyleConfig LoadConfig(string configPath) {
            string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, configPath);

            TrackStyleConfig config;

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"Track style config not found at StreamingAssets/{configPath}. Using default configuration.");
                config = CreateDefaultConfig();
            }
            else {
                try {
                    string configText = File.ReadAllText(fullPath);
                    config = JsonUtility.FromJson<TrackStyleConfig>(configText);
                }
                catch (System.Exception e) {
                    Debug.LogError($"Failed to parse TrackStyleConfig: {e.Message}. Using default configuration.");
                    config = CreateDefaultConfig();
                }
            }

            config.SourceFileName = configPath;
            return config;
        }

        public static PieceMesh[] LoadPieces(TrackStyleConfig config) {
            var pieces = new List<PieceMesh>();

            foreach (var pieceConfig in config.pieces) {
                string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, pieceConfig.mesh);
                var loadedMesh = ObjImporter.LoadMesh(fullPath);
                if (loadedMesh == null) {
                    Debug.LogWarning($"Piece mesh not found: {pieceConfig.mesh}. Skipping.");
                    continue;
                }

                pieces.Add(new PieceMesh(loadedMesh, pieceConfig.length));
            }

            return pieces.OrderByDescending(p => p.NominalLength).ToArray();
        }

        public static void LoadStylePieces(
            TrackStyleConfig config,
            out PieceMesh[] allPieces,
            out NativeArray<TrackPiece> trackPieces,
            out NativeArray<StylePieceRange> styleRanges,
            Allocator allocator
        ) {
            var allPiecesList = new List<PieceMesh>();
            var styleRangesList = new List<StylePieceRange>();

            int styleCount = config.styles.Count > 0 ? config.styles.Count : 1;

            for (int s = 0; s < styleCount; s++) {
                var piecesForStyle = GetPiecesForStyle(config, s);

                int startIndex = allPiecesList.Count;
                foreach (var pieceConfig in piecesForStyle) {
                    string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, pieceConfig.mesh);
                    var loadedMesh = ObjImporter.LoadMesh(fullPath);
                    if (loadedMesh == null) {
                        Debug.LogWarning($"Piece mesh not found: {pieceConfig.mesh}. Skipping.");
                        continue;
                    }
                    allPiecesList.Add(new PieceMesh(loadedMesh, pieceConfig.length));
                }

                int count = allPiecesList.Count - startIndex;
                styleRangesList.Add(new StylePieceRange(startIndex, count));
            }

            // Sort pieces within each style range by length (largest first)
            for (int s = 0; s < styleRangesList.Count; s++) {
                var range = styleRangesList[s];
                if (range.Count <= 1) continue;

                var subset = allPiecesList.GetRange(range.StartIndex, range.Count)
                    .OrderByDescending(p => p.NominalLength)
                    .ToList();

                for (int i = 0; i < range.Count; i++) {
                    allPiecesList[range.StartIndex + i] = subset[i];
                }
            }

            allPieces = allPiecesList.ToArray();

            // Build native TrackPiece array
            trackPieces = new NativeArray<TrackPiece>(allPieces.Length, allocator);
            for (int i = 0; i < allPieces.Length; i++) {
                trackPieces[i] = new TrackPiece(allPieces[i].NominalLength, i);
            }

            // Build native StylePieceRange array
            styleRanges = new NativeArray<StylePieceRange>(styleRangesList.Count, allocator);
            for (int i = 0; i < styleRangesList.Count; i++) {
                styleRanges[i] = styleRangesList[i];
            }
        }

        private static List<PieceMeshConfig> GetPiecesForStyle(TrackStyleConfig config, int styleIndex) {
            if (config.styles.Count > styleIndex && config.styles[styleIndex].pieces.Count > 0) {
                return config.styles[styleIndex].pieces;
            }
            return config.pieces;
        }

        private static TrackStyleConfig CreateDefaultConfig() {
            return new TrackStyleConfig {
                name = "Default",
                colors = new[] { new Color(0.6f, 0.435f, 0.27f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f) },
                pieces = new List<PieceMeshConfig> {
                    new() { mesh = "classic_10m.obj", length = 10f },
                    new() { mesh = "classic_2m.obj", length = 2f }
                },
                SourceFileName = "Classic.json"
            };
        }
    }
}
