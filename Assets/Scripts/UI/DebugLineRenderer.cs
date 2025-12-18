using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class DebugLineRenderer : MonoBehaviour {
        public Vector3 StartPosition = new(-10f, 0f, 0f);
        public Vector3 EndPosition = new(10f, 0f, 0f);
        public Material Material;

        private Bounds _bounds;
        private MaterialPropertyBlock _matProps;
        private GraphicsBuffer _vertexBuffer;
        private GraphicsBuffer _indexBuffer;

        private void Start() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            _vertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                2,
                sizeof(float) * 3
            );
            _indexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                2,
                sizeof(uint)
            );

            Vector3[] vertices = new Vector3[] { StartPosition, EndPosition };
            uint[] indices = new uint[] { 0, 1 };

            _vertexBuffer.SetData(vertices);
            _indexBuffer.SetData(indices);

            _matProps = new MaterialPropertyBlock();
            _matProps.SetBuffer("_Vertices", _vertexBuffer);
            _matProps.SetBuffer("_Indices", _indexBuffer);
        }

        private void OnDestroy() {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        private void Update() {
            RenderParams rp = new(Material) {
                worldBounds = _bounds,
                matProps = _matProps
            };

            Graphics.RenderPrimitives(
                rp,
                MeshTopology.Lines,
                _indexBuffer.count
            );
        }
    }
}
