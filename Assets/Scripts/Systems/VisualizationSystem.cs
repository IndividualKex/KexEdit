using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

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

            RequireForUpdate<TrackMeshGlobalSettings>();
        }

        protected override void OnStartRunning() {
            _mode = 0f;
            _targetMode = 0f;
        }

        protected override void OnUpdate() {
            float deltaTime = UnityEngine.Time.unscaledDeltaTime;
            _mode = math.lerp(_mode, _targetMode, math.saturate(deltaTime * 30f));

            var globalData = SystemAPI.ManagedAPI.GetSingleton<TrackMeshGlobalSettings>();
            Shader.SetGlobalColor("_SelectedColor", globalData.SelectedColor);
            Shader.SetGlobalFloat("_VisualizationMode", _mode);

            SetVisualizationColors();
            UpdateVisualization();
        }

        private void SetVisualizationColors() {
            Shader.SetGlobalColor("_MinColor", Color.green);
            Shader.SetGlobalColor("_MaxColor", Color.red);
        }

        private void UpdateVisualization() {
            if (_currentMode == VisualizationMode.None) {
                Shader.SetGlobalFloat("_MinValue", 0f);
                Shader.SetGlobalFloat("_MaxValue", 1f);
                return;
            }

            _pointLookup.Update(this);

            NativeReference<float2> valueRange = new(Allocator.TempJob) { Value = new float2(float.MaxValue, float.MinValue) };

            switch (_currentMode) {
                case VisualizationMode.Velocity:
                    new VelocityJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.NormalForce:
                    new NormalForceJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.LateralForce:
                    new LateralForceJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.RollSpeed:
                    new RollSpeedJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.PitchSpeed:
                    new PitchSpeedJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.YawSpeed:
                    new YawSpeedJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
                case VisualizationMode.Curvature:
                    new CurvatureJob { ValueRange = valueRange, PointLookup = _pointLookup }.Run(_sectionQuery);
                    break;
            }

            float2 range = valueRange.Value;
            if (range.x == float.MaxValue) {
                range = new float2(0f, 1f);
            }

            if (_currentMode == VisualizationMode.LateralForce) {
                float maxAbs = math.max(math.abs(range.x), math.abs(range.y));
                range = new float2(-maxAbs, maxAbs);
            }

            Shader.SetGlobalFloat("_MinValue", range.x);
            Shader.SetGlobalFloat("_MaxValue", range.y);

            valueRange.Dispose();
        }

        private void SetModeInternal(VisualizationMode mode) {
            if (_currentMode == mode) {
                _currentMode = VisualizationMode.None;
                _targetMode = 0f;
            } else {
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

        [BurstCompile]
        private partial struct VelocityJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, p.Velocity),
                        math.max(ValueRange.Value.y, p.Velocity)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct NormalForceJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, p.NormalForce),
                        math.max(ValueRange.Value.y, p.NormalForce)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct LateralForceJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, p.LateralForce),
                        math.max(ValueRange.Value.y, p.LateralForce)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct RollSpeedJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, p.RollSpeed),
                        math.max(ValueRange.Value.y, p.RollSpeed)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct PitchSpeedJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    float pitchSpeed = math.abs(p.PitchFromLast);
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, pitchSpeed),
                        math.max(ValueRange.Value.y, pitchSpeed)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct YawSpeedJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    float yawSpeed = math.abs(p.YawFromLast);
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, yawSpeed),
                        math.max(ValueRange.Value.y, yawSpeed)
                    );
                }
            }
        }

        [BurstCompile]
        private partial struct CurvatureJob : IJobEntity {
            public NativeReference<float2> ValueRange;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    float curvature = math.abs(p.AngleFromLast);
                    ValueRange.Value = new float2(
                        math.min(ValueRange.Value.x, curvature),
                        math.max(ValueRange.Value.y, curvature)
                    );
                }
            }
        }
    }
}
