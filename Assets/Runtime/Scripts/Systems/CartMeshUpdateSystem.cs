using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CartSystem))]
    public partial class CartMeshUpdateSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var (mesh, transform) in SystemAPI.Query<CartMeshReference, LocalTransform>()) {
                if (mesh.Value == null) continue;
                mesh.Value.transform.SetPositionAndRotation(transform.Position, transform.Rotation);
                mesh.Value.transform.localScale = UnityEngine.Vector3.one * transform.Scale;
            }
        }
    }
}
