using Unity.Entities;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace KexEdit.Legacy {
    public class TrackStyleBuffers : IComponentData, IDisposable {
        public AsyncGPUReadbackRequest? ComputeFence;
        public MeshBuffers CurrentBuffers;
        public MeshBuffers NextBuffers;

        public TrackStyleBuffers() { }

        public TrackStyleBuffers(TrackStyleData data, GizmoSettings gizmoSettings) {
            var extrusionMeshes = new List<ExtrusionMeshSettingsData>();
            foreach (var extrusionMesh in data.ExtrusionMeshes) {
                if (!ExtrusionMeshConverter.Convert(extrusionMesh.Mesh, out var outputMesh)) {
                    UnityEngine.Debug.LogError("Failed to convert extrusion mesh");
                    continue;
                }
                extrusionMeshes.Add(new ExtrusionMeshSettingsData {
                    Mesh = outputMesh,
                    Material = extrusionMesh.Material,
                });
            }

            CurrentBuffers = new MeshBuffers(
                extrusionMeshes,
                data.DuplicationMeshes,
                data.StartCapMeshes,
                data.EndCapMeshes,
                gizmoSettings.ExtrusionGizmos
            );
            NextBuffers = new MeshBuffers(
                extrusionMeshes,
                data.DuplicationMeshes,
                data.StartCapMeshes,
                data.EndCapMeshes,
                gizmoSettings.ExtrusionGizmos
            );
        }

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
