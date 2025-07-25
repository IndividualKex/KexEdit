using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using static KexEdit.Constants;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class KeyframeGizmoRenderSystem : SystemBase {
        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _visualizationDataBuffer;
        private GraphicsBuffer _indirectArgsBuffer;

        private Mesh _sphereMesh;
        private Material _gizmoMaterial;
        private MaterialPropertyBlock _matProps;

        private Bounds _bounds;
        private NativeList<Keyframe> _keyframes;
        private NativeArray<PropertyType> _propertyTypes;

        private ComponentLookup<Duration> _durationLookup;
        private ComponentLookup<Anchor> _anchorLookup;
        private ComponentLookup<Node> _nodeLookup;
        private BufferLookup<Point> _pointLookup;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            var propertyTypes = System.Enum.GetValues(typeof(PropertyType));
            _propertyTypes = new NativeArray<PropertyType>(propertyTypes.Length, Allocator.Persistent);
            for (int i = 0; i < propertyTypes.Length; i++) {
                _propertyTypes[i] = (PropertyType)propertyTypes.GetValue(i);
            }

            _keyframes = new NativeList<Keyframe>(Allocator.Persistent);

            _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _gizmoMaterial = Resources.Load<Material>("KeyframeGizmo");
            _matProps = new MaterialPropertyBlock();

            _durationLookup = SystemAPI.GetComponentLookup<Duration>(true);
            _anchorLookup = SystemAPI.GetComponentLookup<Anchor>(true);
            _nodeLookup = SystemAPI.GetComponentLookup<Node>(true);
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);

            RequireForUpdate<Gizmos>();
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
            if (_propertyTypes.IsCreated) {
                _propertyTypes.Dispose();
            }
            if (_keyframes.IsCreated) {
                _keyframes.Dispose();
            }
        }

        protected override void OnUpdate() {
            if (!SystemAPI.GetSingleton<Gizmos>().DrawGizmos) return;

            _durationLookup.Update(this);
            _anchorLookup.Update(this);
            _nodeLookup.Update(this);
            _pointLookup.Update(this);

            using var entities = new NativeList<Entity>(Allocator.TempJob);
            using var keyframes = new NativeList<Keyframe>(Allocator.TempJob);

            foreach (var (node, render, entity) in SystemAPI.Query<Node, Render>().WithEntityAccess()) {
                if (!render) continue;

                foreach (var propertyType in _propertyTypes) {
                    EntityManager.GetAllKeyframes(entity, propertyType, _keyframes);

                    foreach (var keyframe in _keyframes) {
                        entities.Add(entity);
                        keyframes.Add(keyframe);
                    }
                }
            }

            int instanceCount = entities.Length;
            using var matrices = new NativeArray<float4x4>(instanceCount, Allocator.TempJob);
            using var visualizationData = new NativeArray<float4>(instanceCount, Allocator.TempJob);

            new CalculatePositionJob {
                Entities = entities.AsArray(),
                Keyframes = keyframes.AsArray(),
                DurationLookup = _durationLookup,
                AnchorLookup = _anchorLookup,
                NodeLookup = _nodeLookup,
                PointLookup = _pointLookup,
                Matrices = matrices,
                VisualizationData = visualizationData
            }.Schedule(instanceCount, 16).Complete();

            if (_matrixBuffer == null || _matrixBuffer.count != instanceCount) {
                _matrixBuffer?.Release();
                _visualizationDataBuffer?.Release();
                _indirectArgsBuffer?.Release();

                if (instanceCount > 0) {
                    _matrixBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16);
                    _visualizationDataBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 4);

                    _indirectArgsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.IndirectArguments,
                        1,
                        GraphicsBuffer.IndirectDrawIndexedArgs.size
                    );

                    var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(1, Allocator.Temp);
                    indirectArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                        indexCountPerInstance = _sphereMesh.GetIndexCount(0),
                        instanceCount = (uint)instanceCount
                    };
                    _indirectArgsBuffer.SetData(indirectArgs);
                    indirectArgs.Dispose();

                    _matProps.SetBuffer("_Matrices", _matrixBuffer);
                    _matProps.SetBuffer("_VisualizationData", _visualizationDataBuffer);
                }
            }

            if (_matrixBuffer != null && instanceCount > 0) {
                _matrixBuffer.SetData(matrices);
                _visualizationDataBuffer.SetData(visualizationData);

                var indirectArgs = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(1, Allocator.Temp);
                indirectArgs[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                    indexCountPerInstance = _sphereMesh.GetIndexCount(0),
                    instanceCount = (uint)instanceCount
                };
                _indirectArgsBuffer.SetData(indirectArgs);
                indirectArgs.Dispose();

                var rp = new RenderParams(_gizmoMaterial) {
                    worldBounds = _bounds,
                    matProps = _matProps
                };

                Graphics.RenderMeshIndirect(rp, _sphereMesh, _indirectArgsBuffer);
            }
        }

        [BurstCompile]
        private struct CalculatePositionJob : IJobParallelFor {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public NativeArray<Keyframe> Keyframes;
            [ReadOnly] public ComponentLookup<Duration> DurationLookup;
            [ReadOnly] public ComponentLookup<Anchor> AnchorLookup;
            [ReadOnly] public ComponentLookup<Node> NodeLookup;
            [ReadOnly] public BufferLookup<Point> PointLookup;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> VisualizationData;

            public void Execute(int i) {
                var entity = Entities[i];
                if (!PointLookup.TryGetBuffer(entity, out var points) ||
                    points.Length == 0) return;

                var keyframe = Keyframes[i];
                float position = TimeToPosition(entity, keyframe.Time);
                position = math.clamp(position, 0, points.Length - 1);

                int index = (int)math.floor(position);
                int nextIndex = math.min(index + 1, points.Length - 1);

                bool selected = keyframe.Selected && NodeLookup[entity].Selected;
                float3 worldPosition;
                float4 visualizationData = new(selected ? 1f : 0f, 0f, 0f, 0f);

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
    }
}
