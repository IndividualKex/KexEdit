using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.UIElements;
using static KexEdit.Legacy.Constants;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
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
            if (_matrixBuffer != null) {
                _matrixBuffer.Release();
                _matrixBuffer = null;
            }
            if (_visualizationDataBuffer != null) {
                _visualizationDataBuffer.Release();
                _visualizationDataBuffer = null;
            }
            if (_visualizationIndicesBuffer != null) {
                _visualizationIndicesBuffer.Release();
                _visualizationIndicesBuffer = null;
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
            var gizmos = SystemAPI.GetSingleton<KexEdit.Legacy.Preferences>();
            ref var gameViewData = ref SystemAPI.GetSingletonRW<GameViewData>().ValueRW;
            if (!gizmos.DrawGizmos) {
                gameViewData.IntersectionKeyframe = default;
                return;
            }

            var keyframes = new NativeList<KeyframeReference>(Allocator.TempJob);

            new GatherKeyframesJob {
                PropertyTypes = _propertyTypes,
                RollSpeedLookup = SystemAPI.GetBufferLookup<RollSpeedKeyframe>(true),
                NormalForceLookup = SystemAPI.GetBufferLookup<NormalForceKeyframe>(true),
                LateralForceLookup = SystemAPI.GetBufferLookup<LateralForceKeyframe>(true),
                PitchSpeedLookup = SystemAPI.GetBufferLookup<PitchSpeedKeyframe>(true),
                YawSpeedLookup = SystemAPI.GetBufferLookup<YawSpeedKeyframe>(true),
                FixedVelocityLookup = SystemAPI.GetBufferLookup<FixedVelocityKeyframe>(true),
                HeartLookup = SystemAPI.GetBufferLookup<HeartKeyframe>(true),
                FrictionLookup = SystemAPI.GetBufferLookup<FrictionKeyframe>(true),
                ResistanceLookup = SystemAPI.GetBufferLookup<ResistanceKeyframe>(true),
                TrackStyleLookup = SystemAPI.GetBufferLookup<TrackStyleKeyframe>(true),
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
                        // Read-only properties don't have keyframes, skip them
                        case PropertyType.ReadNormalForce:
                        case PropertyType.ReadLateralForce:
                        case PropertyType.ReadPitchSpeed:
                        case PropertyType.ReadYawSpeed:
                        case PropertyType.ReadRollSpeed:
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

                if (duration.Type == DurationType.Time) {
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
