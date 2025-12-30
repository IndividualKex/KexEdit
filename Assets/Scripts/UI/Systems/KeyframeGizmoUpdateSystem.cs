using KexEdit.Legacy;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.Legacy.Constants;
using static KexEdit.Sim.Sim;
using static KexEdit.UI.Constants;
using Keyframe = KexEdit.Legacy.Keyframe;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class KeyframeGizmoUpdateSystem : SystemBase {
        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _visualizationDataBuffer;
        private ComputeBuffer _visualizationIndicesBuffer;
        private GraphicsBuffer _indirectArgsBuffer;

        private Camera _camera;
        private GameView _gameView;
        private Mesh _sphereMesh;
        private Material _gizmoMaterial;
        private MaterialPropertyBlock _matProps;
        private Bounds _bounds;
        private Vector2 _mousePosition;

        private NativeList<Keyframe> _keyframes;
        private NativeArray<PropertyType> _propertyTypes;
        private NativeArray<float3> _propertyColors;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            var propertyTypes = System.Enum.GetValues(typeof(PropertyType));
            _propertyTypes = new NativeArray<PropertyType>(propertyTypes.Length, Allocator.Persistent);
            _propertyColors = new NativeArray<float3>(propertyTypes.Length, Allocator.Persistent);
            for (int i = 0; i < propertyTypes.Length; i++) {
                _propertyTypes[i] = (PropertyType)propertyTypes.GetValue(i);
                _propertyColors[i] = new float3(0.8f, 0.8f, 0.8f);
            }
            _propertyColors[(int)PropertyType.RollSpeed] = s_RollSpeedColor.ToFloat3();
            _propertyColors[(int)PropertyType.NormalForce] = s_NormalForceColor.ToFloat3();
            _propertyColors[(int)PropertyType.LateralForce] = s_LateralForceColor.ToFloat3();
            _propertyColors[(int)PropertyType.PitchSpeed] = s_PitchSpeedColor.ToFloat3();
            _propertyColors[(int)PropertyType.YawSpeed] = s_YawSpeedColor.ToFloat3();
            _propertyColors[(int)PropertyType.ReadNormalForce] = s_NormalForceColor.ToFloat3();
            _propertyColors[(int)PropertyType.ReadLateralForce] = s_LateralForceColor.ToFloat3();
            _propertyColors[(int)PropertyType.ReadPitchSpeed] = s_PitchSpeedColor.ToFloat3();
            _propertyColors[(int)PropertyType.ReadYawSpeed] = s_YawSpeedColor.ToFloat3();
            _propertyColors[(int)PropertyType.ReadRollSpeed] = s_RollSpeedColor.ToFloat3();

            _keyframes = new NativeList<Keyframe>(Allocator.Persistent);

            _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _gizmoMaterial = Resources.Load<Material>("KeyframeGizmo");
            _matProps = new MaterialPropertyBlock();

            RequireForUpdate<KexEdit.Legacy.Preferences>();
            RequireForUpdate<GameViewData>();
        }

        protected override void OnDestroy() {
            _matrixBuffer?.Release();
            _matrixBuffer = null;
            _visualizationDataBuffer?.Release();
            _visualizationDataBuffer = null;
            _visualizationIndicesBuffer?.Release();
            _visualizationIndicesBuffer = null;
            _indirectArgsBuffer?.Release();
            _indirectArgsBuffer = null;
            _propertyTypes.Dispose();
            _propertyColors.Dispose();
            _keyframes.Dispose();
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            _gameView = root.Q<GameView>();

            _camera = UnityEngine.Camera.main;

            _gameView.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        }

        protected override void OnUpdate() {
            var gizmos = SystemAPI.GetSingleton<KexEdit.Legacy.Preferences>();
            ref var gameViewData = ref SystemAPI.GetSingletonRW<GameViewData>().ValueRW;
            if (!gizmos.DrawGizmos) {
                gameViewData.IntersectionKeyframe = default;
                return;
            }

            var keyframes = new NativeList<KeyframeReference>(Allocator.TempJob);

            if (!SystemAPI.TryGetSingleton<CoasterData>(out var coasterData)) {
                keyframes.Dispose();
                return;
            }

            new GatherKeyframesJob {
                PropertyTypes = _propertyTypes,
                KeyframeStore = coasterData.Value.Keyframes,
                Keyframes = keyframes
            }.Run();

            int count = keyframes.Length;
            if (count == 0) {
                gameViewData.IntersectionKeyframe = default;
                keyframes.Dispose();
                return;
            }

            var matrices = new NativeArray<float4x4>(count, Allocator.TempJob);
            var visualizationData = new NativeArray<float4>(count, Allocator.TempJob);

            float xNorm = _mousePosition.x / _gameView.resolvedStyle.width;
            float yNorm = 1f - _mousePosition.y / _gameView.resolvedStyle.height;
            var ray = _camera.ViewportPointToRay(new Vector3(xNorm, yNorm, 0f));

            var intersectionResult = new NativeReference<KeyframeIntersectionResult>(Allocator.TempJob);

            var keyframeArray = keyframes.AsArray();

            new CalculatePositionsJob {
                Keyframes = keyframeArray,
                PropertyColors = _propertyColors,
                DurationLookup = SystemAPI.GetComponentLookup<Duration>(true),
                AnchorLookup = SystemAPI.GetComponentLookup<Anchor>(true),
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                PointLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(true),
                Matrices = matrices,
                VisualizationData = visualizationData
            }.Schedule(count, 16).Complete();

            RenderKeyframes(count, ref matrices, ref visualizationData);

            Dependency = new KeyframeIntersectionJob {
                RayOrigin = ray.origin,
                RayDirection = math.normalize(ray.direction),
                Matrices = matrices,
                SphereRadius = 0.5f,
                IntersectionData = keyframeArray,
                Result = intersectionResult
            }.Schedule(Dependency);

            Dependency = matrices.Dispose(Dependency);

            Dependency = new StoreResultJob {
                IntersectionData = keyframeArray,
                Result = intersectionResult
            }.Schedule(Dependency);

            Dependency = intersectionResult.Dispose(Dependency);
            Dependency = keyframes.Dispose(Dependency);
        }

        private void RenderKeyframes(int count, ref NativeArray<float4x4> matrices, ref NativeArray<float4> visualizationData) {
            if (_matrixBuffer == null || _matrixBuffer?.count != count) {
                _matrixBuffer?.Release();
                _visualizationDataBuffer?.Release();
                _visualizationIndicesBuffer?.Release();
                _indirectArgsBuffer?.Release();

                if (count > 0) {
                    _matrixBuffer = new ComputeBuffer(count, sizeof(float) * 16);
                    _visualizationDataBuffer = new ComputeBuffer(count, sizeof(float) * 4);
                    _visualizationIndicesBuffer = new ComputeBuffer(count, sizeof(uint));

                    var indices = new NativeArray<uint>(count, Allocator.Temp);
                    for (int i = 0; i < count; i++) {
                        indices[i] = (uint)i;
                    }
                    _visualizationIndicesBuffer.SetData(indices);
                    indices.Dispose();

                    _indirectArgsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.IndirectArguments,
                        1,
                        GraphicsBuffer.IndirectDrawIndexedArgs.size
                    );

                    var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(1, Allocator.Temp);
                    indirectArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                        indexCountPerInstance = _sphereMesh.GetIndexCount(0),
                        instanceCount = (uint)count
                    };
                    _indirectArgsBuffer.SetData(indirectArgs);
                    indirectArgs.Dispose();

                    _matProps.SetBuffer("_Matrices", _matrixBuffer);
                    _matProps.SetBuffer("_VisualizationData", _visualizationDataBuffer);
                    _matProps.SetBuffer("_VisualizationIndices", _visualizationIndicesBuffer);
                }
            }

            if (_matrixBuffer != null && count > 0) {
                _matrixBuffer.SetData(matrices);
                _visualizationDataBuffer.SetData(visualizationData);

                var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(1, Allocator.Temp);
                indirectArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                    indexCountPerInstance = _sphereMesh.GetIndexCount(0),
                    instanceCount = (uint)count
                };
                _indirectArgsBuffer.SetData(indirectArgs);
                indirectArgs.Dispose();

                var rp = new RenderParams(_gizmoMaterial) {
                    worldBounds = _bounds,
                    matProps = _matProps
                };

                Graphics.RenderMeshIndirect(rp, _sphereMesh, _indirectArgsBuffer);
            }

            visualizationData.Dispose();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            _mousePosition = evt.localMousePosition;
        }

        [BurstCompile]
        private partial struct GatherKeyframesJob : IJobEntity {
            [ReadOnly] public NativeArray<PropertyType> PropertyTypes;
            [ReadOnly] public KeyframeStore KeyframeStore;
            [WriteOnly] public NativeList<KeyframeReference> Keyframes;

            public void Execute(Entity entity, in Node node, in Render render) {
                if (!render) return;

                foreach (var propertyType in PropertyTypes) {
                    var propertyId = PropertyTypeToPropertyId(propertyType);
                    if (propertyId == PropertyId.RollSpeed && propertyType != PropertyType.RollSpeed) continue;

                    if (!KeyframeStore.TryGet(node.Id, propertyId, out var slice)) continue;

                    for (int i = 0; i < slice.Length; i++) {
                        Keyframes.Add(new KeyframeReference {
                            Node = entity,
                            Keyframe = new KeyframeData(propertyType, slice[i])
                        });
                    }
                }
            }

            private static PropertyId PropertyTypeToPropertyId(PropertyType type) => type switch {
                PropertyType.RollSpeed => PropertyId.RollSpeed,
                PropertyType.NormalForce => PropertyId.NormalForce,
                PropertyType.LateralForce => PropertyId.LateralForce,
                PropertyType.PitchSpeed => PropertyId.PitchSpeed,
                PropertyType.YawSpeed => PropertyId.YawSpeed,
                PropertyType.FixedVelocity => PropertyId.DrivenVelocity,
                PropertyType.Heart => PropertyId.HeartOffset,
                PropertyType.Friction => PropertyId.Friction,
                PropertyType.Resistance => PropertyId.Resistance,
                PropertyType.TrackStyle => PropertyId.TrackStyle,
                _ => PropertyId.RollSpeed
            };
        }

        [BurstCompile]
        private struct CalculatePositionsJob : IJobParallelFor {
            [ReadOnly] public NativeArray<KeyframeReference> Keyframes;
            [ReadOnly] public NativeArray<float3> PropertyColors;
            [ReadOnly] public ComponentLookup<Duration> DurationLookup;
            [ReadOnly] public ComponentLookup<Anchor> AnchorLookup;
            [ReadOnly] public ComponentLookup<Node> NodeLookup;
            [ReadOnly] public BufferLookup<CorePointBuffer> PointLookup;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> VisualizationData;

            public void Execute(int i) {
                var entity = Keyframes[i].Node;
                if (!PointLookup.TryGetBuffer(entity, out var points) ||
                    points.Length == 0) return;

                var keyframe = Keyframes[i].Keyframe.Value;
                float position = TimeToPosition(entity, keyframe.Time);
                position = math.clamp(position, 0, points.Length - 1);

                int index = (int)math.floor(position);
                int nextIndex = math.min(index + 1, points.Length - 1);

                bool selected = keyframe.Selected && NodeLookup[entity].Selected;
                float3 worldPosition;

                var propertyType = Keyframes[i].Keyframe.Type;
                float3 propertyColor = PropertyColors[(int)propertyType];
                float4 visualizationData = new(propertyColor.x, propertyColor.y, propertyColor.z, selected ? 1f : 0f);

                if (index == nextIndex) {
                    worldPosition = points[index].HeartPosition();
                }
                else {
                    float t = position - index;
                    float3 p0 = points[index].HeartPosition();
                    float3 p1 = points[nextIndex].HeartPosition();
                    worldPosition = math.lerp(p0, p1, t);
                }

                Matrices[i] = float4x4.TRS(worldPosition, quaternion.identity, new float3(0.3f));
                VisualizationData[i] = visualizationData;
            }

            private float TimeToPosition(Entity section, float time) {
                if (!DurationLookup.TryGetComponent(section, out var duration)) {
                    return time * HZ;
                }

                if (duration.Type == Legacy.DurationType.Time) {
                    return time * HZ;
                }

                if (!AnchorLookup.TryGetComponent(section, out var anchor)) {
                    return time * HZ;
                }

                var pointBuffer = PointLookup[section];
                if (pointBuffer.Length < 2) return 0f;

                float targetDistance = anchor.Value.HeartArc + time;

                for (int i = 0; i < pointBuffer.Length - 1; i++) {
                    float currentDistance = pointBuffer[i].HeartArc();
                    float nextDistance = pointBuffer[i + 1].HeartArc();
                    if (targetDistance >= currentDistance && targetDistance <= nextDistance) {
                        float t = (nextDistance - currentDistance) > 0 ?
                            (targetDistance - currentDistance) / (nextDistance - currentDistance) : 0f;
                        return i + t;
                    }
                }

                return pointBuffer.Length - 1;
            }
        }

        [BurstCompile]
        private struct KeyframeIntersectionJob : IJob {
            [ReadOnly] public float3 RayOrigin;
            [ReadOnly] public float3 RayDirection;
            [ReadOnly] public NativeArray<float4x4> Matrices;
            [ReadOnly] public float SphereRadius;
            [ReadOnly] public NativeArray<KeyframeReference> IntersectionData;
            [WriteOnly] public NativeReference<KeyframeIntersectionResult> Result;

            public void Execute() {
                float rayLength = 1000f;
                float closestDistance = float.MaxValue;
                int closestIndex = -1;

                for (int i = 0; i < Matrices.Length; i++) {
                    float3 sphereCenter = Matrices[i].c3.xyz;
                    float3 toSphere = sphereCenter - RayOrigin;

                    float projectionLength = math.dot(toSphere, RayDirection);
                    if (projectionLength < 0 || projectionLength > rayLength) continue;

                    float3 closestPoint = RayOrigin + RayDirection * projectionLength;
                    float distanceToCenter = math.distance(closestPoint, sphereCenter);

                    if (distanceToCenter <= SphereRadius && projectionLength < closestDistance) {
                        closestDistance = projectionLength;
                        closestIndex = i;
                    }
                }

                Result.Value = new KeyframeIntersectionResult {
                    Hit = closestIndex >= 0,
                    KeyframeIndex = closestIndex,
                    Distance = closestDistance
                };
            }
        }

        [BurstCompile]
        private partial struct StoreResultJob : IJobEntity {
            [ReadOnly] public NativeArray<KeyframeReference> IntersectionData;
            [ReadOnly] public NativeReference<KeyframeIntersectionResult> Result;

            public void Execute(ref GameViewData gameViewData) {
                if (Result.Value.Hit) {
                    gameViewData.IntersectionKeyframe = IntersectionData[Result.Value.KeyframeIndex];
                }
                else {
                    gameViewData.IntersectionKeyframe = default;
                }
            }
        }

        private struct KeyframeIntersectionResult {
            public float Distance;
            public int KeyframeIndex;
            public bool Hit;
        }
    }
}
