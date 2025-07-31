using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.UIElements;
using static KexEdit.Constants;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class KeyframeGizmoUpdateSystem : SystemBase {
        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _visualizationDataBuffer;
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

        private ComponentLookup<Duration> _durationLookup;
        private ComponentLookup<Anchor> _anchorLookup;
        private ComponentLookup<Node> _nodeLookup;
        private BufferLookup<Point> _pointLookup;
        private BufferLookup<RollSpeedKeyframe> _rollSpeedLookup;
        private BufferLookup<NormalForceKeyframe> _normalForceLookup;
        private BufferLookup<LateralForceKeyframe> _lateralForceLookup;
        private BufferLookup<PitchSpeedKeyframe> _pitchSpeedLookup;
        private BufferLookup<YawSpeedKeyframe> _yawSpeedLookup;
        private BufferLookup<FixedVelocityKeyframe> _fixedVelocityLookup;
        private BufferLookup<HeartKeyframe> _heartLookup;
        private BufferLookup<FrictionKeyframe> _frictionLookup;
        private BufferLookup<ResistanceKeyframe> _resistanceLookup;
        private BufferLookup<TrackStyleKeyframe> _trackStyleLookup;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            var propertyTypes = System.Enum.GetValues(typeof(PropertyType));
            _propertyTypes = new NativeArray<PropertyType>(propertyTypes.Length, Allocator.Persistent);
            _propertyColors = new NativeArray<float3>(propertyTypes.Length, Allocator.Persistent);
            for (int i = 0; i < propertyTypes.Length; i++) {
                _propertyTypes[i] = (PropertyType)propertyTypes.GetValue(i);
                _propertyColors[i] = new float3(0.8f, 0.8f, 0.8f);
            }
            _propertyColors[(int)PropertyType.RollSpeed] = new float3(1.0f, 0.1f, 0.1f);
            _propertyColors[(int)PropertyType.NormalForce] = new float3(0.1f, 0.3f, 1.0f);
            _propertyColors[(int)PropertyType.LateralForce] = new float3(0.1f, 1.0f, 0.1f);
            _propertyColors[(int)PropertyType.PitchSpeed] = new float3(0.1f, 0.8f, 1.0f);
            _propertyColors[(int)PropertyType.YawSpeed] = new float3(1.0f, 0.9f, 0.1f);

            _keyframes = new NativeList<Keyframe>(Allocator.Persistent);

            _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _gizmoMaterial = Resources.Load<Material>("KeyframeGizmo");
            _matProps = new MaterialPropertyBlock();

            _durationLookup = SystemAPI.GetComponentLookup<Duration>(true);
            _anchorLookup = SystemAPI.GetComponentLookup<Anchor>(true);
            _nodeLookup = SystemAPI.GetComponentLookup<Node>(true);
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);
            _rollSpeedLookup = SystemAPI.GetBufferLookup<RollSpeedKeyframe>(true);
            _normalForceLookup = SystemAPI.GetBufferLookup<NormalForceKeyframe>(true);
            _lateralForceLookup = SystemAPI.GetBufferLookup<LateralForceKeyframe>(true);
            _pitchSpeedLookup = SystemAPI.GetBufferLookup<PitchSpeedKeyframe>(true);
            _yawSpeedLookup = SystemAPI.GetBufferLookup<YawSpeedKeyframe>(true);
            _fixedVelocityLookup = SystemAPI.GetBufferLookup<FixedVelocityKeyframe>(true);
            _heartLookup = SystemAPI.GetBufferLookup<HeartKeyframe>(true);
            _frictionLookup = SystemAPI.GetBufferLookup<FrictionKeyframe>(true);
            _resistanceLookup = SystemAPI.GetBufferLookup<ResistanceKeyframe>(true);
            _trackStyleLookup = SystemAPI.GetBufferLookup<TrackStyleKeyframe>(true);

            RequireForUpdate<Gizmos>();
            RequireForUpdate<GameViewData>();
        }

        protected override void OnDestroy() {
            if (_matrixBuffer != null) {
                _matrixBuffer.Release();
                _matrixBuffer = null;
            }
            if (_visualizationDataBuffer != null) {
                _visualizationDataBuffer.Release();
                _visualizationDataBuffer = null;
            }
            if (_indirectArgsBuffer != null) {
                _indirectArgsBuffer.Release();
                _indirectArgsBuffer = null;
            }
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
            var gizmos = SystemAPI.GetSingleton<Gizmos>();
            ref var gameViewData = ref SystemAPI.GetSingletonRW<GameViewData>().ValueRW;
            if (!gizmos.DrawGizmos) {
                gameViewData.IntersectionKeyframe = default;
                return;
            }

            _durationLookup.Update(this);
            _anchorLookup.Update(this);
            _nodeLookup.Update(this);
            _pointLookup.Update(this);
            _rollSpeedLookup.Update(this);
            _normalForceLookup.Update(this);
            _lateralForceLookup.Update(this);
            _pitchSpeedLookup.Update(this);
            _yawSpeedLookup.Update(this);
            _fixedVelocityLookup.Update(this);
            _heartLookup.Update(this);
            _frictionLookup.Update(this);
            _resistanceLookup.Update(this);
            _trackStyleLookup.Update(this);

            var keyframes = new NativeList<KeyframeReference>(Allocator.TempJob);

            new GatherKeyframesJob {
                PropertyTypes = _propertyTypes,
                RollSpeedLookup = _rollSpeedLookup,
                NormalForceLookup = _normalForceLookup,
                LateralForceLookup = _lateralForceLookup,
                PitchSpeedLookup = _pitchSpeedLookup,
                YawSpeedLookup = _yawSpeedLookup,
                FixedVelocityLookup = _fixedVelocityLookup,
                HeartLookup = _heartLookup,
                FrictionLookup = _frictionLookup,
                ResistanceLookup = _resistanceLookup,
                TrackStyleLookup = _trackStyleLookup,
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
                DurationLookup = _durationLookup,
                AnchorLookup = _anchorLookup,
                NodeLookup = _nodeLookup,
                PointLookup = _pointLookup,
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
                _indirectArgsBuffer?.Release();

                if (count > 0) {
                    _matrixBuffer = new ComputeBuffer(count, sizeof(float) * 16);
                    _visualizationDataBuffer = new ComputeBuffer(count, sizeof(float) * 4);

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
            [ReadOnly] public BufferLookup<RollSpeedKeyframe> RollSpeedLookup;
            [ReadOnly] public BufferLookup<NormalForceKeyframe> NormalForceLookup;
            [ReadOnly] public BufferLookup<LateralForceKeyframe> LateralForceLookup;
            [ReadOnly] public BufferLookup<PitchSpeedKeyframe> PitchSpeedLookup;
            [ReadOnly] public BufferLookup<YawSpeedKeyframe> YawSpeedLookup;
            [ReadOnly] public BufferLookup<FixedVelocityKeyframe> FixedVelocityLookup;
            [ReadOnly] public BufferLookup<HeartKeyframe> HeartLookup;
            [ReadOnly] public BufferLookup<FrictionKeyframe> FrictionLookup;
            [ReadOnly] public BufferLookup<ResistanceKeyframe> ResistanceLookup;
            [ReadOnly] public BufferLookup<TrackStyleKeyframe> TrackStyleLookup;
            [WriteOnly] public NativeList<KeyframeReference> Keyframes;

            public void Execute(Entity entity, in Node node, in Render render) {
                if (!render) return;

                foreach (var propertyType in PropertyTypes) {
                    switch (propertyType) {
                        case PropertyType.RollSpeed:
                            if (!RollSpeedLookup.TryGetBuffer(entity, out var rollSpeedBuffer)) continue;
                            for (int i = 0; i < rollSpeedBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, rollSpeedBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.NormalForce:
                            if (!NormalForceLookup.TryGetBuffer(entity, out var normalForceBuffer)) continue;
                            for (int i = 0; i < normalForceBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, normalForceBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.LateralForce:
                            if (!LateralForceLookup.TryGetBuffer(entity, out var lateralForceBuffer)) continue;
                            for (int i = 0; i < lateralForceBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, lateralForceBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.PitchSpeed:
                            if (!PitchSpeedLookup.TryGetBuffer(entity, out var pitchSpeedBuffer)) continue;
                            for (int i = 0; i < pitchSpeedBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, pitchSpeedBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.YawSpeed:
                            if (!YawSpeedLookup.TryGetBuffer(entity, out var yawSpeedBuffer)) continue;
                            for (int i = 0; i < yawSpeedBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, yawSpeedBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.FixedVelocity:
                            if (!FixedVelocityLookup.TryGetBuffer(entity, out var fixedVelocityBuffer)) continue;
                            for (int i = 0; i < fixedVelocityBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, fixedVelocityBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.Heart:
                            if (!HeartLookup.TryGetBuffer(entity, out var heartBuffer)) continue;
                            for (int i = 0; i < heartBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, heartBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.Friction:
                            if (!FrictionLookup.TryGetBuffer(entity, out var frictionBuffer)) continue;
                            for (int i = 0; i < frictionBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, frictionBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.Resistance:
                            if (!ResistanceLookup.TryGetBuffer(entity, out var resistanceBuffer)) continue;
                            for (int i = 0; i < resistanceBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, resistanceBuffer[i].Value)
                                });
                            }
                            break;
                        case PropertyType.TrackStyle:
                            if (!TrackStyleLookup.TryGetBuffer(entity, out var trackStyleBuffer)) continue;
                            for (int i = 0; i < trackStyleBuffer.Length; i++) {
                                Keyframes.Add(new KeyframeReference {
                                    Node = entity,
                                    Keyframe = new KeyframeData(propertyType, trackStyleBuffer[i].Value)
                                });
                            }
                            break;
                        default:
                            throw new System.NotImplementedException($"GatherKeyframesJob not implemented for PropertyType: {propertyType}");
                    }
                }
            }
        }

        [BurstCompile]
        private struct CalculatePositionsJob : IJobParallelFor {
            [ReadOnly] public NativeArray<KeyframeReference> Keyframes;
            [ReadOnly] public NativeArray<float3> PropertyColors;
            [ReadOnly] public ComponentLookup<Duration> DurationLookup;
            [ReadOnly] public ComponentLookup<Anchor> AnchorLookup;
            [ReadOnly] public ComponentLookup<Node> NodeLookup;
            [ReadOnly] public BufferLookup<Point> PointLookup;
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
                    worldPosition = points[index].Value.Position;
                }
                else {
                    float t = position - index;
                    PointData p0 = points[index].Value;
                    PointData p1 = points[nextIndex].Value;
                    worldPosition = math.lerp(p0.Position, p1.Position, t);
                }

                Matrices[i] = float4x4.TRS(worldPosition, quaternion.identity, new float3(0.3f));
                VisualizationData[i] = visualizationData;
            }

            private float TimeToPosition(Entity section, float time) {
                if (!DurationLookup.TryGetComponent(section, out var duration)) {
                    return time * HZ;
                }

                if (duration.Type == DurationType.Time) {
                    return time * HZ;
                }

                if (!AnchorLookup.TryGetComponent(section, out var anchor)) {
                    return time * HZ;
                }

                var pointBuffer = PointLookup[section];
                if (pointBuffer.Length < 2) return 0f;

                float targetDistance = anchor.Value.TotalLength + time;

                for (int i = 0; i < pointBuffer.Length - 1; i++) {
                    float currentDistance = pointBuffer[i].Value.TotalLength;
                    float nextDistance = pointBuffer[i + 1].Value.TotalLength;
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
