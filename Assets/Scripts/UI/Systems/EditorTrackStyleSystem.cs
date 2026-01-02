using KexEdit.Legacy;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class EditorTrackStyleSystem : SystemBase {
        private int _pieceStyleVersion;

        protected override void OnCreate() {
            RequireForUpdate<EditorTrackStyleSettingsSingleton>();
        }

        protected override void OnUpdate() {
            UpdateSettings();
        }

        private void UpdateSettings() {
            var singletonRW = SystemAPI.GetSingletonRW<EditorTrackStyleSettingsSingleton>();

            if (!singletonRW.ValueRO.Dirty) return;
            singletonRW.ValueRW.Dirty = false;

            var config = TrackStyleResourceLoader.LoadConfig(Preferences.CurrentTrackStyle);
            UpdateRenderStyle(config);
            UpdatePieceStyle(config);
        }

        private void UpdateRenderStyle(TrackStyleConfig config) {
            var style = new RenderStyle {
                PrimaryColor = config.GetColor(0),
                SecondaryColor = config.GetColor(1),
                TertiaryColor = config.GetColor(2)
            };

            if (SystemAPI.TryGetSingletonRW<RenderStyleSingleton>(out var singletonRW)) {
                singletonRW.ValueRW.Style = style;
            } else {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new RenderStyleSingleton { Style = style });
                EntityManager.SetName(entity, "Render Style");
            }
        }

        private void UpdatePieceStyle(TrackStyleConfig config) {
            _pieceStyleVersion++;

            TrackStyleResourceLoader.LoadStylePieces(
                config,
                out var allPieces,
                out var trackPieces,
                out var styleRanges,
                Allocator.Persistent);

            int styleCount = config.styles.Count > 0 ? config.styles.Count : 1;

            if (SystemAPI.ManagedAPI.TryGetSingleton<PieceStyleSingleton>(out var existing)) {
                existing.Dispose();
                existing.AllPieces = allPieces;
                existing.TrackPieces = trackPieces;
                existing.StyleRanges = styleRanges;
            } else {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new PieceStyleSingleton {
                    AllPieces = allPieces,
                    TrackPieces = trackPieces,
                    StyleRanges = styleRanges
                });
                EntityManager.SetName(entity, "Piece Style");
            }

            var styleConfig = new StyleConfigSingleton {
                DefaultStyleIndex = config.defaultStyle,
                StyleCount = styleCount,
                Version = _pieceStyleVersion
            };

            if (SystemAPI.TryGetSingletonRW<StyleConfigSingleton>(out var configRW)) {
                configRW.ValueRW = styleConfig;
            } else {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, styleConfig);
                EntityManager.SetName(entity, "Style Config");
            }
        }
    }
}
