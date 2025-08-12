using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VisualizationSystem : SystemBase {
        public static VisualizationSystem Instance { get; private set; }

        private float _mode, _targetMode;
        private VisualizationMode _currentMode = VisualizationMode.None;

        private BufferLookup<Point> _pointLookup;

        private EntityQuery _sectionQuery;

        public static VisualizationMode CurrentMode => Instance._currentMode;
        public static bool ShowVelocity => Instance._currentMode == VisualizationMode.Velocity;

        public VisualizationSystem() {
            Instance = this;
        }

        protected override void OnCreate() {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);

            _sectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point>()
                .Build(EntityManager);

            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<Preferences>();
        }

        protected override void OnStartRunning() {
            _mode = 0f;
            _targetMode = 0f;
        }

        protected override void OnUpdate() {
            float deltaTime = UnityEngine.Time.unscaledDeltaTime;
            _mode = math.lerp(_mode, _targetMode, math.saturate(deltaTime * 30f));

            Shader.SetGlobalColor("_SelectedColor", Constants.SELECTED_COLOR);
            Shader.SetGlobalFloat("_VisualizationMode", _mode);

            SetVisualizationColors();
            UpdateVisualization();
        }

        private void SetVisualizationColors() {
            Shader.SetGlobalColor("_MinColor", new Color(0f, 0f, 1f, 1f)); // Pure blue
            Shader.SetGlobalColor("_MaxColor", new Color(1f, 0f, 0f, 1f)); // Pure red
            Shader.SetGlobalColor("_ZeroColor", new Color(0.7f, 0.7f, 0.7f, 1f)); // Light gray
        }

        private void UpdateVisualization() {
            if (_currentMode == VisualizationMode.None) {
                Shader.SetGlobalFloat("_MinValue", 0f);
                Shader.SetGlobalFloat("_MaxValue", 1f);
                return;
            }

            var preferences = SystemAPI.GetSingleton<Preferences>();
            var range = _currentMode switch {
                VisualizationMode.Velocity => preferences.VelocityRange,
                VisualizationMode.NormalForce => preferences.NormalForceRange,
                VisualizationMode.LateralForce => preferences.LateralForceRange,
                VisualizationMode.RollSpeed => preferences.RollSpeedRange,
                VisualizationMode.PitchSpeed => preferences.PitchSpeedRange,
                VisualizationMode.YawSpeed => preferences.YawSpeedRange,
                VisualizationMode.Curvature => preferences.CurvatureRange,
                _ => new float2(0f, 1f)
            };
            Shader.SetGlobalFloat("_MinValue", range.x);
            Shader.SetGlobalFloat("_MaxValue", range.y);
        }

        private void SetModeInternal(VisualizationMode mode) {
            if (_currentMode == mode) {
                _currentMode = VisualizationMode.None;
                _targetMode = 0f;
            }
            else {
                _currentMode = mode;
                _targetMode = 1f;
            }
        }

        public static void SetMode(VisualizationMode mode) {
            Instance.SetModeInternal(mode);
        }

        public static void Toggle() {
            Instance.SetModeInternal(VisualizationMode.Velocity);
        }
    }
}
