using UnityEngine;
using Unity.Entities;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VisualizationSystem : SystemBase {
        private float _mode, _targetMode;
        private bool _showVelocity = false;

        private BufferLookup<Point> _pointLookup;

        private EntityQuery _sectionQuery;

        protected override void OnCreate() {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);

            _sectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point>()
                .Build(EntityManager);

            RequireForUpdate<GlobalMeshData>();
        }

        protected override void OnStartRunning() {
            _mode = 0f;
            _targetMode = 0f;
        }

        protected override void OnUpdate() {
            if (Keyboard.current.f1Key.wasPressedThisFrame) {
                Toggle();
            }

            float deltaTime = UnityEngine.Time.unscaledDeltaTime;
            _mode = math.lerp(_mode, _targetMode, math.saturate(deltaTime * 30f));

            var globalData = SystemAPI.ManagedAPI.GetSingleton<GlobalMeshData>();
            Shader.SetGlobalColor("_SelectedColor", globalData.SelectedColor);
            Shader.SetGlobalColor("_MinColor", Color.green);
            Shader.SetGlobalColor("_MaxColor", Color.red);
            Shader.SetGlobalFloat("_VisualizationMode", _mode);

            UpdateSelected();
            UpdateVelocity();
        }

        private void UpdateSelected() {
            float deltaTime = UnityEngine.Time.unscaledDeltaTime;
            float t = math.saturate(deltaTime * 30f);
            foreach (var (node, selectedBlend) in SystemAPI.Query<Node, RefRW<SelectedBlend>>()) {
                selectedBlend.ValueRW.Value = math.lerp(selectedBlend.ValueRW.Value, node.Selected ? 1f : 0f, t);
            }
        }

        private void UpdateVelocity() {
            _pointLookup.Update(this);

            NativeReference<float> maxVelocity = new(Allocator.TempJob) { Value = 0f };

            new VelocityJob {
                MaxVelocity = maxVelocity,
                PointLookup = _pointLookup
            }.Run(_sectionQuery);

            Shader.SetGlobalFloat("_MinValue", 0f);
            Shader.SetGlobalFloat("_MaxValue", maxVelocity.Value);

            maxVelocity.Dispose();
        }

        private void Toggle() {
            _showVelocity = !_showVelocity;
            if (_showVelocity) {
                _targetMode = 1f;
            }
            else {
                _targetMode = 0f;
            }
        }

        [BurstCompile]
        private partial struct VelocityJob : IJobEntity {
            public NativeReference<float> MaxVelocity;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(Entity entity) {
                if (!PointLookup.TryGetBuffer(entity, out var points)) return;
                foreach (var point in points) {
                    PointData p = point;
                    if (p.Velocity > MaxVelocity.Value) {
                        MaxVelocity.Value = p.Velocity;
                    }
                }
            }
        }
    }
}
