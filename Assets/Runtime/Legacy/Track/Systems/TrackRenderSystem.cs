using KexEdit.Rendering;
using KexEdit.Sim.Schema;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackRenderSystem : SystemBase {
        private TrackMeshPipeline _pipeline;
        private ComputeShader _computeShader;
        private Material _material;
        private int _pieceStyleVersion;

        public TrackMeshPipeline Pipeline => _pipeline;

        protected override void OnCreate() {
            RequireForUpdate<TrackSingleton>();
            RequireForUpdate<Preferences>();
            RequireForUpdate<PieceStyleSingleton>();
            RequireForUpdate<StyleConfigSingleton>();
        }

        protected override void OnStartRunning() {
            LoadShaderResources();
            SetVisualizationColors();
        }

        protected override void OnStopRunning() {
            DisposePipeline();
        }

        protected override void OnDestroy() {
            DisposePipeline();
        }

        protected override void OnUpdate() {
            EnsurePipeline();

            if (_pipeline == null) return;

            var style = SystemAPI.TryGetSingleton<RenderStyleSingleton>(out var styleSingleton)
                ? styleSingleton.Style
                : RenderStyle.Default;

            var preferences = SystemAPI.GetSingleton<Preferences>();
            SetVisualizationRange(in preferences);

            var singleton = SystemAPI.GetSingleton<TrackSingleton>();
            ref readonly var track = ref singleton.Value;

            var keyframes = GetKeyframes();
            var sectionHighlights = BuildSectionHighlights(in track);

            try {
                _pipeline.Render(in track, in keyframes, in style, preferences.VisualizationMode, sectionHighlights);
            }
            finally {
                if (sectionHighlights.IsCreated) {
                    sectionHighlights.Dispose();
                }
            }
        }

        private NativeArray<float> BuildSectionHighlights(in KexEdit.Track.Track track) {
            if (!track.IsCreated || track.SectionCount == 0) return default;

            var sectionHighlights = new NativeArray<float>(track.SectionCount, Allocator.Temp);

            foreach (var node in SystemAPI.Query<Node>()) {
                if (!node.Selected) continue;

                if (!track.NodeToSection.TryGetValue(node.Id, out int sectionIndex)) continue;
                if (sectionIndex < 0 || sectionIndex >= sectionHighlights.Length) continue;

                sectionHighlights[sectionIndex] = 1f;
            }

            return sectionHighlights;
        }

        private KeyframeStore GetKeyframes() {
            foreach (var (coasterData, _) in SystemAPI.Query<CoasterData>().WithEntityAccess()) {
                if (coasterData.Value.Keyframes.Keyframes.IsCreated) {
                    return coasterData.Value.Keyframes;
                }
            }
            return default;
        }

        private void LoadShaderResources() {
            _computeShader = Resources.Load<ComputeShader>("DeformCompute");
            if (_computeShader == null) {
                Debug.LogError("TrackRenderSystem: Failed to load DeformCompute shader");
            }

            _material = Resources.Load<Material>("DeformMaterial");
            if (_material == null) {
                Debug.LogWarning("TrackRenderSystem: DeformMaterial not found in Resources");
            }
        }

        private void EnsurePipeline() {
            var pieceStyle = SystemAPI.ManagedAPI.GetSingleton<PieceStyleSingleton>();
            var styleConfig = SystemAPI.GetSingleton<StyleConfigSingleton>();

            if (_pipeline != null && styleConfig.Version == _pieceStyleVersion) return;

            DisposePipeline();

            if (_computeShader == null || _material == null) return;

            var pieces = pieceStyle.AllPieces;
            if (pieces == null || pieces.Length == 0) {
                Debug.LogWarning("TrackRenderSystem: No pieces loaded from style config");
                return;
            }

            if (!pieceStyle.TrackPieces.IsCreated || !pieceStyle.StyleRanges.IsCreated) {
                Debug.LogWarning("TrackRenderSystem: Native arrays not initialized");
                return;
            }

            _pieceStyleVersion = styleConfig.Version;
            _pipeline = new TrackMeshPipeline(
                _computeShader,
                _material,
                pieces,
                pieceStyle.TrackPieces,
                pieceStyle.StyleRanges,
                styleConfig.DefaultStyleIndex);

            Debug.Log($"TrackRenderSystem: Created pipeline with {pieces.Length} pieces, {styleConfig.StyleCount} styles");
        }

        private void DisposePipeline() {
            _pipeline?.Dispose();
            _pipeline = null;
        }

        private static void SetVisualizationColors() {
            Shader.SetGlobalColor("_MinColor", Constants.VISUALIZATION_MIN_COLOR);
            Shader.SetGlobalColor("_MaxColor", Constants.VISUALIZATION_MAX_COLOR);
            Shader.SetGlobalColor("_ZeroColor", Constants.VISUALIZATION_ZERO_COLOR);
            Shader.SetGlobalColor("_HighlightColor", Constants.HIGHLIGHT_COLOR);
            Shader.SetGlobalColor("_SelectedColor", Constants.SELECTED_COLOR);
        }

        private static void SetVisualizationRange(in Preferences preferences) {
            var mode = preferences.VisualizationMode;
            Shader.SetGlobalFloat("_VisualizationMode", (float)mode);

            var range = mode switch {
                VisualizationMode.Velocity => preferences.VelocityRange,
                VisualizationMode.NormalForce => preferences.NormalForceRange,
                VisualizationMode.LateralForce => preferences.LateralForceRange,
                VisualizationMode.RollSpeed => preferences.RollSpeedRange,
                _ => new float2(0f, 1f)
            };
            Shader.SetGlobalFloat("_MinValue", range.x);
            Shader.SetGlobalFloat("_MaxValue", range.y);
        }
    }
}
