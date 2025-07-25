using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GridSystem : SystemBase {
        public static GridSystem Instance { get; private set; }

        private Material _gridMaterial;
        private GameObject _groundPlane;
        private GraphicsBuffer _vertexBuffer;
        private GraphicsBuffer _indexBuffer;
        private MaterialPropertyBlock _matProps;
        private Bounds _bounds;
        private UnityEngine.Camera _camera;
        private Vector3 _lastGridCenter;

        private bool _showGrid = true;
        private int _gridSize = 250;
        private float _gridSpacing = 10f;

        public bool ShowGrid => _showGrid;

        public GridSystem() {
            Instance = this;
        }

        protected override void OnStartRunning() {
            _groundPlane = GameObject.Find("GroundPlane");
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            _camera = UnityEngine.Camera.main;
            _gridMaterial = UIService.Instance.GridMaterial;
            GenerateGridMesh();
            _groundPlane.SetActive(_showGrid);
        }

        protected override void OnStopRunning() {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            Instance = null;
        }

        protected override void OnUpdate() {
            if (!_showGrid || _camera == null) return;

            Vector3 cameraPos = _camera.transform.position;
            Vector3 gridCenter = new(
                Mathf.Round(cameraPos.x / _gridSpacing) * _gridSpacing,
                0f,
                Mathf.Round(cameraPos.z / _gridSpacing) * _gridSpacing
            );
            _groundPlane.transform.position = gridCenter;

            if (Vector3.Distance(gridCenter, _lastGridCenter) > _gridSpacing * 0.5f) {
                _lastGridCenter = gridCenter;
                GenerateGridMesh(gridCenter);
            }

            RenderParams rp = new(_gridMaterial) {
                worldBounds = _bounds,
                matProps = _matProps
            };

            Graphics.RenderPrimitives(
                rp,
                MeshTopology.Lines,
                _indexBuffer.count
            );
        }

        private void GenerateGridMesh(Vector3 center = default) {
            int vertexCount = (_gridSize + 1) * 4;
            int indexCount = (_gridSize + 1) * 4;

            var vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob);
            var indices = new NativeArray<uint>(indexCount, Allocator.TempJob);

            new GenerateGridJob {
                GridSize = _gridSize,
                GridSpacing = _gridSpacing,
                Center = center,
                Vertices = vertices,
                Indices = indices
            }.Run();

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                vertexCount,
                sizeof(float) * 3
            );

            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                indexCount,
                sizeof(uint)
            );

            _vertexBuffer.SetData(vertices);
            _indexBuffer.SetData(indices);

            vertices.Dispose();
            indices.Dispose();

            _matProps = new MaterialPropertyBlock();
            _matProps.SetBuffer("_Vertices", _vertexBuffer);
            _matProps.SetBuffer("_Indices", _indexBuffer);
        }

        public void ToggleGrid() {
            _showGrid = !_showGrid;
            _groundPlane.SetActive(_showGrid);
        }

        [BurstCompile]
        private struct GenerateGridJob : IJob {
            public int GridSize;
            public float GridSpacing;
            public float3 Center;

            public NativeArray<float3> Vertices;
            public NativeArray<uint> Indices;

            public void Execute() {
                int halfSize = GridSize / 2;
                int indexCounter = 0;

                for (int x = -halfSize; x <= halfSize; x++) {
                    float xPos = Center.x + x * GridSpacing;

                    int baseIndex = x + halfSize;
                    Vertices[baseIndex * 2] = new float3(xPos, 0.01f, Center.z + (-halfSize * GridSpacing));
                    Vertices[baseIndex * 2 + 1] = new float3(xPos, 0.01f, Center.z + (halfSize * GridSpacing));

                    Indices[indexCounter++] = (uint)(baseIndex * 2);
                    Indices[indexCounter++] = (uint)(baseIndex * 2 + 1);
                }

                int zLinesOffset = (GridSize + 1) * 2;
                for (int z = -halfSize; z <= halfSize; z++) {
                    float zPos = Center.z + z * GridSpacing;

                    int baseIndex = z + halfSize;
                    int vertexOffset = zLinesOffset + baseIndex * 2;
                    Vertices[vertexOffset] = new float3(Center.x + (-halfSize * GridSpacing), 0.01f, zPos);
                    Vertices[vertexOffset + 1] = new float3(Center.x + (halfSize * GridSpacing), 0.01f, zPos);

                    Indices[indexCounter++] = (uint)vertexOffset;
                    Indices[indexCounter++] = (uint)(vertexOffset + 1);
                }
            }
        }
    }
}
