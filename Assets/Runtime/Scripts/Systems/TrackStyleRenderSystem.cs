using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(TrackStyleBuildSystem))]
    public partial class TrackStyleRenderSystem : SystemBase {
        private Bounds _bounds;

        protected override void OnCreate() {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            RequireForUpdate<Preferences>();
        }

        protected override void OnUpdate() {
            var preferences = SystemAPI.GetSingleton<Preferences>();

            ShadowCastingMode shadowCastingMode = preferences.EnableShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            bool receiveShadows = preferences.EnableShadows;

            foreach (var buffers in SystemAPI.Query<TrackStyleBuffers>()) {
                if (buffers.CurrentBuffers == null ||
                    !buffers.CurrentBuffers.Active ||
                    buffers.CurrentBuffers.Count <= 1) continue;

                foreach (var buffer in buffers.CurrentBuffers.DuplicationBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Mesh,
                        buffer.DuplicationBuffer
                    );
                }

                foreach (var buffer in buffers.CurrentBuffers.ExtrusionBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows
                    };

                    Graphics.RenderPrimitives(
                        rp,
                        MeshTopology.Triangles,
                        buffer.ExtrusionIndicesBuffer.count
                    );
                }

                foreach (var buffer in buffers.CurrentBuffers.StartCapBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Mesh,
                        buffer.CapBuffer
                    );
                }

                foreach (var buffer in buffers.CurrentBuffers.EndCapBuffers) {
                    var rp = new RenderParams(buffer.Material) {
                        worldBounds = _bounds,
                        matProps = buffer.MatProps,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows
                    };

                    Graphics.RenderMeshIndirect(
                        rp,
                        buffer.Mesh,
                        buffer.CapBuffer
                    );
                }

                if (preferences.DrawGizmos) {
                    foreach (var buffer in buffers.CurrentBuffers.ExtrusionGizmoBuffers) {
                        var rp = new RenderParams(buffer.Material) {
                            worldBounds = _bounds,
                            matProps = buffer.MatProps,
                        };

                        Graphics.RenderPrimitives(
                            rp,
                            MeshTopology.Lines,
                            buffer.ExtrusionVerticesBuffer.count
                        );
                    }
                }
            }
        }
    }
}
