using KexEdit.Legacy;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.Legacy.Debug {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SplineGizmos : SystemBase {
        protected override void OnUpdate() {
            foreach (var splineBuffer in SystemAPI.Query<DynamicBuffer<SplineBuffer>>()) {
                if (splineBuffer.Length < 2) continue;

                for (int i = 0; i < splineBuffer.Length - 1; i++) {
                    UnityEngine.Debug.DrawLine(
                        splineBuffer[i].Point.Position,
                        splineBuffer[i + 1].Point.Position,
                        Color.cyan,
                        duration: 0.1f
                    );
                }
            }
        }
    }
}
