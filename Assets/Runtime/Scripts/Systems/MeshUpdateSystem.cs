using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MeshUpdateSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var (mesh, anchor, render, dirtyRW) in SystemAPI.Query<MeshReference, Anchor, Render, RefRW<Dirty>>()) {
                if (mesh.Value == null) continue;

                mesh.Value.gameObject.SetActive(render.Value);

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

                mesh.Value.transform.SetPositionAndRotation(position, rotation);
                mesh.Value.transform.localScale = scale;
            }
        }
    }
}
