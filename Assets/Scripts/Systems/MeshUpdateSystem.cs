using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MeshUpdateSystem : SystemBase {
        private HashSet<ManagedMesh> _managedMeshes = new();

        protected override void OnUpdate() {
            _managedMeshes.Clear();

            foreach (var (meshReference, anchor, render, dirtyRW) in SystemAPI.Query<MeshReference, Anchor, Render, RefRW<Dirty>>()) {
                if (meshReference.Value == null) continue;
                _managedMeshes.Add(meshReference.Value);

                meshReference.Value.gameObject.SetActive(render.Value);

                if (!render.Value) continue;

                ref bool dirty = ref dirtyRW.ValueRW.Value;
                if (!dirty) continue;
                dirty = false;

                Vector3 position = anchor.Value.Position;
                Quaternion rotation = Quaternion.Euler(
                    anchor.Value.Roll,
                    anchor.Value.Velocity,
                    anchor.Value.Energy
                );
                Vector3 scale = Vector3.one * anchor.Value.NormalForce;

                meshReference.Value.transform.SetPositionAndRotation(position, rotation);
                meshReference.Value.transform.localScale = scale;
            }

            foreach (var managedMesh in GameObject.FindObjectsByType<ManagedMesh>(FindObjectsSortMode.None)) {
                if (_managedMeshes.Contains(managedMesh)) continue;
                GameObject.Destroy(managedMesh.gameObject);
            }
        }
    }
}
